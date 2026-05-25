// MeetingDocEndpoints — REST surface for the MeetingDocs/ R2 folder.
//
// ════════════════════════════════════════════════════════════════
// STUB — NOT YET IMPLEMENTED. See EventDocEndpoints.cs for a
// complete, working example of this pattern.
// ════════════════════════════════════════════════════════════════
//
// ROUTE GROUP: /api/meeting-docs
//   POST   /api/meeting-docs/presigned       → Step 1: presigned PUT URL (admin)
//   POST   /api/meeting-docs                 → Step 3: register after upload (admin)
//   GET    /api/meeting-docs?meetingId=...   → list docs for a meeting (admin)
//   GET    /api/meeting-docs/{id}            → single doc metadata (admin)
//   GET    /api/meeting-docs/{id}/download   → short-lived GET URL (admin, 5 min!)
//   DELETE /api/meeting-docs/{id}            → soft-delete (admin)
//
// HOW TO IMPLEMENT:
//   1. Implement IMeetingDocService in MeetingDocService.cs (skeleton is there).
//   2. Copy EventDocEndpoints.cs and adapt for meeting docs:
//      - Replace IEventDocService → IMeetingDocService
//      - Replace EventDocDto     → MeetingDocDto
//      - Replace "EventDocs"     → "MeetingDocs" in error messages and summaries
//      - Replace "eventId" query param → "meetingId" (string, e.g. "2026-09-15")
//      - ALL endpoints use .RequireAdmin() — meeting docs are internal only.
//        Members should NEVER see these routes.
//      - The /download endpoint expiry should be SHORT (5 minutes):
//          var url = docs.GetDownloadUrl(doc.StorageKey, expiresInMinutes: 5);
//        Budget spreadsheets and officer notes are sensitive — don't leave URLs
//        alive for an hour.
//   3. Register in Program.cs:
//      builder.Services.AddScoped<IMeetingDocService, MeetingDocService>();
//      app.MapMeetingDocEndpoints();
//
// ADDITIONAL ENDPOINT TO CONSIDER:
//   GET /api/meeting-docs?from=2026-01-01&to=2026-05-31
//   → list all docs across all meetings in a date range
//   → useful for semester audits and officer handoffs
//   → implement GetByDateRangeAsync in MeetingDocService (skeleton is documented there)
//
// ACCESS CONTROL NOTE:
//   Every single endpoint here must use .RequireAdmin().
//   Do NOT use .RequireMember() — regular members should never see meeting budgets,
//   officer evaluations, or internal agendas. This is the key difference from
//   EventDocEndpoints (which uses .RequireMember() for reads).
//
// EXAMPLE ENDPOINT SKELETON:
//
// public static class MeetingDocEndpoints
// {
//     public static WebApplication MapMeetingDocEndpoints(this WebApplication app)
//     {
//         app.MapPost("/api/meeting-docs/presigned", async (
//             MeetingDocPresignedRequest req,
//             IMeetingDocService docs) =>
//         {
//             // validate MIME type against meeting doc allowed list
//             // call docs.GetPresignedUploadUrlAsync(req.Filename, req.MimeType, req.MeetingId)
//             // return Results.Ok(result)
//         })
//         .RequireAdmin()
//         .WithSummary("Get a presigned R2 PUT URL for uploading a meeting document")
//         .WithTags("MeetingDocs");
//
//         app.MapPost("/api/meeting-docs", async (...) => { ... })
//             .RequireAdmin().WithTags("MeetingDocs");
//
//         app.MapGet("/api/meeting-docs", async (...) => { ... })
//             .RequireAdmin().WithTags("MeetingDocs");
//
//         app.MapGet("/api/meeting-docs/{id:guid}", async (...) => { ... })
//             .RequireAdmin().WithTags("MeetingDocs");
//
//         app.MapGet("/api/meeting-docs/{id:guid}/download", async (
//             Guid id, IMeetingDocService docs) =>
//         {
//             var doc = await docs.GetByIdAsync(id);
//             if (doc is null) return Results.NotFound();
//             var url = docs.GetDownloadUrl(doc.StorageKey, expiresInMinutes: 5); // SHORT — sensitive!
//             return Results.Ok(new { downloadUrl = url, expiresInSeconds = 300 });
//         })
//         .RequireAdmin().WithTags("MeetingDocs");
//
//         app.MapDelete("/api/meeting-docs/{id:guid}", async (...) => { ... })
//             .RequireAdmin().WithTags("MeetingDocs");
//
//         return app;
//     }
// }
//
// public record MeetingDocPresignedRequest(string Filename, string MimeType, string MeetingId);
// public record MeetingDocRegisterRequest(string StorageKey, string MimeType, long SizeBytes, string MeetingId, string? DisplayName);
