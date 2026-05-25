// EventFlyerEndpoints — REST surface for the EventFlyers/ R2 folder.
//
// ════════════════════════════════════════════════════════════════
// STUB — NOT YET IMPLEMENTED. See EventDocEndpoints.cs for a
// complete, working example of this pattern.
// ════════════════════════════════════════════════════════════════
//
// ROUTE GROUP: /api/event-flyers
//   POST   /api/event-flyers/presigned      → Step 1: presigned PUT URL (admin)
//   POST   /api/event-flyers                → Step 3: register after upload (admin)
//   GET    /api/event-flyers?eventId={id}   → list flyers for an event (member/public)
//   GET    /api/event-flyers/{id}           → single flyer metadata (member/public)
//   GET    /api/event-flyers/{id}/download  → GET URL (member/public, if private R2)
//   DELETE /api/event-flyers/{id}           → soft-delete (admin)
//
// HOW TO IMPLEMENT:
//   1. Implement IEventFlyerService in EventFlyerService.cs (skeleton is there).
//   2. Copy EventDocEndpoints.cs and adapt for flyers:
//      - Replace IEventDocService → IEventFlyerService
//      - Replace EventDocDto     → EventFlyerDto
//      - Replace "EventDocs"     → "EventFlyers" in error messages and summaries
//      - Upload endpoint should make eventId REQUIRED (not optional) since every
//        flyer belongs to an event.
//      - If R2 bucket has public access configured (R2_PUBLIC_URL set in config),
//        the GET /list and GET /{id} responses can include a public URL directly.
//        The /download endpoint may not be needed at all for public flyers.
//      - If flyers are kept private, keep the /download endpoint and use a longer
//        expiry than EventDocs (60 min is fine for flyers — they're not sensitive).
//   3. Register in Program.cs:
//      builder.Services.AddScoped<IEventFlyerService, EventFlyerService>();
//      app.MapEventFlyerEndpoints();
//
// ACCESS CONTROL NOTE:
//   Flyers are promotional content. Once an event is published, its flyer should
//   be readable by unauthenticated users (the public site shows event cards with flyers).
//   That means the GET endpoints may eventually need to drop .RequireMember() in favour
//   of no auth requirement at all. Coordinate with the public Events page dev.
//
// EXAMPLE ENDPOINT SKELETON:
//
// public static class EventFlyerEndpoints
// {
//     public static WebApplication MapEventFlyerEndpoints(this WebApplication app)
//     {
//         app.MapPost("/api/event-flyers/presigned", async (
//             EventFlyerPresignedRequest req,
//             IEventFlyerService flyers) =>
//         {
//             // validate image MIME type
//             // call flyers.GetPresignedUploadUrlAsync(req.Filename, req.MimeType, req.EventId)
//             // return Results.Ok(result)
//         })
//         .RequireAdmin()
//         .WithSummary("Get a presigned R2 PUT URL for uploading an event flyer")
//         .WithTags("EventFlyers");
//
//         app.MapPost("/api/event-flyers", async (...) => { ... })
//             .RequireAdmin().WithTags("EventFlyers");
//
//         app.MapGet("/api/event-flyers", async (...) => { ... })
//             .RequireMember().WithTags("EventFlyers"); // or no auth if public
//
//         app.MapGet("/api/event-flyers/{id:guid}", async (...) => { ... })
//             .RequireMember().WithTags("EventFlyers");
//
//         app.MapGet("/api/event-flyers/{id:guid}/download", async (...) => { ... })
//             .RequireMember().WithTags("EventFlyers"); // only needed if objects are private
//
//         app.MapDelete("/api/event-flyers/{id:guid}", async (...) => { ... })
//             .RequireAdmin().WithTags("EventFlyers");
//
//         return app;
//     }
// }
//
// public record EventFlyerPresignedRequest(string Filename, string MimeType, Guid EventId);
// public record EventFlyerRegisterRequest(string StorageKey, string MimeType, long SizeBytes, Guid EventId, string? DisplayName);
