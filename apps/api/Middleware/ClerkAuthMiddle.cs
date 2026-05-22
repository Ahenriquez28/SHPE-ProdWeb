using Microsoft.EntityFrameworkCore;

// This middleware runs on every single request before it hits any endpoint
// It reads the Authorization header, verifies the Clerk JWT,
// looks up the Person in our database, and attaches their ID + roles to the request
// Endpoints then read ctx.GetPersonId() and ctx.HasRole("admin") without doing any auth work themselves
public class ClerkAuthMiddleware
{
    private readonly RequestDelegate _next;

    public ClerkAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext ctx,
        IClerkJwtVerifier verifier,
        AppDbContext db)
    {
        var auth = ctx.Request.Headers.Authorization.FirstOrDefault();

        // Only process requests that have a Bearer token
        if (auth?.StartsWith("Bearer ") == true)
        {
            var token = auth.Substring("Bearer ".Length);

            // Verify the JWT with Clerk's public keys
            var principal = await verifier.VerifyAsync(token, ctx.RequestAborted);

            if (principal != null)
            {
                // Look up the Person in our database via their Clerk user ID
                var account = await db.AuthAccounts
                    .Where(a => a.Provider == "clerk" &&
                                a.ProviderUserId == principal.ClerkUserId)
                    .FirstOrDefaultAsync();

                if (account != null)
                {
                    // Load their roles so endpoints can check permissions
                    var roles = await db.PersonRoles
                        .Where(r => r.PersonId == account.PersonId)
                        .Select(r => r.Role)
                        .ToListAsync();

                    // Attach identity to the request context
                    // Endpoints read these via the HttpContextExtensions below
                    ctx.Items["PersonId"] = account.PersonId;
                    ctx.Items["Roles"] = roles;
                    ctx.Items["Email"] = principal.Email;
                }
            }
        }

        // Always continue to the next middleware/endpoint regardless of auth result
        // Endpoints that require auth use RequireRoleFilter to enforce it
        await _next(ctx);
    }
}

// Extension methods so endpoints can cleanly read auth info
// Usage: ctx.GetPersonId(), ctx.HasRole("admin")
public static class HttpContextExtensions
{
    // Returns the logged-in person's ID, or null if not authenticated
    public static Guid? GetPersonId(this HttpContext ctx) =>
        ctx.Items["PersonId"] as Guid?;

    // Returns all roles the logged-in person has
    public static List<string> GetRoles(this HttpContext ctx) =>
        (ctx.Items["Roles"] as List<string>) ?? [];

    // Returns true if the logged-in person has the specified role
    public static bool HasRole(this HttpContext ctx, string role) =>
        ctx.GetRoles().Contains(role);
}