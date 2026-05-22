// RequireRoleFilter — extension methods that attach role enforcement to Minimal API endpoints.
//
// WHY THIS EXISTS:
//   Rule 6 in CLAUDE.md forbids inline role checks inside handlers.
//   Instead, every protected endpoint gets a declarative extension:
//     app.MapPost("/api/events", handler).RequireAdmin();
//   This makes permission requirements visible at the route level, not buried in handler logic.
//
// ROLE HIERARCHY (wider roles include narrower ones):
//   super_admin → admin → member
//   RequireMember() allows member | admin | super_admin
//   RequireAdmin()  allows admin | super_admin
//   RequireSuperAdmin() allows super_admin only
//
// HOW IT WORKS:
//   AddEndpointFilter registers a lambda that runs BEFORE the handler on every request.
//   If the person lacks the required role, we short-circuit with a 401/403.
//   The handler never runs — no DB queries, no business logic.
//
// 401 vs 403:
//   401 = not authenticated (no PersonId on context — token was missing or invalid)
//   403 = authenticated but insufficient role
public static class RequireRoleExtensions
{
    // Requires the person to have at least member-level access.
    // Use on endpoints any logged-in SHPE member can reach (their own profile, RSVP, etc.)
    public static RouteHandlerBuilder RequireMember(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(async (ctx, next) =>
        {
            var http = ctx.HttpContext;

            if (http.GetPersonId() == null)
                return Results.Problem(
                    statusCode: 401,
                    title: "Authentication required",
                    detail: "You must be signed in to access this resource.");

            var hasAccess = http.HasRole(Roles.Member)
                         || http.HasRole(Roles.Admin)
                         || http.HasRole(Roles.SuperAdmin);

            if (!hasAccess)
                return Results.Problem(
                    statusCode: 403,
                    title: "Forbidden",
                    detail: "Member role required.");

            return await next(ctx);
        });

    // Requires admin or super_admin.
    // Use on endpoints that manage events, members, and comms but not system settings.
    public static RouteHandlerBuilder RequireAdmin(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(async (ctx, next) =>
        {
            var http = ctx.HttpContext;

            if (http.GetPersonId() == null)
                return Results.Problem(
                    statusCode: 401,
                    title: "Authentication required",
                    detail: "You must be signed in to access this resource.");

            var hasAccess = http.HasRole(Roles.Admin)
                         || http.HasRole(Roles.SuperAdmin);

            if (!hasAccess)
                return Results.Problem(
                    statusCode: 403,
                    title: "Forbidden",
                    detail: "Admin role required.");

            return await next(ctx);
        });

    // Requires super_admin only.
    // Use on system-level endpoints: role management, bootstrap actions, config changes.
    public static RouteHandlerBuilder RequireSuperAdmin(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(async (ctx, next) =>
        {
            var http = ctx.HttpContext;

            if (http.GetPersonId() == null)
                return Results.Problem(
                    statusCode: 401,
                    title: "Authentication required",
                    detail: "You must be signed in to access this resource.");

            if (!http.HasRole(Roles.SuperAdmin))
                return Results.Problem(
                    statusCode: 403,
                    title: "Forbidden",
                    detail: "Super-admin role required.");

            return await next(ctx);
        });
}
