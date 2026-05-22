// FileRecord — metadata for every file uploaded through the app.
//
// WHY A SEPARATE TABLE:
//   Every vertical (events, resume review, member profile) needs file uploads.
//   Storing file metadata centrally means one place to audit, one cleanup job,
//   one presigned-URL service. Vertical-specific code just stores a FileRecord.Id FK.
//
// WHAT LIVES HERE VS IN SPACES:
//   The actual file bytes live in DigitalOcean Spaces (S3-compatible blob storage).
//   We only store metadata: the Spaces key (path inside the bucket), CDN URL,
//   MIME type, size, and who uploaded it.
//
// SOFT DELETE:
//   Setting DeletedAt hides the record from all queries (HasQueryFilter in config).
//   The actual file in Spaces is cleaned up by a nightly Quartz.NET job (Phase 2.3).
//   This means we keep the audit trail even for "deleted" files.
public class FileRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Path inside the Spaces bucket, e.g. "resume_review/abc-123/jane_doe.pdf"
    // Format: {purpose}/{uuid}/{sanitized_filename}
    // The UUID prevents collisions; the purpose groups related uploads in Spaces console.
    public string SpacesKey { get; set; } = "";

    // Public CDN URL for serving the file to browsers.
    // Spaces automatically fronts files with a CDN — this is computed from SpacesKey at upload time.
    public string CdnUrl { get; set; } = "";

    // MIME type as reported by the client at upload time, e.g. "application/pdf", "image/jpeg"
    public string MimeType { get; set; } = "";

    // File size in bytes — useful for storage dashboards and enforcing per-user quotas later.
    public long SizeBytes { get; set; }

    // Who uploaded it — null for system-generated files (e.g. auto-generated flyers)
    public Guid? UploadedBy { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Soft delete — set by FileService.DeleteAsync, cleaned up by Phase 2.3 Quartz job
    public DateTime? DeletedAt { get; set; }
}
