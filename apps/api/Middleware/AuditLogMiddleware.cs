// AuditLogMiddleware records every authenticated mutation to the audit_log table.
//
// PIPELINE POSITION:
//   Registered AFTER ClerkAuthMiddleware in Program.cs.
//   That order matters — ClerkAuthMiddleware sets ctx.Items["PersonId"],
//   and we read it via ctx.GetPersonId(). If we ran first, every log row
//   would be anonymous because the identity hasn't been attached yet.
//
// WHEN WE LOG:
//   After the endpoint completes (we call _next first), so we only record
//   successful outcomes. A failed DB write, a 500, a validation error —
//   none of those get a log row. We're logging "what happened", not "what was attempted".
//
// WHAT DOES NOT GET LOGGED HERE:
//   Rich domain events (role changes, bootstrap super-admin, check-in merges)
//   are written directly by the relevant service with TargetType + TargetId + Details.
//   This middleware only captures the generic "actor did HTTP verb to path" pattern.
//
// FAILURE MODE:
//   If Postgres is unavailable when we try to save, we swallow the exception
//   and let the response go through anyway. A missing audit row is better than
//   a 500 reaching the user for a request that already succeeded.
public class AuditLogMiddleware
{
    private readonly RequestDelegate _next;

    // RequestDelegate is ASP.NET's way of representing "the rest of the pipeline".
    // Calling _next(ctx) hands control to the next middleware or endpoint.
    public AuditLogMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    // AppDbContext is injected per-request (Scoped lifetime).
    // It must be in the method signature (not the constructor) because middleware
    // instances are singletons — injecting Scoped services into the constructor
    // would lock in a single DbContext for the entire app lifetime (a "captive dependency").
    public async Task InvokeAsync(HttpContext ctx, AppDbContext db)
    {
        // Run the rest of the pipeline first.
        // After this line returns, the response has been written and the status code is set.
        await _next(ctx);

        // ── Filter: only log the interesting stuff ─────────────────────────────

        // Only mutations — GETs and HEADs are reads, no state was changed.
        var isMutation = ctx.Request.Method is "POST" or "PATCH" or "PUT" or "DELETE";

        // Only authenticated users — anonymous mutations shouldn't reach protected
        // endpoints, but if they somehow do, we can't attribute the action anyway.
        var personId = ctx.GetPersonId();
        var isAuthed = personId != null;

        // Only successes — 4xx means the request was rejected, nothing changed.
        // 5xx means something broke server-side; we don't want a log row for that.
        var isSuccess = ctx.Response.StatusCode < 400;

        if (!isMutation || !isAuthed || !isSuccess) return;

        // ── Write the log row ─────────────────────────────────────────────────

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorPersonId = personId,

            // e.g. "POST /api/events" or "DELETE /api/members/abc-123"
            // Enough to reconstruct what happened without storing the request body
            Action = $"{ctx.Request.Method} {ctx.Request.Path}",

            // IP and user-agent for security review purposes.
            // IpAddress will be the load balancer IP in production —
            // we'll need X-Forwarded-For parsing (Phase 2 hardening) for the real client IP.
            IpAddress = ctx.Connection.RemoteIpAddress?.ToString(),
            UserAgent = ctx.Request.Headers.UserAgent.ToString(),

            At = DateTime.UtcNow,
        });

        // Swallow any exception from the audit write.
        // We never want an audit log failure to degrade the user's response.
        // If this becomes a problem, wire in a background retry queue in Phase 2.
        try { await db.SaveChangesAsync(); }
        catch { /* deliberately swallowed — see file comment above */ }
    }
}
