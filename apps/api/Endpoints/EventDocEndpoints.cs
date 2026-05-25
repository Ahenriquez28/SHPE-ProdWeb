// EventDocEndpoints — REST surface for the EventDocs/ R2 folder.
//
// All endpoints delegate to IEventDocService for business logic.
// Only the route mapping, request parsing, and HTTP response shaping live here.
//
// ROUTE GROUP: /api/event-docs
//   POST   /api/event-docs/presigned          → Step 1: presigned PUT URL (admin)
//   POST   /api/event-docs                    → Step 3: register after upload (admin)
//   GET    /api/event-docs?eventId={id}       → list docs for an event (member)
//   GET    /api/event-docs/{id}               → single doc metadata (member)
//   GET    /api/event-docs/{id}/download      → short-lived GET URL (member)
//   DELETE /api/event-docs/{id}               → soft-delete (admin)
//
// RATE LIMITING:
//   Presigned + register use the "default" policy (300/min per user).
//   The actual upload goes to R2 directly — no API rate limit needed there.
//
// ACCESS CONTROL:
//   Uploads and deletes are admin-only (.RequireAdmin()).
//   Reads and downloads are member-only (.RequireMember()).
//   The frontend enforces role-based UI on top of this.

public static class EventDocEndpoints
{
    public static WebApplication MapEventDocEndpoints(this WebApplication app)
    {
        // ── POST /api/event-docs/presigned ────────────────────────────────────────
        // Step 1 of the upload flow. Returns a signed R2 PUT URL valid for 15 min.
        //
        // REQUEST BODY:
        //   {
        //     "filename":  "agenda.pdf",
        //     "mimeType":  "application/pdf",
        //     "eventId":   "3fa85f64-5717-4562-b3fc-2c963f66afa6"   ← optional
        //   }
        //
        // RESPONSE 200:
        //   {
        //     "uploadUrl":        "https://...r2.cloudflarestorage.com/...",
        //     "storageKey":       "EventDocs/{eventId?}/{uuid}/agenda.pdf",
        //     "expiresInSeconds": 900
        //   }
        app.MapPost("/api/event-docs/presigned", async (
            EventDocPresignedRequest req,
            IEventDocService docs) =>
        {
            try
            {
                var result = await docs.GetPresignedUploadUrlAsync(
                    req.Filename, req.MimeType, req.EventId);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { title = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { title = ex.Message });
            }
        })
        .RequireAdmin()
        .WithSummary("Get a presigned R2 PUT URL for uploading an event document")
        .WithTags("EventDocs");

        // ── POST /api/event-docs ──────────────────────────────────────────────────
        // Step 3 of the upload flow. Call this after the browser has PUT the file to R2.
        //
        // REQUEST BODY:
        //   {
        //     "storageKey":  "EventDocs/{eventId?}/{uuid}/agenda.pdf",
        //     "mimeType":    "application/pdf",
        //     "sizeBytes":   204800,
        //     "eventId":     "3fa85f64-...",    ← optional, must match the key path
        //     "displayName": "Fall 2026 Kickoff Agenda"  ← optional, defaults to filename
        //   }
        //
        // RESPONSE 201 Created:
        //   { "fileId": "...", "storageKey": "...", "displayName": "...", "mimeType": "...", ... }
        app.MapPost("/api/event-docs", async (
            EventDocRegisterRequest req,
            IEventDocService docs,
            HttpContext ctx) =>
        {
            try
            {
                var doc = await docs.RegisterAsync(
                    req.StorageKey, req.MimeType, req.SizeBytes,
                    ctx.GetPersonId(), req.EventId, req.DisplayName);

                return Results.Created($"/api/event-docs/{doc.FileId}", doc);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { title = ex.Message });
            }
        })
        .RequireAdmin()
        .WithSummary("Register an event document after uploading it to R2")
        .WithTags("EventDocs");

        // ── GET /api/event-docs?eventId={id} ─────────────────────────────────────
        // Lists all documents for a specific event.
        // eventId is required — listing all docs across all events is not supported
        // without pagination (and the UI doesn't need it).
        //
        // RESPONSE 200: array of EventDocDto
        app.MapGet("/api/event-docs", async (
            Guid? eventId,
            IEventDocService docs) =>
        {
            if (eventId is null)
                return Results.BadRequest(new { title = "eventId query param is required." });

            var list = await docs.GetByEventAsync(eventId.Value);
            return Results.Ok(list);
        })
        .RequireMember()
        .WithSummary("List all documents for an event")
        .WithTags("EventDocs");

        // ── GET /api/event-docs/{id} ──────────────────────────────────────────────
        // Returns metadata for a single document by FileRecord.Id.
        // Does NOT include a download URL — call /api/event-docs/{id}/download for that.
        //
        // RESPONSE 200: EventDocDto
        // RESPONSE 404: document not found or soft-deleted
        app.MapGet("/api/event-docs/{id:guid}", async (
            Guid id,
            IEventDocService docs) =>
        {
            var doc = await docs.GetByIdAsync(id);
            return doc is null ? Results.NotFound() : Results.Ok(doc);
        })
        .RequireMember()
        .WithSummary("Get event document metadata by ID")
        .WithTags("EventDocs");

        // ── GET /api/event-docs/{id}/download ────────────────────────────────────
        // Returns a short-lived presigned R2 GET URL for the browser to download the file.
        //
        // WHY A SEPARATE ENDPOINT:
        //   Generating a presigned URL is not free — it consumes signing keys.
        //   More importantly, it gives us a place to enforce access control per-download
        //   (e.g. log who downloaded what, check if the event is published, etc.).
        //   Presigned GET URLs expire after 15 minutes; the browser must request a fresh
        //   one each time (it doesn't need to re-authenticate to R2 directly).
        //
        // RESPONSE 200: { "downloadUrl": "https://...", "expiresInSeconds": 900 }
        // RESPONSE 404: document not found
        app.MapGet("/api/event-docs/{id:guid}/download", async (
            Guid id,
            IEventDocService docs) =>
        {
            var doc = await docs.GetByIdAsync(id);
            if (doc is null) return Results.NotFound();

            var url = docs.GetDownloadUrl(doc.StorageKey, expiresInMinutes: 15);
            return Results.Ok(new { downloadUrl = url, expiresInSeconds = 900 });
        })
        .RequireMember()
        .WithSummary("Get a presigned R2 download URL for an event document")
        .WithTags("EventDocs");

        // ── DELETE /api/event-docs/{id} ───────────────────────────────────────────
        // Soft-deletes the FileRecord. The R2 object is cleaned up by the Phase 2.3 Quartz job.
        //
        // RESPONSE 204 No Content: success
        // RESPONSE 404: document not found
        app.MapDelete("/api/event-docs/{id:guid}", async (
            Guid id,
            IEventDocService docs) =>
        {
            await docs.DeleteAsync(id);
            return Results.NoContent();
        })
        .RequireAdmin()
        .WithSummary("Soft-delete an event document")
        .WithTags("EventDocs");

        return app;
    }
}

// ── Request records ───────────────────────────────────────────────────────────

// Step 1 request body
public record EventDocPresignedRequest(
    string Filename,    // e.g. "agenda.pdf"
    string MimeType,    // e.g. "application/pdf"
    Guid?  EventId      // optional — links the upload to a specific event
);

// Step 3 request body
public record EventDocRegisterRequest(
    string  StorageKey,   // the key returned by Step 1
    string  MimeType,     // MIME type confirmed after upload
    long    SizeBytes,    // actual file size in bytes
    Guid?   EventId,      // optional — same eventId as Step 1
    string? DisplayName   // optional human-readable name for the UI
);
