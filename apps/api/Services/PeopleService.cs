using Microsoft.EntityFrameworkCore;

// PeopleService — business logic around Person records.
//
// WHY A SERVICE LAYER:
//   Endpoints are intentionally thin: they validate input, call a service method,
//   and return a response. All the "should this be allowed? what else needs to
//   change?" logic lives here, not scattered across endpoint handlers.
//   This makes each piece testable in isolation and readable without scrolling
//   through HTTP plumbing.
//
// WHAT THIS SERVICE HANDLES (now and later):
//   - Bootstrap super-admin on first signup        ← implemented here
//   - Profile updates, major/grad-year edits       ← added in Phase 1.4
//   - Merging duplicate Person records             ← added in Phase 2.1 (GSU sync)
//
// INTERFACE + IMPLEMENTATION PATTERN:
//   We always define an interface (IPeopleService) and register the concrete class
//   against it. Endpoints depend on IPeopleService, never PeopleService directly.
//   This lets us swap in a mock for tests without touching production code.
public interface IPeopleService
{
    // Called immediately after a new Person row is inserted during signup.
    //
    // Grants super_admin + admin + member to the first signup whose email
    // matches BOOTSTRAP_SUPER_ADMIN_EMAIL — but ONLY if no super_admin exists yet.
    // Idempotent: safe to call on every signup; after bootstrap fires once it
    // checks the DB and returns early on every subsequent call.
    Task CheckBootstrapSuperAdminAsync(Person newPerson);
}

public class PeopleService : IPeopleService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    // Both dependencies are injected by ASP.NET's DI container.
    // AppDbContext is Scoped (one per request); IConfiguration is Singleton.
    public PeopleService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task CheckBootstrapSuperAdminAsync(Person newPerson)
    {
        // ── Guard 1: feature must be explicitly enabled ───────────────────────
        // If the env var isn't set, we skip entirely. This prevents accidental
        // bootstrapping on staging or prod where the var was forgotten.
        var bootstrapEmail = _config["BOOTSTRAP_SUPER_ADMIN_EMAIL"];
        if (string.IsNullOrWhiteSpace(bootstrapEmail)) return;

        // ── Guard 2: idempotency check ────────────────────────────────────────
        // Once a super_admin row exists in the DB, this feature is permanently
        // disabled — even if the env var still matches. This prevents a second
        // "super_admin" from being created if the original admin's account is
        // deleted and then someone signs up with the same email again.
        var superAdminExists = await _db.PersonRoles
            .AnyAsync(r => r.Role == Roles.SuperAdmin);
        if (superAdminExists) return;

        // ── Guard 3: email must match the configured address ──────────────────
        // OrdinalIgnoreCase so "Alex@GSU.edu" matches "alex@gsu.edu".
        // We check both the primary email and the GSU email (in case someone
        // signs up with their GSU address first).
        var emailMatches =
            string.Equals(newPerson.Email, bootstrapEmail, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(newPerson.GsuEmail, bootstrapEmail, StringComparison.OrdinalIgnoreCase);

        if (!emailMatches) return;

        // ── Grant the three foundational roles ────────────────────────────────
        // We add super_admin + admin + member explicitly rather than implying
        // lower roles from super_admin, because:
        //   - Member-only queries use WHERE role = 'member' — they won't return
        //     this person without the explicit member row
        //   - Same pattern for admin-only queries
        //   - Future devs shouldn't need to know about implicit role hierarchies
        _db.PersonRoles.AddRange(
            new PersonRole { Id = Guid.NewGuid(), PersonId = newPerson.Id, Role = Roles.SuperAdmin },
            new PersonRole { Id = Guid.NewGuid(), PersonId = newPerson.Id, Role = Roles.Admin },
            new PersonRole { Id = Guid.NewGuid(), PersonId = newPerson.Id, Role = Roles.Member }
        );

        // ── Write a specific audit log entry ─────────────────────────────────
        // The generic middleware logs "POST /api/auth/signup" — that's not enough
        // context for an auditor to understand what happened. We write a richer
        // entry directly from the service so the audit trail shows the real reason.
        _db.AuditLogs.Add(new AuditLog
        {
            Id           = Guid.NewGuid(),
            ActorPersonId = newPerson.Id,
            Action       = "BOOTSTRAP_SUPER_ADMIN",
            TargetType   = "person",
            TargetId     = newPerson.Id,
            Details      = $"Bootstrap super-admin granted to {newPerson.Email ?? newPerson.GsuEmail}",
            At           = DateTime.UtcNow,
        });

        // Single SaveChanges for the roles + audit log — they succeed or fail together.
        await _db.SaveChangesAsync();
    }
}
