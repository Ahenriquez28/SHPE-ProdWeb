# Phase 0 — Backend Foundation

> **Goal:** Finish the backend foundation so it's ready for Phase 1 vertical scaffolding. Each step has a definition-of-done — meet it before moving on. Every file gets explanatory comments.

---

## 0.6 — Audit log middleware

**Why:** Every mutation by an authenticated user gets recorded automatically. Other devs don't have to think about audit logging — it just happens.

### Create `apps/api/Middleware/AuditLogMiddleware.cs`

```csharp
using Microsoft.EntityFrameworkCore;

// AuditLogMiddleware records every authenticated mutation to the audit_log table.
//
// Pipeline position: runs AFTER ClerkAuthMiddleware (so it can read GetPersonId())
// and AFTER the endpoint completes (so we only log successful responses).
//
// For richer audit events (merges, role changes, bootstrap super-admin),
// services write directly to db.AuditLogs with target_type, target_id, details.
// This middleware only captures the generic "someone did X to Y" pattern.
public class AuditLogMiddleware
{
    private readonly RequestDelegate _next;

    public AuditLogMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx, AppDbContext db)
    {
        // Execute the rest of the pipeline first.
        // We only want to log the outcome (success/failure), not the attempt.
        // If we logged before _next, every 500 error would also get a row.
        await _next(ctx);

        // Filter: only log mutations (POST/PATCH/PUT/DELETE), by authenticated users,
        // with successful responses (status < 400).
        var isMutation = ctx.Request.Method is "POST" or "PATCH" or "PUT" or "DELETE";
        var isAuthed = ctx.GetPersonId() != null;
        var isSuccess = ctx.Response.StatusCode < 400;

        if (!isMutation || !isAuthed || !isSuccess) return;

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorPersonId = ctx.GetPersonId(),
            Action = $"{ctx.Request.Method} {ctx.Request.Path}",
            IpAddress = ctx.Connection.RemoteIpAddress?.ToString(),
            UserAgent = ctx.Request.Headers.UserAgent.ToString(),
            At = DateTime.UtcNow,
        });

        // Never let audit log failure break the response.
        // If Postgres is down or the DbContext is disposed, swallow it.
        // We'd rather miss a log entry than 500 the user's request.
        try { await db.SaveChangesAsync(); }
        catch { /* deliberately swallowed */ }
    }
}
```

### Wire up in `Program.cs`

After the existing line `app.UseMiddleware<ClerkAuthMiddleware>();`, add:

```csharp
// Audit log middleware. MUST come after ClerkAuthMiddleware so it can
// read ctx.GetPersonId() — otherwise every mutation appears anonymous.
app.UseMiddleware<AuditLogMiddleware>();
```

### Verify

- `make up`
- Hit a mutation endpoint once they exist (or temporarily add a test POST endpoint)
- `make psql` → `SELECT * FROM audit_log;` shows a new row

### Definition of done

Audit log fills up as you click around the admin panel. Other devs need do nothing.

---

## 0.7 — Bootstrap super-admin

**Why:** Day-one and disaster recovery. Someone needs to be able to grant the first super-admin role without manually running SQL.

### Create `apps/api/Services/PeopleService.cs`

```csharp
using Microsoft.EntityFrameworkCore;

// PeopleService handles business logic around Person records:
// signups, profile updates, bootstrap super-admin, merging duplicates, etc.
//
// Endpoints inject IPeopleService rather than working with AppDbContext directly.
// Pattern: endpoints are thin (request → service → response).
public interface IPeopleService
{
    // Called whenever a new Person is created via signup.
    // Grants super_admin + admin + member roles to the first signup whose email
    // matches BOOTSTRAP_SUPER_ADMIN_EMAIL, but only if no super_admin exists yet.
    Task CheckBootstrapSuperAdminAsync(Person newPerson);
}

public class PeopleService : IPeopleService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public PeopleService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task CheckBootstrapSuperAdminAsync(Person newPerson)
    {
        // Read the bootstrap email from environment / appsettings.
        // If it's not set, this feature is disabled entirely.
        var bootstrapEmail = _config["BOOTSTRAP_SUPER_ADMIN_EMAIL"];
        if (string.IsNullOrWhiteSpace(bootstrapEmail)) return;

        // Idempotent guard: once a super_admin exists in the DB,
        // this method never fires again, even if the env var still matches.
        var hasSuperAdmin = await _db.PersonRoles
            .AnyAsync(r => r.Role == Roles.SuperAdmin);
        if (hasSuperAdmin) return;

        // Only the configured email gets bootstrapped — random signups don't.
        if (!string.Equals(newPerson.Email, bootstrapEmail, StringComparison.OrdinalIgnoreCase))
            return;

        // Grant the three foundational roles.
        // super_admin can do everything; admin and member are listed explicitly
        // so the bootstrap person shows up in member-only and admin-only queries
        // without needing a "super_admin implies all" workaround in those queries.
        _db.PersonRoles.Add(new PersonRole { Id = Guid.NewGuid(), PersonId = newPerson.Id, Role = Roles.SuperAdmin });
        _db.PersonRoles.Add(new PersonRole { Id = Guid.NewGuid(), PersonId = newPerson.Id, Role = Roles.Admin });
        _db.PersonRoles.Add(new PersonRole { Id = Guid.NewGuid(), PersonId = newPerson.Id, Role = Roles.Member });

        // Audit log via service (not middleware) because we want to capture
        // the specific reason and target. The middleware only captures method+path.
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorPersonId = newPerson.Id,
            Action = "BOOTSTRAP_SUPER_ADMIN",
            TargetType = "person",
            TargetId = newPerson.Id,
            At = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
    }
}
```

### Register in `Program.cs`

Add before `var app = builder.Build();`:

```csharp
// PeopleService — business logic for Person CRUD, bootstrap, merge, etc.
// Scoped lifetime: one instance per HTTP request, sharing the DbContext.
builder.Services.AddScoped<IPeopleService, PeopleService>();
```

### Add to `.env.example`

```bash
# ─── Bootstrap super-admin ──────────────────────────────
# The first person to sign up with this email gets super_admin + admin + member.
# After someone has super_admin in the database, this variable is ignored.
# Used for disaster recovery — see docs/runbooks/admin-recovery.md
BOOTSTRAP_SUPER_ADMIN_EMAIL=REPLACE_WITH_YOUR_EMAIL
```

### Create `docs/runbooks/admin-recovery.md`

```markdown
# Admin Recovery

If all super-admin accounts are lost, you have three ways to recover access.

## Hatch 1 (preferred) — Bootstrap env var

1. Set `BOOTSTRAP_SUPER_ADMIN_EMAIL=<your-email>` in production env
2. Restart the API service
3. Have someone sign up with that email
4. PeopleService.CheckBootstrapSuperAdminAsync grants super_admin on signup
5. Once the role is in place, unset the env var

## Hatch 2 — Admin CLI tool

(Not yet built — see `tools/AdminCli/`)

## Hatch 3 — Raw SQL

```sql
INSERT INTO person_role (id, person_id, role, assigned_at)
VALUES (gen_random_uuid(), '<person-uuid>', 'super_admin', now());
```

Run via `make psql` against the production database. Only use if Hatches 1 and 2 fail.
```

### Verify

Once signup endpoint exists (Phase 1.1):
1. Set `BOOTSTRAP_SUPER_ADMIN_EMAIL` to your email
2. Sign up with that email
3. `SELECT role FROM person_role WHERE person_id = '<your-id>';` → 3 roles
4. Sign up with a different email → only `member` role

### Definition of done

The bootstrap path works without anyone touching SQL. Documented in runbook.

---

## 0.8 — File upload infrastructure (DigitalOcean Spaces)

**Why:** Photos, flyers, resumes, logos, sponsor packets — every vertical needs file uploads. Build the pattern once.

### Architecture

Three-step flow keeps large files out of the API process:

1. Client asks API for a **presigned PUT URL**
2. Client uploads the file **directly to DO Spaces** using that URL
3. Client tells the API "I uploaded it — please register it" with the Spaces key

The API never sees the file bytes. This is critical for resumes/photos which can be multi-megabyte.

### Add entity `apps/api/Models/FileRecord.cs`

```csharp
// FileRecord tracks every file uploaded via the app.
// The actual file content lives in DigitalOcean Spaces (S3-compatible blob storage).
// We only store metadata here: the Spaces key, MIME type, size, who uploaded it.
public class FileRecord
{
    public Guid Id { get; set; }

    // Path inside the Spaces bucket, e.g. "resume_review/abc-123/jane.pdf"
    public string SpacesKey { get; set; } = "";

    // Public CDN URL for serving (Spaces auto-fronts files with a CDN)
    public string CdnUrl { get; set; } = "";

    public string MimeType { get; set; } = "";
    public long SizeBytes { get; set; }

    public Guid? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}
```

### Add configuration `apps/api/Data/Configurations/FileRecordConfiguration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class FileRecordConfiguration : IEntityTypeConfiguration<FileRecord>
{
    public void Configure(EntityTypeBuilder<FileRecord> b)
    {
        b.ToTable("file_record");
        b.HasKey(f => f.Id);
        b.Property(f => f.Id).HasColumnName("id");
        b.Property(f => f.SpacesKey).HasColumnName("spaces_key").IsRequired();
        b.Property(f => f.CdnUrl).HasColumnName("cdn_url").IsRequired();
        b.Property(f => f.MimeType).HasColumnName("mime_type").IsRequired();
        b.Property(f => f.SizeBytes).HasColumnName("size_bytes");
        b.Property(f => f.UploadedBy).HasColumnName("uploaded_by");
        b.Property(f => f.UploadedAt).HasColumnName("uploaded_at");
        b.Property(f => f.DeletedAt).HasColumnName("deleted_at");

        // Soft-delete: queries through db.Files auto-exclude deleted rows
        b.HasQueryFilter(f => f.DeletedAt == null);
    }
}
```

### Register DbSet in `AppDbContext.cs`

```csharp
public DbSet<FileRecord> Files => Set<FileRecord>();
```

### Install AWS S3 SDK

```bash
cd apps/api
dotnet add package AWSSDK.S3
```

### Create `apps/api/Services/FileService.cs`

```csharp
using Amazon.S3;
using Amazon.S3.Model;
using System.Text.RegularExpressions;

// FileService wraps DigitalOcean Spaces (S3-compatible) operations.
//
// Flow:
//   1. Client calls GetPresignedUploadAsync — gets a signed PUT URL valid for 15 min
//   2. Client PUTs the file directly to Spaces using that URL (API not involved)
//   3. Client calls RegisterUploadedFileAsync to write the metadata row
//
// This means the API never holds large files in memory — they go
// straight from browser to Spaces. Critical for resumes and photos.
public interface IFileService
{
    Task<PresignedUploadResponse> GetPresignedUploadAsync(string filename, string mimeType, string purpose);
    Task<FileRecord> RegisterUploadedFileAsync(string spacesKey, string mimeType, long sizeBytes, Guid? uploadedBy);
    Task DeleteAsync(Guid fileId);
}

public record PresignedUploadResponse
{
    public required string UploadUrl { get; init; }
    public required string SpacesKey { get; init; }
    public int ExpiresInSeconds { get; init; }
}

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
        _bucket = cfg["DO_SPACES_BUCKET"]!;

        // CDN host can be customized via env var, otherwise defaults to the standard pattern.
        // DO Spaces auto-creates a CDN at <bucket>.<region>.cdn.digitaloceanspaces.com
        _cdnHost = cfg["DO_SPACES_CDN_HOST"]
            ?? $"{_bucket}.{cfg["DO_SPACES_REGION"]}.cdn.digitaloceanspaces.com";
    }

    public async Task<PresignedUploadResponse> GetPresignedUploadAsync(
        string filename, string mimeType, string purpose)
    {
        // Sanitize the filename — strip anything that could break URL parsing or
        // be used for path traversal (../). Keep alphanumerics, dots, hyphens, underscores.
        var safe = Regex.Replace(filename, @"[^a-zA-Z0-9_.-]", "_");

        // Key structure: purpose/uuid/safe_filename
        //   - "purpose" groups related uploads (resume_review, flyer, photo, headshot, logo, doc)
        //   - the UUID prevents collisions even with the same filename
        //   - the original-ish filename helps debugging in Spaces console
        var key = $"{purpose}/{Guid.NewGuid()}/{safe}";

        var req = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            // 15 minutes is enough for any browser upload but tight enough
            // that a leaked URL has limited damage potential.
            Expires = DateTime.UtcNow.AddMinutes(15),
            ContentType = mimeType,
        };

        return new PresignedUploadResponse
        {
            UploadUrl = await _s3.GetPreSignedURLAsync(req),
            SpacesKey = key,
            ExpiresInSeconds = 900,
        };
    }

    public async Task<FileRecord> RegisterUploadedFileAsync(
        string spacesKey, string mimeType, long sizeBytes, Guid? uploadedBy)
    {
        var file = new FileRecord
        {
            Id = Guid.NewGuid(),
            SpacesKey = spacesKey,
            CdnUrl = $"https://{_cdnHost}/{spacesKey}",
            MimeType = mimeType,
            SizeBytes = sizeBytes,
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

        // Soft delete in our DB so audit trail is preserved.
        // Actual Spaces deletion happens in a nightly cleanup job (Phase 2.3).
        file.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
```

### Register in `Program.cs`

Add before `var app = builder.Build();`:

```csharp
// AWS S3 client configured for DigitalOcean Spaces.
// Spaces is S3-compatible — we just override the service URL to point at DO.
builder.Services.AddSingleton<IAmazonS3>(_ =>
{
    var cfg = builder.Configuration;
    var s3Config = new AmazonS3Config
    {
        ServiceURL = $"https://{cfg["DO_SPACES_REGION"]}.digitaloceanspaces.com",
        ForcePathStyle = false,
    };
    return new AmazonS3Client(
        cfg["DO_SPACES_KEY"],
        cfg["DO_SPACES_SECRET"],
        s3Config);
});

// FileService — presigned URLs + metadata management for file uploads
builder.Services.AddScoped<IFileService, FileService>();
```

### Add endpoints in `apps/api/Endpoints/FilesEndpoints.cs`

```csharp
// FilesEndpoints — two endpoints for the file upload flow.
//
//   POST /api/files/presigned    — get a presigned PUT URL for direct-to-Spaces upload
//   POST /api/files              — register the file metadata after browser-side upload
//
// Browser flow:
//   1. POST /api/files/presigned with { filename, mimeType, purpose }
//   2. PUT file bytes to the returned uploadUrl
//   3. POST /api/files with { spacesKey, mimeType, sizeBytes }
//
// Both require an authenticated member at minimum.
public static class FilesEndpoints
{
    public static void MapFilesEndpoints(this WebApplication app)
    {
        app.MapPost("/api/files/presigned",
            async (PresignedRequest req, IFileService files) =>
            {
                var result = await files.GetPresignedUploadAsync(
                    req.Filename, req.MimeType, req.Purpose);
                return Results.Ok(result);
            }).RequireMember();

        app.MapPost("/api/files",
            async (RegisterFileRequest req, IFileService files, HttpContext ctx) =>
            {
                var file = await files.RegisterUploadedFileAsync(
                    req.SpacesKey, req.MimeType, req.SizeBytes,
                    ctx.GetPersonId());
                return Results.Created($"/api/files/{file.Id}", file);
            }).RequireMember();
    }
}

public record PresignedRequest(string Filename, string MimeType, string Purpose);
public record RegisterFileRequest(string SpacesKey, string MimeType, long SizeBytes);
```

Register in `Program.cs` after the existing route mapping:

```csharp
app.MapFilesEndpoints();
```

### Update `.env.example`

```bash
# ─── DigitalOcean Spaces (file uploads) ─────────────────
# Create a Spaces bucket in DO, then generate access keys
# Spaces → Settings → Access Keys → Generate New Key
DO_SPACES_KEY=REPLACE_WITH_DO_SPACES_KEY
DO_SPACES_SECRET=REPLACE_WITH_DO_SPACES_SECRET
DO_SPACES_BUCKET=shpegsu-staging
DO_SPACES_REGION=nyc3
# Optional — defaults to <bucket>.<region>.cdn.digitaloceanspaces.com
DO_SPACES_CDN_HOST=
```

### Migration

```bash
make migrate-add name=AddFileRecord
make migrate-up
```

### Definition of done

- `file_record` table exists in Postgres
- Endpoint `POST /api/files/presigned` returns a signed URL
- Manual test once Spaces creds are filled in: presigned URL accepts a PUT, then `POST /api/files` writes the metadata row

### What's blocked

Real uploads can't be tested until Alex creates the Spaces bucket and fills in the keys. Code is ready for that.

---

## 0.9 — Shared error handling (RFC 7807 Problem Details)

**Why:** Consistent error shape across the API. The frontend has one error handler that knows what to expect from any endpoint.

### What RFC 7807 looks like

Every error response is JSON:

```json
{
  "type": "https://shpegsu.com/errors/validation",
  "title": "Validation failed",
  "status": 400,
  "detail": "Email is required",
  "traceId": "00-abc..."
}
```

### Use built-in ASP.NET support

In `Program.cs`, replace:

```csharp
builder.Services.AddProblemDetails();
```

with:

```csharp
// ProblemDetails — RFC 7807 standardized error responses.
// CustomizeProblemDetails fires on every error response, letting us
// attach the trace ID so users can paste it in bug reports.
builder.Services.AddProblemDetails(opts =>
{
    opts.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
        ctx.ProblemDetails.Instance = ctx.HttpContext.Request.Path;
    };
});
```

### Domain error helpers

Create `apps/api/Middleware/ProblemDetailsResults.cs`:

```csharp
// Static helpers for returning common error shapes.
// Usage in endpoints: return ProblemDetailsResults.Conflict("Already checked in");
//
// The "type" URL is a stable identifier the frontend can branch on —
// not a real URL we have to host. RFC 7807 says it MAY resolve;
// most APIs treat it as an opaque slug.
public static class ProblemDetailsResults
{
    private const string TypeBase = "https://shpegsu.com/errors/";

    public static IResult ValidationFailed(IDictionary<string, string[]> errors) =>
        Results.ValidationProblem(errors,
            type: TypeBase + "validation",
            title: "Validation failed");

    public static IResult Conflict(string detail) =>
        Results.Problem(
            type: TypeBase + "conflict",
            title: "Conflict",
            detail: detail,
            statusCode: 409);

    public static IResult NotFound(string detail) =>
        Results.Problem(
            type: TypeBase + "not-found",
            title: "Not found",
            detail: detail,
            statusCode: 404);

    public static IResult UpstreamFailure(string upstream, string fallback) =>
        Results.Problem(
            type: TypeBase + $"upstream-{upstream}",
            title: $"{upstream} unreachable",
            detail: $"Falling back to {fallback}",
            statusCode: 502);

    public static IResult BadRequest(string detail) =>
        Results.Problem(
            type: TypeBase + "bad-request",
            title: "Bad request",
            detail: detail,
            statusCode: 400);
}
```

### Definition of done

- Endpoints return `Results.Ok(...)`, `ProblemDetailsResults.Conflict(...)`, etc.
- Every error response has the same JSON shape
- The frontend can write one error handler

---

## 0.10 — Rate limiting

**Why:** Defends against abuse and runaway costs (especially Telnyx SMS and the GSU API quota).

### Configure in `Program.cs`

Add before `var app = builder.Build();`:

```csharp
using System.Threading.RateLimiting;

// Rate limiting policies.
// Different policies for different endpoint classes:
//   - default: 300/min per user (general API browsing)
//   - auth:    5/min per IP (signup/login — defends against brute force)
//   - comms:   1/min per user (sending broadcasts — both abuse and cost defense)
//   - checkin: 120/min per user (scanning at events is bursty)
builder.Services.AddRateLimiter(opts =>
{
    // Generic default — applied if no other policy matches
    opts.AddPolicy("default", ctx =>
    {
        // Partition by person if authed, otherwise by IP, otherwise "anon"
        var key = ctx.GetPersonId()?.ToString()
                  ?? ctx.Connection.RemoteIpAddress?.ToString()
                  ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
            });
    });

    // Auth: stricter, by IP — defends against credential stuffing
    opts.AddPolicy("auth", ctx =>
    {
        var key = ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
            });
    });

    // Comms: 1/min per user — sending costs real money, prevent fat-finger blasts
    opts.AddPolicy("comms", ctx =>
    {
        var key = ctx.GetPersonId()?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1,
                Window = TimeSpan.FromMinutes(1),
            });
    });

    // Check-in: 120/min — scanning is bursty (10+ in 30 seconds), but we still want a cap
    opts.AddPolicy("checkin", ctx =>
    {
        var key = ctx.GetPersonId()?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
            });
    });

    // When limit exceeded, return 429 with Retry-After
    opts.RejectionStatusCode = 429;
    opts.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.Headers["Retry-After"] = "60";
        await ctx.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://shpegsu.com/errors/rate-limit",
            title = "Too many requests",
            status = 429,
            detail = "Slow down and try again in a minute",
        }, cancellationToken: ct);
    };
});
```

After `app.UseCors();` add:

```csharp
// Rate limiter middleware — must come before any endpoint mapping.
app.UseRateLimiter();
```

### Usage in endpoints

```csharp
app.MapPost("/api/auth/signup", SignupHandler)
   .RequireRateLimiting("auth");

app.MapPost("/api/comms/email", SendEmailHandler)
   .RequireAdmin()
   .RequireRateLimiting("comms");

app.MapPost("/api/events/{id}/checkin/qr", ScanHandler)
   .RequireAdmin()
   .RequireRateLimiting("checkin");
```

### Definition of done

Hammering an endpoint with `for i in {1..400}; do curl localhost:5001/api/health; done` returns 429 with `Retry-After: 60`.

---

## 0.12 — CI/CD (GitHub Actions)

**Why:** Every PR shows green CI before merge. Push to main deploys to staging. No "works on my machine" — works in CI or it doesn't ship.

### Create `.github/workflows/ci.yml`

```yaml
# CI pipeline — runs on every PR and every push to main.
# Status: required before merge (configured in repo branch protection).
#
# Steps:
#   1. Spin up a Postgres service container so integration tests can use a real DB
#   2. Build + test the .NET API
#   3. Build + lint + typecheck the React web app
#
# Both apps must pass for CI to be green.

name: CI

on:
  pull_request:
    branches: [main]
  push:
    branches: [main]

jobs:
  api:
    runs-on: ubuntu-latest

    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_USER: postgres
          POSTGRES_PASSWORD: postgres
          POSTGRES_DB: shpe_test
        ports: ['5432:5432']
        options: >-
          --health-cmd "pg_isready -U postgres"
          --health-interval 5s
          --health-timeout 5s
          --health-retries 5

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x

      - name: Restore
        working-directory: apps/api
        run: dotnet restore

      - name: Build
        working-directory: apps/api
        run: dotnet build --no-restore --configuration Release

      - name: Test
        working-directory: apps/api
        run: dotnet test --no-build --configuration Release --logger "console;verbosity=normal"
        env:
          ConnectionStrings__Postgres: "Host=localhost;Port=5432;Database=shpe_test;Username=postgres;Password=postgres"

  web:
    runs-on: ubuntu-latest

    steps:
      - uses: actions/checkout@v4

      - name: Setup Node 20
        uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: 'pnpm'

      - name: Install pnpm
        uses: pnpm/action-setup@v3
        with:
          version: 9

      - name: Install
        working-directory: apps/web
        run: pnpm install --frozen-lockfile

      - name: Typecheck
        working-directory: apps/web
        run: pnpm typecheck

      - name: Lint
        working-directory: apps/web
        run: pnpm lint

      - name: Test
        working-directory: apps/web
        run: pnpm test

      - name: Build
        working-directory: apps/web
        run: pnpm build
```

### Branch protection

Once the first PR merges (so the workflow has run at least once), go to:

GitHub repo → Settings → Branches → Add branch ruleset
- Branch pattern: `main`
- Require pull request before merging
- Require 1 approval
- Require status checks: `api` and `web`
- Block force pushes
- Block deletions

### Definition of done

Open a test PR. CI runs. Both jobs pass. Merge requires the green check.

---

## 0.13 — Staging deploy on DigitalOcean

**Why:** Every PR to main goes live on staging within 5 minutes. No "deploy and pray" against prod.

### Prerequisites

These need to exist before the deploy can work. Alex creates them in the DO dashboard:

1. **Managed Postgres database** — $15/mo cheapest tier, NYC3 region
2. **Spaces bucket** — `shpegsu-staging`, NYC3
3. **App Platform app** — connected to the GitHub repo

### App Platform configuration

Create `.do/app.yaml` in repo root:

```yaml
# DigitalOcean App Platform spec.
# Defines the staging environment: API + static frontend + managed DB.
# Push to main triggers a deploy.

name: shpe-gsu-staging

services:
  # ─── .NET API ───────────────────────────────────────────
  - name: api
    github:
      repo: Ahenriquez28/SHPE-ProdWeb
      branch: main
      deploy_on_push: true
    source_dir: apps/api
    dockerfile_path: apps/api/Dockerfile
    instance_count: 1
    # Smallest tier — fine for staging, scale up for production
    instance_size_slug: basic-xxs
    http_port: 8080
    routes:
      - path: /api
    health_check:
      http_path: /api/health
    envs:
      - key: ASPNETCORE_ENVIRONMENT
        value: Staging
      - key: ConnectionStrings__Postgres
        value: ${db.DATABASE_URL}
      - key: Clerk__Issuer
        value: ${CLERK_ISSUER}
        type: SECRET
      - key: BOOTSTRAP_SUPER_ADMIN_EMAIL
        value: ${BOOTSTRAP_SUPER_ADMIN_EMAIL}
        type: SECRET
      - key: DO_SPACES_KEY
        type: SECRET
      - key: DO_SPACES_SECRET
        type: SECRET
      - key: DO_SPACES_BUCKET
        value: shpegsu-staging
      - key: DO_SPACES_REGION
        value: nyc3
      # SES — leave as placeholders until production access granted
      - key: AWS_ACCESS_KEY_ID
        type: SECRET
      - key: AWS_SECRET_ACCESS_KEY
        type: SECRET
      - key: AWS_SES_REGION
        value: us-east-1
      - key: AWS_SES_FROM_EMAIL
        value: noreply@shpegsu.com
      # Telnyx
      - key: TELNYX_API_KEY
        type: SECRET
      - key: TELNYX_PHONE_NUMBER
        value: "+14706728573"

# ─── Static frontend ─────────────────────────────────────
static_sites:
  - name: web
    github:
      repo: Ahenriquez28/SHPE-ProdWeb
      branch: main
      deploy_on_push: true
    source_dir: apps/web
    build_command: pnpm install && pnpm build
    output_dir: dist
    routes:
      - path: /
    envs:
      - key: VITE_API_URL
        value: ${APP_URL}/api
      - key: VITE_CLERK_PUBLISHABLE_KEY
        type: SECRET

# ─── Managed Postgres ────────────────────────────────────
databases:
  - name: db
    engine: PG
    version: "16"
    size: db-s-dev-database  # $15/mo
    num_nodes: 1
    production: false

# ─── Custom domain ────────────────────────────────────────
domains:
  - domain: staging.shpegsu.com
    type: PRIMARY
```

### Add DNS records in Cloudflare

- `CNAME` for `staging.shpegsu.com` → the App Platform default URL (DO will give you this)

### Smoke test

After deploy:
```bash
curl https://staging.shpegsu.com/api/health
# → { "status": "ok" }
```

### Definition of done

Push to main triggers deploy. Within 5 min, `staging.shpegsu.com/api/health` returns 200.

---

## End of Phase 0

At this point the foundation is solid. Move on to `03_PHASE_0_5_FRONTEND.md`.
