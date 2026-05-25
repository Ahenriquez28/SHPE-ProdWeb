// EventFlyerService — handles all file operations for the EventFlyers/ R2 folder.
//
// ════════════════════════════════════════════════════════════════
// STUB — NOT YET IMPLEMENTED. See EventDocService.cs for a
// complete, working example of this pattern.
// ════════════════════════════════════════════════════════════════
//
// PURPOSE: "flyer" → R2 folder "EventFlyers/"
//
// WHAT THIS IS:
//   Event flyers are promotional images distributed on social media, email, and
//   in the member portal. They are distinct from EventDocs (structured documents)
//   and MeetingDocs (internal officer files).
//
// HOW TO IMPLEMENT (follow EventDocService.cs as your guide):
//
//   1. INTERFACE — copy IEventDocService as your starting point and adapt:
//      - Change GetPresignedUploadUrlAsync to only allow image MIME types
//        (image/jpeg, image/png, image/webp, image/gif).
//      - Change MaxSizeBytes to something lower — 10 MB is reasonable for flyers.
//      - A flyer is often 1:1 with an event, so consider making eventId required
//        rather than optional like it is for docs.
//      - Consider adding a GetFeaturedFlyerAsync(Guid eventId) method that returns
//        the most recently uploaded flyer for a given event. The public event page
//        will need this.
//
//   2. VALIDATION — stricter than EventDocService:
//      private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
//      {
//          "image/jpeg",
//          "image/png",
//          "image/webp",
//          "image/gif",   // consider excluding if you don't want animated flyers
//      };
//      public const long MaxSizeBytes = 10 * 1024 * 1024; // 10 MB
//
//   3. KEY STRUCTURE — suggested:
//      EventFlyers/{eventId}/{uuid}/{filename}
//      This is the same subFolder pattern used in EventDocService.
//
//   4. CDN / PUBLIC ACCESS NOTE:
//      Flyers are typically PUBLIC (shown on the public event page without auth).
//      If you enable public access on the R2 bucket or configure a custom domain,
//      set R2_PUBLIC_URL in config and flyers will get a real public CdnUrl.
//      Without that, you'll need presigned GET URLs (see GetDownloadUrl in IFileService).
//      Talk to Alex before enabling public access — it affects all folders, not just flyers.
//
//   5. IMAGE PROCESSING (OPTIONAL, Phase 2+):
//      Consider adding a resize/compress step after upload. Two options:
//        a. Cloudflare Images (separate CF product) — apply a transformation URL prefix.
//        b. A background Quartz.NET job that uses ImageSharp to create a thumbnail
//           and store it at EventFlyers/{eventId}/{uuid}/thumb_{filename}.
//      This is NOT required for Phase 1 — just something to design for.
//
//   6. ENDPOINTS — create EventFlyerEndpoints.cs (see stub there):
//      POST /api/event-flyers/presigned   → validate image MIME/size, return URL
//      POST /api/event-flyers             → register after upload
//      GET  /api/event-flyers?eventId=... → list flyers for an event
//      GET  /api/event-flyers/{id}/download → presigned GET URL
//      DELETE /api/event-flyers/{id}      → admin only, soft-delete
//
//   7. REGISTER IN Program.cs:
//      builder.Services.AddScoped<IEventFlyerService, EventFlyerService>();
//      app.MapEventFlyerEndpoints();
//
// ── EXAMPLE IMPLEMENTATION SKELETON ─────────────────────────────────────────
//
// public interface IEventFlyerService
// {
//     Task<PresignedUploadResponse> GetPresignedUploadUrlAsync(string filename, string mimeType, Guid eventId);
//     Task<EventFlyerDto> RegisterAsync(string storageKey, string mimeType, long sizeBytes, Guid? uploadedBy, Guid eventId, string? displayName);
//     Task<List<EventFlyerDto>> GetByEventAsync(Guid eventId);
//     Task<EventFlyerDto?> GetFeaturedAsync(Guid eventId);  // most recent flyer for an event
//     string GetDownloadUrl(string storageKey, int expiresInMinutes = 60);
//     Task DeleteAsync(Guid fileId);
// }
//
// public record EventFlyerDto(Guid FileId, string StorageKey, string DisplayName, string MimeType, long SizeBytes, Guid EventId, Guid? UploadedBy, DateTime UploadedAt);
//
// public class EventFlyerService : IEventFlyerService
// {
//     private readonly IFileService _files;
//     private readonly AppDbContext _db;
//
//     private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
//     {
//         "image/jpeg", "image/png", "image/webp", "image/gif",
//     };
//     public const long MaxSizeBytes = 10 * 1024 * 1024;
//
//     public EventFlyerService(IFileService files, AppDbContext db)
//     {
//         _files = files;
//         _db    = db;
//     }
//
//     public async Task<PresignedUploadResponse> GetPresignedUploadUrlAsync(
//         string filename, string mimeType, Guid eventId)
//     {
//         if (!AllowedMimeTypes.Contains(mimeType))
//             throw new InvalidOperationException($"File type '{mimeType}' is not allowed for flyers.");
//         return await _files.GetPresignedUploadUrlAsync(filename, mimeType, "flyer", eventId.ToString());
//     }
//
//     // ... RegisterAsync, GetByEventAsync, GetFeaturedAsync, GetDownloadUrl, DeleteAsync
//     // Follow the same pattern as EventDocService for each method.
// }

// Placeholder so the file compiles cleanly. Remove this class when you implement the service above.
public interface IEventFlyerService { }
