// FilesEndpoints — two endpoints for the three-step browser-direct-upload flow.
//
// ENDPOINTS:
//   POST /api/files/presigned   → Step 1: get a signed PUT URL
//   POST /api/files             → Step 3: register file metadata after browser upload
//
// BROWSER FLOW:
//   1. Browser calls POST /api/files/presigned with { filename, mimeType, purpose }
//   2. Browser PUTs file bytes directly to the returned uploadUrl (Spaces, not this API)
//   3. Browser calls POST /api/files with { spacesKey, mimeType, sizeBytes } to write the DB row
//
// Both endpoints require at least Member role.
// The audit log middleware automatically records both POSTs.
public static class FilesEndpoints
{
    public static void MapFilesEndpoints(this WebApplication app)
    {
        // Step 1 — generate a presigned PUT URL
        // The client declares what it's about to upload; we return a URL it can PUT to.
        app.MapPost("/api/files/presigned",
            async (PresignedRequest req, IFileService files) =>
            {
                var result = await files.GetPresignedUploadUrlAsync(
                    req.Filename, req.MimeType, req.Purpose);

                return Results.Ok(result);
            })
            .RequireMember()
            .WithSummary("Get a presigned URL to upload a file directly to Spaces");

        // Step 3 — register the uploaded file's metadata
        // Called after the browser has successfully PUT the file to Spaces.
        app.MapPost("/api/files",
            async (RegisterFileRequest req, IFileService files, HttpContext ctx) =>
            {
                var file = await files.RegisterUploadAsync(
                    req.SpacesKey,
                    req.MimeType,
                    req.SizeBytes,
                    ctx.GetPersonId());

                // 201 Created with a Location header pointing to the new resource.
                return Results.Created($"/api/files/{file.Id}", new FileResponse(
                    file.Id,
                    file.CdnUrl,
                    file.MimeType,
                    file.SizeBytes,
                    file.UploadedAt));
            })
            .RequireMember()
            .WithSummary("Register a file after uploading it directly to Spaces");
    }
}

// ── Request / Response records ────────────────────────────────────────────────

// Step 1 request: what the browser is about to upload
public record PresignedRequest(
    string Filename,  // original filename, e.g. "jane_doe_resume.pdf"
    string MimeType,  // MIME type, e.g. "application/pdf"
    string Purpose    // folder prefix in Spaces, e.g. "resume_review", "flyer", "headshot"
);

// Step 3 request: confirm what was uploaded
public record RegisterFileRequest(
    string SpacesKey,  // the key returned by Step 1
    string MimeType,   // MIME type confirmed after upload
    long   SizeBytes   // actual file size in bytes
);

// Response returned after Step 3 — what the frontend stores
public record FileResponse(
    Guid     Id,
    string   CdnUrl,
    string   MimeType,
    long     SizeBytes,
    DateTime UploadedAt
);
