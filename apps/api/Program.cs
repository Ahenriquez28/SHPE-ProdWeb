using Amazon.S3;
using Amazon.SimpleEmailV2;
using Amazon;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;


var builder = WebApplication.CreateBuilder(args);

//EntityFrameworkCore + Postgres database
//Reads the connection string from appsettings.Development.json
// It also registers AppDbContext so we can inject it anywhere with (AppDbContext db)
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));


// Register Clerk JWT verifier so it can be injected anywhere
builder.Services.AddSingleton<IClerkJwtVerifier, ClerkJwtVerifier>();

// PeopleService — business logic for Person CRUD, bootstrap, merge, etc.
// Scoped: one instance per HTTP request, sharing the request's DbContext.
builder.Services.AddScoped<IPeopleService, PeopleService>();

// ── DigitalOcean Spaces (S3-compatible file storage) ─────────────────────────
// We configure the AWS S3 client to hit DO Spaces instead of AWS by overriding ServiceURL.
// Spaces implements the S3 API, so everything (presigned URLs, uploads, deletes) works identically.
// Singleton: the client is thread-safe and expensive to construct — one instance for the app lifetime.
builder.Services.AddSingleton<IAmazonS3>(_ =>
{
    var cfg = builder.Configuration;
    var region = cfg["DO_SPACES_REGION"] ?? "nyc3";
    var s3Config = new AmazonS3Config
    {
        // DO Spaces endpoint: https://<region>.digitaloceanspaces.com
        ServiceURL   = $"https://{region}.digitaloceanspaces.com",
        // Path-style is needed for non-AWS S3 providers.
        // Without it, the SDK prepends the bucket name as a subdomain which DO Spaces doesn't support.
        ForcePathStyle = true,
    };
    return new AmazonS3Client(
        cfg["DO_SPACES_KEY"]    ?? "placeholder",
        cfg["DO_SPACES_SECRET"] ?? "placeholder",
        s3Config);
});

// FileService — presigned upload URLs + file metadata management
builder.Services.AddScoped<IFileService, FileService>();

// ── AWS SES v2 (email sending) ────────────────────────────────────────────────
// Separate client from the DO Spaces S3 client — SES is an AWS service (not DO),
// so it uses normal AWS regional endpoints, not the DO Spaces override.
// Singleton: AmazonSimpleEmailServiceV2Client is thread-safe.
builder.Services.AddSingleton<IAmazonSimpleEmailServiceV2>(_ =>
{
    var cfg    = builder.Configuration;
    var key    = cfg["AWS_ACCESS_KEY_ID"]     ?? throw new InvalidOperationException("AWS_ACCESS_KEY_ID is required.");
    var secret = cfg["AWS_SECRET_ACCESS_KEY"] ?? throw new InvalidOperationException("AWS_SECRET_ACCESS_KEY is required.");
    var region = cfg["AWS_SES_REGION"]        ?? "us-east-1";

    // RegionEndpoint.GetBySystemName resolves "us-east-1" → RegionEndpoint.USEast1 etc.
    return new AmazonSimpleEmailServiceV2Client(key, secret, RegionEndpoint.GetBySystemName(region));
});

// SesEmailService — scoped because it depends on AppDbContext (also scoped).
// BlastAsync needs to query member emails from the DB.
builder.Services.AddScoped<ISesEmailService, SesEmailService>();

// ── Rate limiting ─────────────────────────────────────────────────────────────
// Four policies for four risk profiles:
//   default  — 300/min per user/IP  (general browsing, can't easily DoS us)
//   auth     — 5/min per IP         (signup/login: stops brute-force and credential stuffing)
//   comms    — 1/min per user       (email/SMS blast: costs real money per send)
//   checkin  — 120/min per user     (event scanning: legitimately bursty, but still needs a cap)
//
// WHY PARTITION BY PERSON NOT IP FOR AUTHED ENDPOINTS:
//   IP-based limits hurt users behind NAT (entire office on one IP gets rate-limited together).
//   For authenticated users we partition by PersonId — each account gets its own bucket.
builder.Services.AddRateLimiter(opts =>
{
    // Default: 300 requests per minute per person (or IP if anonymous)
    opts.AddPolicy("default", ctx =>
    {
        var key = ctx.GetPersonId()?.ToString()
                  ?? ctx.Connection.RemoteIpAddress?.ToString()
                  ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1) });
    });

    // Auth: strict, IP-based — credential stuffing targets the auth endpoint specifically
    opts.AddPolicy("auth", ctx =>
    {
        var key = ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) });
    });

    // Comms: 1/min per user — a fat-finger broadcast to 500 members is a real incident
    opts.AddPolicy("comms", ctx =>
    {
        var key = ctx.GetPersonId()?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new FixedWindowRateLimiterOptions { PermitLimit = 1, Window = TimeSpan.FromMinutes(1) });
    });

    // Check-in: 120/min — scanning 10 members in 30 sec is normal; 200+ in a minute is not
    opts.AddPolicy("checkin", ctx =>
    {
        var key = ctx.GetPersonId()?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) });
    });

    // 429 response: RFC 7807-shaped JSON + Retry-After header
    opts.RejectionStatusCode = 429;
    opts.OnRejected = async (rejCtx, ct) =>
    {
        rejCtx.HttpContext.Response.Headers["Retry-After"] = "60";
        await rejCtx.HttpContext.Response.WriteAsJsonAsync(new
        {
            type    = "https://shpegsu.com/errors/rate-limit",
            title   = "Too many requests",
            status  = 429,
            detail  = "Slow down and try again in a minute.",
        }, cancellationToken: ct);
    };
});

// Memory Cache
// Used by the Clerk JWT verifier to cache the JWKS keys
// this is so we dont hit Clerk's servers on every single request (too much money)
builder.Services.AddMemoryCache();


// HTTP context accessor
// Setting up for APis so we can read the data within 
builder.Services.AddHttpContextAccessor();

//CORS (Cross-Origin Resourse Sharing)
//This allows the frontend and the backend to talk to eachother
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p =>
        p.WithOrigins(
            "http://localhost:5173",       // local Vite dev server
            "https://staging.shpegsu.com", // staging frontend
            "https://shpegsu.com"          // production frontend
        )
        .AllowAnyHeader()
        .AllowAnyMethod()));

//Creates the Swagger Page
//Swagger page is like a doc that shows up when running the program
//IT shows all of our APIs and allows us to test it directly
//Think abt it as PostMan but .Net framework just gives it to us 
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// RFC 7807 standardized error responses.
// CustomizeProblemDetails fires on every error so every response gets a traceId.
// The traceId lets users paste one string in a bug report that maps directly
// to a specific request in the logs — no more "it just didn't work" reports.
builder.Services.AddProblemDetails(opts =>
{
    opts.CustomizeProblemDetails = ctx =>
    {
        // TraceIdentifier is set by ASP.NET for every request — unique per request.
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
        // Instance is the path that failed, e.g. "/api/events/abc-123"
        ctx.ProblemDetails.Instance = ctx.HttpContext.Request.Path;
    };
});

var app = builder.Build();

// Global exception handler — catches any unhandled exception and returns RFC 7807.
// Without this, unhandled exceptions return an empty 500 with no body.
app.UseExceptionHandler();

// Status code pages — generates RFC 7807 bodies for responses with no body.
// This covers route-not-found (404) and method-not-allowed (405) which ASP.NET
// returns with an empty body by default. With AddProblemDetails configured above,
// this middleware fills in the standard { type, title, status, traceId } shape.
app.UseStatusCodePages();

//applying CORS headers to every response
app.UseCors();

// Rate limiter — must run before auth and endpoints so rejected requests are cheap.
// Does NOT run before CORS (browsers need the CORS preflight to succeed even on rate-limited requests).
app.UseRateLimiter();

// Run Clerk auth middleware on every request
app.UseMiddleware<ClerkAuthMiddleware>();

// Audit log middleware — MUST come after ClerkAuthMiddleware.
// ClerkAuthMiddleware sets ctx.Items["PersonId"]; AuditLogMiddleware reads it.
// If you swap the order, every audit row will have a null ActorPersonId.
app.UseMiddleware<AuditLogMiddleware>();

//Serving the Swagger Ui at /swagger
//Fix later since idk if everyone will be able to see this 
app.UseSwagger();
app.UseSwaggerUI();

// Health check endpoint
//Used by Docker, Digital Ocean, and devs to see if APis are running fine
app.MapGet("/api/health", () => Results.Ok(new { status = "ok"}));

// Auth endpoints — POST /api/auth/sync (first-login provisioning) + GET /api/me
app.MapAuthEndpoints();

// File upload endpoints (presigned URL + metadata registration)
app.MapFilesEndpoints();

// Email endpoints (send, blast, welcome) — all admin-gated, "comms" rate limited
app.MapEmailEndpoints();

// Auto-migrate on startup in Development and Staging.
//
// WHY NOT PRODUCTION:
//   Production has real member data. Running migrations automatically risks data
//   loss if a migration has a bug. In production, migrations run as a separate
//   pre-deploy job so we can verify the SQL before it touches live data.
//   See docs/runbooks/admin-recovery.md and the deployment guide.
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();