// MeetingDocService — handles all file operations for the MeetingDocs/ R2 folder.
//
// ════════════════════════════════════════════════════════════════
// STUB — NOT YET IMPLEMENTED. See EventDocService.cs for a
// complete, working example of this pattern.
// ════════════════════════════════════════════════════════════════
//
// PURPOSE: "meeting_doc" → R2 folder "MeetingDocs/"
//
// WHAT THIS IS:
//   MeetingDocs are internal officer files: e-board meeting agendas, minutes,
//   budget spreadsheets, officer reports, etc. They are NOT public — only admins
//   and super_admins should be able to upload, view, or download them.
//   This is different from EventDocs (visible to all members) and EventFlyers (public).
//
// HOW TO IMPLEMENT (follow EventDocService.cs as your guide):
//
//   1. INTERFACE — copy IEventDocService as your starting point and adapt:
//      - All endpoints must be admin-only, not just member-only.
//      - The "eventId" equivalent here is "meetingId" (a date or a GUID for the meeting).
//        You may not have a Meeting entity yet — if so, use the same prefix-scan
//        workaround that EventDocService uses for GetByEventAsync.
//      - Consider adding a GetByDateRangeAsync(DateTime start, DateTime end) since
//        officers often need "all docs from the last semester."
//
//   2. VALIDATION — similar to EventDocService but can be more permissive:
//      private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
//      {
//          "application/pdf",
//          "application/msword",
//          "application/vnd.openxmlformats-officedocument.wordprocessingml.document",     // .docx
//          "application/vnd.ms-powerpoint",
//          "application/vnd.openxmlformats-officedocument.presentationml.presentation",   // .pptx
//          "application/vnd.ms-excel",
//          "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",           // .xlsx
//          "text/plain",
//          "text/csv",
//          "image/jpeg",   // allow photos of whiteboards etc. in meeting docs
//          "image/png",
//      };
//      public const long MaxSizeBytes = 100 * 1024 * 1024; // 100 MB — larger for budget files
//
//   3. KEY STRUCTURE — suggested:
//      MeetingDocs/{meetingId_or_date}/{uuid}/{filename}
//      Example: MeetingDocs/2026-09-15/{uuid}/agenda.pdf
//      Using a date string makes the R2 console browsable without a DB lookup.
//
//   4. ACCESS CONTROL NOTE:
//      MeetingDocs should NEVER be publicly readable. Even with a presigned URL,
//      be conservative with expiry times (5 minutes max). Budget spreadsheets and
//      officer personal notes can be sensitive.
//      In your endpoints, use .RequireAdmin() — do NOT use .RequireMember().
//
//   5. DOWNLOAD PATTERN:
//      Because these are private, all access goes through presigned GET URLs.
//      Don't try to set R2_PUBLIC_URL for this folder. The GetDownloadUrl method
//      on IFileService generates short-lived URLs — use 5-minute expiry here:
//        GetDownloadUrl(storageKey, expiresInMinutes: 5)
//
//   6. ENDPOINTS — create MeetingDocEndpoints.cs (see stub there):
//      POST /api/meeting-docs/presigned   → admin only, validate MIME/size, return URL
//      POST /api/meeting-docs             → admin only, register after upload
//      GET  /api/meeting-docs?meetingId=... → admin only, list docs for a meeting
//      GET  /api/meeting-docs/{id}/download → admin only, 5-min presigned GET URL
//      DELETE /api/meeting-docs/{id}      → admin only, soft-delete
//
//   7. REGISTER IN Program.cs:
//      builder.Services.AddScoped<IMeetingDocService, MeetingDocService>();
//      app.MapMeetingDocEndpoints();
//
// ── EXAMPLE IMPLEMENTATION SKELETON ─────────────────────────────────────────
//
// public interface IMeetingDocService
// {
//     Task<PresignedUploadResponse> GetPresignedUploadUrlAsync(string filename, string mimeType, string meetingId);
//     Task<MeetingDocDto> RegisterAsync(string storageKey, string mimeType, long sizeBytes, Guid? uploadedBy, string meetingId, string? displayName);
//     Task<List<MeetingDocDto>> GetByMeetingAsync(string meetingId);
//     Task<List<MeetingDocDto>> GetByDateRangeAsync(DateTime start, DateTime end);
//     string GetDownloadUrl(string storageKey, int expiresInMinutes = 5);  // short window!
//     Task DeleteAsync(Guid fileId);
// }
//
// public record MeetingDocDto(Guid FileId, string StorageKey, string DisplayName, string MimeType, long SizeBytes, string MeetingId, Guid? UploadedBy, DateTime UploadedAt);
//
// public class MeetingDocService : IMeetingDocService
// {
//     private readonly IFileService _files;
//     private readonly AppDbContext _db;
//
//     private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
//     {
//         "application/pdf", "application/msword",
//         "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
//         "application/vnd.ms-powerpoint",
//         "application/vnd.openxmlformats-officedocument.presentationml.presentation",
//         "application/vnd.ms-excel",
//         "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
//         "text/plain", "text/csv", "image/jpeg", "image/png",
//     };
//     public const long MaxSizeBytes = 100 * 1024 * 1024;
//
//     public MeetingDocService(IFileService files, AppDbContext db)
//     {
//         _files = files;
//         _db    = db;
//     }
//
//     public async Task<PresignedUploadResponse> GetPresignedUploadUrlAsync(
//         string filename, string mimeType, string meetingId)
//     {
//         if (!AllowedMimeTypes.Contains(mimeType))
//             throw new InvalidOperationException($"File type '{mimeType}' is not allowed for meeting docs.");
//         return await _files.GetPresignedUploadUrlAsync(filename, mimeType, "meeting_doc", meetingId);
//     }
//
//     // ... RegisterAsync, GetByMeetingAsync, GetByDateRangeAsync, GetDownloadUrl, DeleteAsync
//     // Follow the same pattern as EventDocService for each method.
// }

// Placeholder so the file compiles cleanly. Remove this class when you implement the service above.
public interface IMeetingDocService { }
