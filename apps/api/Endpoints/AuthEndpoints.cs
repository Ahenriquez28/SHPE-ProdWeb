using Microsoft.EntityFrameworkCore;

/*
 * AuthEndpoints — bridges Clerk (our auth provider) with our Person table.
 *
 * WHY THIS EXISTS:
 *   Clerk manages passwords, OAuth, MFA — but knows nothing about our member data
 *   (roles, grad year, GSU email, etc.). We store that in `person` and `person_role`.
 *   On first sign-in, we link the Clerk user ID → Person row so every subsequent
 *   request can resolve identity without touching Clerk's API again.
 *
 * POST /api/auth/sync
 *   First call:  creates the AuthAccount link (and Person if brand-new user).
 *   Later calls: returns existing profile immediately — safe to call on every sign-in.
 *
 * GET /api/me
 *   Returns the caller's profile + roles. Frontend uses this for sidebar + role checks.
 */
public static class AuthEndpoints
{
    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        // ── POST /api/auth/sync ───────────────────────────────────────────────────
        // Rate-limited at 5/min per IP (auth policy) to prevent bulk account creation.
        app.MapPost("/api/auth/sync", async (
            HttpContext ctx,
            AppDbContext db,
            SyncRequest req,
            CancellationToken ct) =>
        {
            // ClerkUserId is set by ClerkAuthMiddleware whenever a valid JWT is present.
            var clerkUserId = ctx.GetClerkUserId();
            if (clerkUserId is null)
                return Results.Unauthorized();

            // ── Already linked? Return immediately (idempotent) ───────────────────
            var account = await db.AuthAccounts
                .Where(a => a.Provider == "clerk" && a.ProviderUserId == clerkUserId)
                .FirstOrDefaultAsync(ct);

            if (account != null)
            {
                var existing      = await db.People.FindAsync([account.PersonId], ct);
                var existingRoles = await db.PersonRoles
                    .Where(r => r.PersonId == account.PersonId)
                    .Select(r => r.Role)
                    .ToListAsync(ct);
                return Results.Ok(Map(existing!, existingRoles));
            }

            // ── Not linked yet — find or create Person ────────────────────────────
            // Email comes from Clerk's user object (frontend sends it in the request body)
            // because Clerk JWTs don't include the email claim by default.
            var email = req.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
                return Results.BadRequest(new { title = "Email is required on first sign-in." });

            // Match on personal email OR GSU email so pre-seeded members (e.g. admin
            // accounts created before Clerk launch) are found and linked automatically.
            var person = await db.People
                .Where(p => p.Email == email || p.GsuEmail == email)
                .FirstOrDefaultAsync(ct);

            if (person is null)
            {
                // Brand-new user — create a bare-bones Person and give them the member role.
                // They complete their profile (grad year, GSU email, etc.) on the Profile page.
                person = new Person
                {
                    Id        = Guid.NewGuid(),
                    FullName  = req.FullName ?? email,
                    Email     = email,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                db.People.Add(person);

                db.PersonRoles.Add(new PersonRole
                {
                    Id         = Guid.NewGuid(),
                    PersonId   = person.Id,
                    Role       = "member",
                    AssignedAt = DateTime.UtcNow,
                });
            }

            // Create the AuthAccount row that links Clerk user ID ↔ Person ID.
            // This is what ClerkAuthMiddleware looks up on every subsequent request.
            db.AuthAccounts.Add(new AuthAccount
            {
                Id             = Guid.NewGuid(),
                PersonId       = person.Id,
                Provider       = "clerk",
                ProviderUserId = clerkUserId,
                EmailVerified  = true,
                CreatedAt      = DateTime.UtcNow,
            });

            await db.SaveChangesAsync(ct);

            var roles = await db.PersonRoles
                .Where(r => r.PersonId == person.Id)
                .Select(r => r.Role)
                .ToListAsync(ct);

            return Results.Ok(Map(person, roles));
        })
        .RequireRateLimiting("auth")
        .WithName("AuthSync");

        // ── GET /api/me ───────────────────────────────────────────────────────────
        app.MapGet("/api/me", async (HttpContext ctx, AppDbContext db, CancellationToken ct) =>
        {
            var personId = ctx.GetPersonId();
            if (personId is null) return Results.Unauthorized();

            var person = await db.People.FindAsync([personId.Value], ct);
            if (person is null) return Results.NotFound();

            var roles = await db.PersonRoles
                .Where(r => r.PersonId == person.Id)
                .Select(r => r.Role)
                .ToListAsync(ct);

            return Results.Ok(Map(person, roles));
        })
        .WithName("GetMe");

        return app;
    }

    // Response shape matches the Person type in apps/web/src/types/api.ts.
    // ASP.NET's default JSON policy serializes these as camelCase automatically.
    private static object Map(Person p, List<string> roles) => new
    {
        id          = p.Id,
        fullName    = p.FullName,
        email       = p.Email,
        gsuEmail    = p.GsuEmail,
        phone       = p.PhoneNumber,
        gradYear    = p.GradYear,
        linkedinUrl = p.LinkedinUrl,
        roles,
        shareContact = p.ShareContact,
        smsOptIn     = p.SmsOptIn,
        photoOptOut  = p.PhotoOptOut,
        createdAt    = p.CreatedAt,
    };
}

// Body for POST /api/auth/sync.
// Email comes from Clerk's useUser() hook on the frontend — NOT from the JWT claim.
public record SyncRequest(string? Email, string? FullName);
