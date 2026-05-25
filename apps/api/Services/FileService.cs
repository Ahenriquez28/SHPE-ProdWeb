using Amazon.S3;
using Amazon.S3.Model;
using System.Text.RegularExpressions;

// FileService — wraps Cloudflare R2 (S3-compatible) for the three-step upload flow.
//
// WHY THREE STEPS (not just POST the file to the API):
//   Files can be large (event docs, flyers, slideshows). If the browser POSTed bytes
//   to the API, the API would hold that memory, saturate its bandwidth, and become
//   a bottleneck. The three-step flow keeps the API completely out of the data path:
//
//   1. Client → API:  POST /api/files/presigned  → signed PUT URL (valid 15 min)
//   2. Client → R2:   PUT <bytes> to signed URL  → file lands directly in R2
//   3. Client → API:  POST /api/files             → write metadata row to DB
//
//   The API only sees small JSON payloads, never the file bytes.
//
// R2 COMPATIBILITY:
//   Cloudflare R2 is S3-compatible. We configure the AWS SDK client with the R2
//   endpoint URL instead of aws.amazon.com. Everything else — presigned URLs, PUT,
//   GET, DELETE — works identically because R2 implements the S3 API spec.
//   See Program.cs for client registration.
//
// BUCKET FOLDER STRUCTURE (pre-created in R2 console):
//   EventFlyers/   ← purpose "flyer"       → EventFlyerService
//   EventDocs/     ← purpose "event_doc"   → EventDocService    (fully implemented)
//   MeetingDocs/   ← purpose "meeting_doc" → MeetingDocService
//
// PURPOSE → FOLDER MAPPING:
//   The purpose string (e.g. "event_doc") is a logical name used throughout the app.
//   It is mapped to a physical R2 folder via PurposeFolderMap below.
//   Never allow arbitrary purpose strings from the client — only map known values.

public interface IFileService
{
    // Step 1: generate a presigned PUT URL the browser uploads to directly.
    // mimeType must match the Content-Type header used in the PUT — R2 enforces this.
    Task<PresignedUploadResponse> GetPresignedUploadUrlAsync(
        string filename, string mimeType, string purpose, string? subFolder = null);

    // Step 3: after the browser has PUT the file, register its metadata in the DB.
    // storageKey is the exact key returned by Step 1.
    Task<FileRecord> RegisterUploadAsync(
        string storageKey, string mimeType, long sizeBytes, Guid? uploadedBy);

    // Soft-delete a FileRecord. The R2 object is cleaned up by the Phase 2.3 Quartz job.
    Task DeleteAsync(Guid fileId);

    // Generate a presigned GET URL so private objects can be served to authorized clients.
    // expiresInMinutes default is 60. Use short windows (5 min) for sensitive docs.
    string GetPresignedDownloadUrl(string storageKey, int expiresInMinutes = 60);
}

// Returned by Step 1. The browser uses UploadUrl and must pass StorageKey back in Step 3.
public record PresignedUploadResponse(
    string UploadUrl,       // signed PUT URL — valid for ExpiresInSeconds
    string StorageKey,      // R2 object key — pass this back in POST /api/files
    int    ExpiresInSeconds
);

public class FileService : IFileService
{
    private readonly IAmazonS3 _s3;
    private readonly AppDbContext _db;
    private readonly string _bucket;
    private readonly string? _publicUrlBase; // optional: "https://files.shpegsu.com"

    // Maps the logical purpose name to the pre-created R2 folder.
    // Add new entries here when a new upload category is introduced.
    // NEVER allow the client to pass arbitrary folder names — this prevents
    // a malicious caller from writing to unexpected paths in the bucket.
    private static readonly Dictionary<string, string> PurposeFolderMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["event_doc"]   = "EventDocs",
            ["flyer"]       = "EventFlyers",
            ["meeting_doc"] = "MeetingDocs",
        };

    public FileService(IAmazonS3 s3, AppDbContext db, IConfiguration cfg)
    {
        _s3    = s3;
        _db    = db;
        _bucket = cfg["R2_BUCKET_NAME"]
            ?? throw new InvalidOperationException("R2_BUCKET_NAME is not configured.");

        // R2_PUBLIC_URL is the base URL for public object access.
        // Set it if the bucket has public access enabled or has a custom domain.
        // Leave empty if all objects are private (access via presigned GET URLs).
        _publicUrlBase = cfg["R2_PUBLIC_URL"]?.TrimEnd('/').NullIfEmpty();
    }

    public Task<PresignedUploadResponse> GetPresignedUploadUrlAsync(
        string filename, string mimeType, string purpose, string? subFolder = null)
    {
        if (!PurposeFolderMap.TryGetValue(purpose, out var folder))
            throw new ArgumentException(
                $"Unknown upload purpose '{purpose}'. Valid values: {string.Join(", ", PurposeFolderMap.Keys)}");

        // Sanitize: strip path traversal chars and anything that breaks URL parsing.
        // Keeps alphanumerics, dots, hyphens, underscores only.
        var safeName = Regex.Replace(filename, @"[^a-zA-Z0-9._-]", "_");

        // Key format: {Folder}/{subFolder?}/{uuid}/{safe_filename}
        // The UUID guarantees no two uploads ever collide, even for identical filenames.
        // subFolder is typically an eventId, so EventDocs are grouped by event in R2.
        var keyParts = new List<string> { folder };
        if (!string.IsNullOrWhiteSpace(subFolder))
            keyParts.Add(subFolder);
        keyParts.Add(Guid.NewGuid().ToString());
        keyParts.Add(safeName);
        var key = string.Join("/", keyParts);

        var req = new GetPreSignedUrlRequest
        {
            BucketName  = _bucket,
            Key         = key,
            Verb        = HttpVerb.PUT,
            ContentType = mimeType,
            // 15 minutes: enough for any browser upload over slow connections;
            // tight enough that a leaked URL has limited damage potential.
            Expires = DateTime.UtcNow.AddMinutes(15),
        };

        // GetPreSignedURL is a local crypto operation — no network call.
        var url = _s3.GetPreSignedURL(req);

        return Task.FromResult(new PresignedUploadResponse(url, key, ExpiresInSeconds: 900));
    }

    public async Task<FileRecord> RegisterUploadAsync(
        string storageKey, string mimeType, long sizeBytes, Guid? uploadedBy)
    {
        // CdnUrl: if R2_PUBLIC_URL is configured, files are publicly readable at that URL.
        // If not, the CdnUrl is a placeholder — access should go through GetPresignedDownloadUrl.
        var cdnUrl = _publicUrlBase != null
            ? $"{_publicUrlBase}/{storageKey}"
            : $"r2://{_bucket}/{storageKey}";  // placeholder until public access is set up

        var file = new FileRecord
        {
            Id         = Guid.NewGuid(),
            SpacesKey  = storageKey,   // column name is "spaces_key" in DB (historical — means R2 key now)
            CdnUrl     = cdnUrl,
            MimeType   = mimeType,
            SizeBytes  = sizeBytes,
            UploadedBy = uploadedBy,
            UploadedAt = DateTime.UtcNow,
        };

        _db.Files.Add(file);
        await _db.SaveChangesAsync();
        return file;
    }

    public async Task DeleteAsync(Guid fileId)
    {
        var file = await _db.Files.FindAsync(fileId);
        if (file == null) return;

        // Soft delete only — the R2 object stays until the Phase 2.3 cleanup job runs.
        // This preserves the audit trail and lets admins see what was deleted.
        file.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public string GetPresignedDownloadUrl(string storageKey, int expiresInMinutes = 60)
    {
        var req = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key        = storageKey,
            Verb       = HttpVerb.GET,
            Expires    = DateTime.UtcNow.AddMinutes(expiresInMinutes),
        };
        return _s3.GetPreSignedURL(req);
    }
}

// Extension to coerce empty string → null (for optional config values).
internal static class StringExtensions
{
    public static string? NullIfEmpty(this string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
