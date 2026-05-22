// ProblemDetailsResults — static helpers for returning RFC 7807 error shapes from endpoints.
//
// WHY RFC 7807:
//   Without a standard, every endpoint invents its own error format.
//   The frontend ends up with error-handling logic scattered everywhere.
//   RFC 7807 gives us one JSON shape for every error:
//     { "type": "...", "title": "...", "status": 400, "detail": "...", "traceId": "..." }
//
// USAGE IN ENDPOINTS:
//   return ProblemDetailsResults.NotFound("No event with that ID.");
//   return ProblemDetailsResults.Conflict("That email is already registered.");
//
// THE "type" URL:
//   RFC 7807 says the type field SHOULD resolve to a human-readable page.
//   We use shpegsu.com/errors/<slug> as a stable opaque identifier.
//   We don't actually have to host those pages — the frontend treats it as a key, not a URL.
//   It's how the frontend can distinguish "conflict" from "not-found" without parsing the message.
//
// WHERE traceId COMES FROM:
//   It's automatically injected by the CustomizeProblemDetails delegate in Program.cs.
//   Every error response has the trace ID so users can paste it in bug reports.
public static class ProblemDetailsResults
{
    private const string TypeBase = "https://shpegsu.com/errors/";

    // 400 — malformed request or failed business rule (not a validation schema error)
    public static IResult BadRequest(string detail) =>
        Results.Problem(
            type:       TypeBase + "bad-request",
            title:      "Bad request",
            detail:     detail,
            statusCode: 400);

    // 400 — field-level validation failures, e.g. missing required fields
    // errors: { "email": ["Email is required", "Must be a valid email"] }
    public static IResult ValidationFailed(IDictionary<string, string[]> errors) =>
        Results.ValidationProblem(
            errors,
            type:  TypeBase + "validation",
            title: "Validation failed");

    // 404 — resource doesn't exist (or was soft-deleted)
    public static IResult NotFound(string detail) =>
        Results.Problem(
            type:       TypeBase + "not-found",
            title:      "Not found",
            detail:     detail,
            statusCode: 404);

    // 409 — state conflict, e.g. duplicate email, already checked in, already RSVP'd
    public static IResult Conflict(string detail) =>
        Results.Problem(
            type:       TypeBase + "conflict",
            title:      "Conflict",
            detail:     detail,
            statusCode: 409);

    // 502 — an upstream service (GSU API, Telnyx, SES) returned an error or timed out
    // upstream: short name of the service ("gsu-api", "telnyx", "ses")
    // fallback: what we're doing instead ("queued for retry", "skipping SMS", etc.)
    public static IResult UpstreamFailure(string upstream, string fallback) =>
        Results.Problem(
            type:       TypeBase + $"upstream-{upstream}",
            title:      $"{upstream} unreachable",
            detail:     $"Falling back to: {fallback}",
            statusCode: 502);
}
