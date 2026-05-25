// EmailEndpoints — outbound email API via AWS SES.
//
// ROUTES:
//   POST /api/email/send    → targeted send to specific addresses      (admin)
//   POST /api/email/blast   → fan-out to all members (or role filter)  (admin)
//   POST /api/email/welcome → manual welcome send for a given email    (admin)
//
// All three routes require Admin role and are under the "comms" rate limit
// (1 request/minute per admin) to prevent accidental mass sends and cost overruns.
//
// AUDIT TRAIL:
//   The AuditLogMiddleware automatically records every POST here.
//   For blasts, we get one audit row for the trigger, not one per recipient.
//
// ADDING MORE EMAIL TYPES:
//   Add a new endpoint method here and a new method to ISesEmailService.
//   Keep handlers thin — all logic lives in EmailService.

public static class EmailEndpoints
{
    public static void MapEmailEndpoints(this WebApplication app)
    {
        // ── POST /api/email/send ──────────────────────────────────────────────
        // Send to an explicit list of addresses.
        // Body: { to, subject, body, htmlBody?, fromName?, replyTo?, cc? }
        app.MapPost("/api/email/send",
            async (EmailSendRequest req, ISesEmailService email) =>
            {
                // Validate: must have at least one recipient and a subject
                if (req.To.Length == 0)
                    return Results.Problem(statusCode: 400, title: "Bad request",
                        detail: "At least one recipient is required.");

                if (string.IsNullOrWhiteSpace(req.Subject))
                    return Results.Problem(statusCode: 400, title: "Bad request",
                        detail: "Subject is required.");

                if (string.IsNullOrWhiteSpace(req.Body))
                    return Results.Problem(statusCode: 400, title: "Bad request",
                        detail: "Body is required.");

                var result = await email.SendAsync(req);

                return result.Success
                    ? Results.Ok(result)
                    : Results.Problem(statusCode: 502, title: "Email delivery failed",
                        detail: result.Error);
            })
            .RequireAdmin()
            .RequireRateLimiting("comms")
            .WithSummary("Send email to specific recipients via SES")
            .WithDescription("Admin-only. Sends to the specified To/Cc list with an optional HTML body. Rate limited to 1 call/min.");

        // ── POST /api/email/blast ─────────────────────────────────────────────
        // Fan-out to all active members, or a role-filtered subset.
        // Body: { subject, body, htmlBody?, roleFilter? }
        //
        // roleFilter values: null (all members), "member", "eboard", "admin", "dev_team"
        // The service reads Person rows from the DB and resolves emails there.
        app.MapPost("/api/email/blast",
            async (EmailBlastRequest req, ISesEmailService email) =>
            {
                if (string.IsNullOrWhiteSpace(req.Subject))
                    return Results.Problem(statusCode: 400, title: "Bad request",
                        detail: "Subject is required.");

                if (string.IsNullOrWhiteSpace(req.Body))
                    return Results.Problem(statusCode: 400, title: "Bad request",
                        detail: "Body is required.");

                var result = await email.BlastAsync(req);

                // Even partial success is a 200 — the caller inspects Sent/Failed counts.
                // A complete failure (Sent = 0) returns 502.
                return result.Sent > 0
                    ? Results.Ok(result)
                    : Results.Problem(statusCode: 502, title: "Blast failed",
                        detail: result.Error);
            })
            .RequireAdmin()
            .RequireRateLimiting("comms")
            .WithSummary("Blast email to all members (or filtered by role)")
            .WithDescription("Admin-only. Queries the member DB, sends to each. Supports role filter. Rate limited to 1 call/min.");

        // ── POST /api/email/welcome ───────────────────────────────────────────
        // Manually (re-)send a welcome email for a given address.
        // Useful if the automatic send at signup failed (network blip, SES sandbox, etc.)
        // Body: { email, firstName }
        app.MapPost("/api/email/welcome",
            async (WelcomeEmailRequest req, ISesEmailService email) =>
            {
                if (string.IsNullOrWhiteSpace(req.Email))
                    return Results.Problem(statusCode: 400, title: "Bad request",
                        detail: "Email is required.");

                var ok = await email.SendWelcomeAsync(req.Email, req.FirstName ?? "there");

                return ok
                    ? Results.Ok(new { sent = true, to = req.Email })
                    : Results.Problem(statusCode: 502, title: "Welcome email failed",
                        detail: "SES rejected the message. Check that the address is verified in sandbox mode.");
            })
            .RequireAdmin()
            .RequireRateLimiting("comms")
            .WithSummary("Send (or resend) a welcome email to a member")
            .WithDescription("Admin-only. Useful if the automatic welcome send at signup failed.");
    }
}

// ── Request records ───────────────────────────────────────────────────────────

// /api/email/welcome body
public record WelcomeEmailRequest(
    string  Email,
    string? FirstName = null
);
