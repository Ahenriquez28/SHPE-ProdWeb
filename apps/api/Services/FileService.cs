using Amazon.S3;
using Amazon.S3.Model;
using System.Text.RegularExpressions;

// FileService — wraps DigitalOcean Spaces (S3-compatible) for the three-step upload flow.
//
// WHY THREE STEPS (not just POST the file to the API):
//   Files can be large (resumes, photos, sponsor decks). If the browser POSTed bytes
//   to the API, the API would hold that memory, saturate its bandwidth, and become
//   a bottleneck. The three-step flow keeps the API out of the data path entirely:
//
//   1. Client → API:    POST /api/files/presigned  → signed PUT URL (valid 15 min)
//   2. Client → Spaces: PUT <bytes> to signed URL  → file lands directly in Spaces
//   3. Client → API:    POST /api/files             → write metadata row to DB
//
//   The API only sees the small JSON payloads, never the file bytes.
//
// DO SPACES COMPATIBILITY:
//   Spaces is S3-compatible. We configure the AWS SDK client to point at the DO endpoint
//   instead of aws.amazon.com. Everything else (presigned URLs, bucket operations) works
//   identically because Spaces implements the S3 API.
public interface IFileService
{
    // Step 1: generate a presigned PUT URL the browser can upload to directly.
    // The client must PUT with the exact Content-Type it declared here.
    Task<PresignedUploadResponse> GetPresignedUploadUrlAsync(
        string filename, string mimeType, string purpose);

    // Step 3: after the browser has PUT the file to Spaces, register its metadata in the DB.
    Task<FileRecord> RegisterUploadAsync(
        string spacesKey, string mimeType, long sizeBytes, Guid? uploadedBy);

    // Soft-delete a FileRecord. The actual Spaces object is cleaned up by the Phase 2.3 job.
    Task DeleteAsync(Guid fileId);
}

// Response shape returned by Step 1.
public record PresignedUploadResponse(
    string UploadUrl,   // signed PUT URL — valid for ExpiresInSeconds
    string SpacesKey,   // the key the client must pass back in Step 3
    int ExpiresInSeconds
);

public class FileService : IFileService
{
    private readonly IAmazonS3 _s3;
    private readonly AppDbContext _db;
    private readonly string _bucket;
    private readonly string _cdnHost;

    public FileService(IAmazonS3 s3, AppDbContext db, IConfiguration cfg)
    {
        _s3 = s3;
        _db = db;
        _bucket = cfg["DO_SPACES_BUCKET"]
            ?? throw new InvalidOperationException("DO_SPACES_BUCKET is not configured.");

        // CDN host defaults to the standard DO pattern: <bucket>.<region>.cdn.digitaloceanspaces.com
        // Override via DO_SPACES_CDN_HOST if a custom domain is configured on the bucket.
        _cdnHost = cfg["DO_SPACES_CDN_HOST"]
            ?? $"{_bucket}.{cfg["DO_SPACES_REGION"] ?? "nyc3"}.cdn.digitaloceanspaces.com";
    }

    public Task<PresignedUploadResponse> GetPresignedUploadUrlAsync(
        string filename, string mimeType, string purpose)
    {
        // Sanitize filename: strip path traversal characters and anything that would
        // break URL parsing. Keep alphanumerics, dots, hyphens, underscores.
        var safeName = Regex.Replace(filename, @"[^a-zA-Z0-9._-]", "_");

        // Key structure: {purpose}/{uuid}/{safe_filename}
        // The UUID guarantees uniqueness so two users uploading "resume.pdf" don't collide.
        // The purpose prefix groups related uploads in the Spaces console (resume_review/, flyer/, etc.)
        var key = $"{purpose}/{Guid.NewGuid()}/{safeName}";

        var req = new GetPreSignedUrlRequest
        {
            BucketName  = _bucket,
            Key         = key,
            Verb        = HttpVerb.PUT,
            ContentType = mimeType,
            // 15 minutes: enough for any browser upload over slow connections,
            // tight enough that a leaked URL has limited damage potential.
            Expires = DateTime.UtcNow.AddMinutes(15),
        };

        // GetPreSignedURL is a local crypto operation — no network call.
        // We wrap it in Task.FromResult so the interface stays async
        // (callers don't need to know it's synchronous under the hood).
        var url = _s3.GetPreSignedURL(req);

        return Task.FromResult(new PresignedUploadResponse(url, key, ExpiresInSeconds: 900));
    }

    public async Task<FileRecord> RegisterUploadAsync(
        string spacesKey, string mimeType, long sizeBytes, Guid? uploadedBy)
    {
        var file = new FileRecord
        {
            Id         = Guid.NewGuid(),
            SpacesKey  = spacesKey,
            CdnUrl     = $"https://{_cdnHost}/{spacesKey}",
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

        // Soft delete only — the Spaces object stays until the Phase 2.3 cleanup job runs.
        // This preserves the audit trail and allows admins to see what was uploaded.
        file.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
