using Microsoft.EntityFrameworkCore;

// EventDocService — handles all file operations for the EventDocs/ R2 folder.
//
// PURPOSE: "event_doc" → R2 folder "EventDocs/"
//
// WHAT THIS IS:
//   Event documents are structured files that belong to a specific event:
//   agendas, slideshows, meeting minutes, handouts, sign-in sheets, etc.
//   They are distinct from EventFlyers (promotional images — see EventFlyerService)
//   and MeetingDocs (internal officer meeting files — see MeetingDocService).
//
// UPLOAD FLOW (same three-step pattern as the rest of the app):
//   1. Admin calls POST /api/event-docs/presigned with { filename, mimeType, eventId? }
//      → EventDocService validates MIME type + size, returns presigned PUT URL
//   2. Browser PUTs the file bytes directly to R2 (EventDocs/{eventId?}/{uuid}/{filename})
//   3. Admin calls POST /api/event-docs with { storageKey, mimeType, sizeBytes, eventId?, displayName? }
//      → EventDocService writes a FileRecord row and returns metadata
//
// ACCESS CONTROL:
//   Uploading and deleting require admin role.
//   Downloading is member-only — GET /api/event-docs/{id}/download returns a short-lived
//   presigned GET URL so private R2 objects can be served without exposing bucket credentials.
//
// PHASE 1 TODO — Event linkage:
//   When the Event entity is built, add a junction table event_document:
//     event_id    UUID FK → event.id
//     file_id     UUID FK → file_record.id
//   Then replace the eventId-in-path encoding with a proper FK relationship.
//   GetByEventAsync currently queries by storageKey prefix (EventDocs/{eventId}/...) as a
//   workaround until that table exists. This is intentional and documented below.

public interface IEventDocService
{
    // Step 1: validate and get presigned URL for uploading an event doc.
    // eventId (optional): encodes the event association into the R2 key path.
    Task<PresignedUploadResponse> GetPresignedUploadUrlAsync(
        string filename, string mimeType, Guid? eventId);

    // Step 3: register the uploaded doc's metadata in the DB.
    // displayName: human-readable name shown in the UI (defaults to filename if omitted).
    Task<EventDocDto> RegisterAsync(
        string storageKey, string mimeType, long sizeBytes,
        Guid? uploadedBy, Guid? eventId, string? displayName);

    // Returns all docs whose R2 key path starts with EventDocs/{eventId}/.
    // This is a prefix-scan workaround until the event_document junction table exists.
    Task<List<EventDocDto>> GetByEventAsync(Guid eventId);

    // Returns a single doc by FileRecord.Id.
    Task<EventDocDto?> GetByIdAsync(Guid fileId);

    // Generate a short-lived presigned GET URL so the browser can download the file.
    // Default 15 minutes — tight enough to prevent URL forwarding abuse.
    string GetDownloadUrl(string storageKey, int expiresInMinutes = 15);

    // Soft-delete. R2 object cleanup happens in the Phase 2.3 Quartz job.
    Task DeleteAsync(Guid fileId);
}

// DTO returned by all read operations.
// Does NOT include a presigned download URL by default — callers must request it
// explicitly via GetDownloadUrl to avoid generating unnecessary signed URLs.
public record EventDocDto(
    Guid     FileId,
    string   StorageKey,
    string   DisplayName,
    string   MimeType,
    long     SizeBytes,
    Guid?    EventId,       // null if uploaded without an event association
    Guid?    UploadedBy,
    DateTime UploadedAt
);

public class EventDocService : IEventDocService
{
    private readonly IFileService _files;
    private readonly AppDbContext _db;

    // Allowed MIME types for event documents.
    // Intentionally restrictive — no executables, no audio/video.
    // Add entries here if a new document format needs to be supported.
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/msword",                                                          // .doc
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",    // .docx
        "application/vnd.ms-powerpoint",                                              // .ppt
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",  // .pptx
        "application/vnd.ms-excel",                                                   // .xls
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",          // .xlsx
        "text/plain",
        "text/csv",
    };

    // 50 MB hard limit for event documents.
    // Adjust if large slide decks become a common need — but keep it reasonable
    // to protect R2 storage costs and upload time on slow connections.
    public const long MaxSizeBytes = 50 * 1024 * 1024; // 50 MB

    public EventDocService(IFileService files, AppDbContext db)
    {
        _files = files;
        _db    = db;
    }

    public async Task<PresignedUploadResponse> GetPresignedUploadUrlAsync(
        string filename, string mimeType, Guid? eventId)
    {
        // Validate MIME type before generating the URL.
        // This prevents storing garbage in R2 — the bucket only gets real documents.
        if (!AllowedMimeTypes.Contains(mimeType))
            throw new InvalidOperationException(
                $"File type '{mimeType}' is not allowed for event documents. " +
                $"Accepted: PDF, Word, PowerPoint, Excel, plain text, CSV.");

        // eventId as a subFolder groups all docs for one event under a single R2 prefix:
        //   EventDocs/{eventId}/{uuid}/{filename}
        // Without eventId: EventDocs/{uuid}/{filename} (no grouping)
        //
        // This makes it easy to see an event's files in the R2 console, and also enables
        // the GetByEventAsync prefix-scan workaround until the junction table is built.
        var subFolder = eventId?.ToString();

        return await _files.GetPresignedUploadUrlAsync(filename, mimeType, "event_doc", subFolder);
    }

    public async Task<EventDocDto> RegisterAsync(
        string storageKey, string mimeType, long sizeBytes,
        Guid? uploadedBy, Guid? eventId, string? displayName)
    {
        // Enforce size limit at registration time too (the client declares sizeBytes).
        // This is a second line of defence — R2 itself doesn't enforce the limit during
        // the presigned PUT, so we check again here before writing the DB row.
        if (sizeBytes > MaxSizeBytes)
            throw new InvalidOperationException(
                $"File too large ({sizeBytes / 1024 / 1024} MB). Maximum is {MaxSizeBytes / 1024 / 1024} MB.");

        // Verify the key actually belongs to EventDocs/ — prevents someone from passing
        // a key from EventFlyers/ or MeetingDocs/ and registering it as an event doc.
        if (!storageKey.StartsWith("EventDocs/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "storageKey does not belong to the EventDocs folder.");

        var record = await _files.RegisterUploadAsync(storageKey, mimeType, sizeBytes, uploadedBy);

        // Derive displayName from the filename portion of the key if not provided.
        // Key format: EventDocs/{eventId?}/{uuid}/{filename}
        var inferredName = displayName?.Trim().NullIfEmpty()
            ?? storageKey.Split('/').Last();

        return ToDto(record, inferredName, eventId);
    }

    public async Task<List<EventDocDto>> GetByEventAsync(Guid eventId)
    {
        // WORKAROUND: until the event_document junction table exists (Phase 1),
        // we find docs by prefix-matching the storage key.
        // All docs uploaded for an event have keys starting with EventDocs/{eventId}/.
        var prefix = $"EventDocs/{eventId}/";

        var records = await _db.Files
            .Where(f => f.SpacesKey.StartsWith(prefix))
            .OrderByDescending(f => f.UploadedAt)
            .ToListAsync();

        return records
            .Select(r => ToDto(r, r.SpacesKey.Split('/').Last(), eventId))
            .ToList();
    }

    public async Task<EventDocDto?> GetByIdAsync(Guid fileId)
    {
        var record = await _db.Files.FindAsync(fileId);
        if (record is null) return null;

        // Parse eventId from the key path if present.
        // Key: EventDocs/{eventId}/{uuid}/{filename}  OR  EventDocs/{uuid}/{filename}
        Guid? parsedEventId = null;
        var parts = record.SpacesKey.Split('/');
        if (parts.Length >= 4 && Guid.TryParse(parts[1], out var eid))
            parsedEventId = eid;

        return ToDto(record, parts.Last(), parsedEventId);
    }

    public string GetDownloadUrl(string storageKey, int expiresInMinutes = 15) =>
        _files.GetPresignedDownloadUrl(storageKey, expiresInMinutes);

    public async Task DeleteAsync(Guid fileId) =>
        await _files.DeleteAsync(fileId);

    // Maps FileRecord → EventDocDto.
    private static EventDocDto ToDto(FileRecord r, string displayName, Guid? eventId) =>
        new(r.Id, r.SpacesKey, displayName, r.MimeType, r.SizeBytes, eventId, r.UploadedBy, r.UploadedAt);
}
