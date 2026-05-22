// Roles — the complete list of role strings used in the person_role table.
//
// WHY A STATIC CLASS:
//   The role column in Postgres is a plain string, not an enum. That keeps the
//   DB schema simple and avoids enum migration headaches. But we still want a
//   single source of truth for the valid role names so a typo ("Amdin") doesn't
//   silently fail to grant access. All code uses these constants, never bare strings.
//
// ROLE HIERARCHY (widest to narrowest):
//   super_admin  → full system access, can grant/revoke any role
//   admin        → manages events, members, comms; cannot touch system settings
//   member       → logged-in verified SHPE member; can RSVP, view member portal
//
// A person can hold multiple roles simultaneously. Having super_admin does NOT
// automatically imply admin or member — the bootstrap code explicitly grants all
// three so the bootstrap user shows up in member-only and admin-only queries.
public static class Roles
{
    public const string SuperAdmin = "super_admin";
    public const string Admin      = "admin";
    public const string Member     = "member";
}
