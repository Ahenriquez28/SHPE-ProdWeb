# Phase 2 — Cross-Cutting Integrations

> **Goal:** Replace Phase 1 stubs with real implementations. Build the systems only the lead builds end-to-end: GSU API, comms (email/SMS), retention cleanup, security audit.

---

## 2.1 — GSU API integration + real Aztec check-in

**Replaces:** the stub `POST /api/events/{id}/checkin/qr` from Phase 1.3

**What needs to be in place before starting:**
- GSU API credentials (bearer token)
- Confirmed endpoint path and parameter name
- Sample response payload (real or anonymized)
- Confirmed rate limit (assumption: 60 req/min/IP)

**If credentials not yet received: STOP and escalate.** This blocker should have been resolved before June 1. The contact email is the single most time-sensitive item from `PRE_JUNE1_CHECKLIST.md`.

### `apps/api/Services/GsuApiClient.cs`

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Polly;

// GsuApiClient — wraps Georgia State's PantherCard lookup endpoint.
//
// Three concerns:
//   1. Cache responses for 24h (gsu_api_cache table) — same Aztec on same day
//      should never re-hit upstream.
//   2. Retry on 5xx and 429 with exponential backoff (Polly).
//   3. Tolerant parsing — we don't know exactly which JSON shape GSU returns,
//      so the parser tries multiple field names (firstName/first_name/givenName).
public interface IGsuApiClient
{
    Task<GsuPersonInfo?> LookupAsync(string aztecPayload, CancellationToken ct);
}

public record GsuPersonInfo
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string FullName { get; init; } = "";
    public string? Email { get; init; }
    public string RawJson { get; init; } = "";
}

public class GsuApiClient : IGsuApiClient
{
    private readonly HttpClient _http;
    private readonly AppDbContext _db;
    private readonly ILogger<GsuApiClient> _log;

    public GsuApiClient(HttpClient http, IConfiguration cfg, AppDbContext db, ILogger<GsuApiClient> log)
    {
        _http = http;
        _http.BaseAddress = new Uri(cfg["GSU_API_BASE_URL"]!);
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", cfg["GSU_API_KEY"]);
        _db = db;
        _log = log;
    }

    public async Task<GsuPersonInfo?> LookupAsync(string aztecPayload, CancellationToken ct)
    {
        // 1. Cache hit?
        var cached = await _db.GsuApiCache.FindAsync(new object[] { aztecPayload }, ct);
        if (cached != null && cached.ExpiresAt > DateTime.UtcNow)
        {
            return ParseTolerant(cached.ResponseJson);
        }

        // 2. Upstream call with retry policy
        var policy = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r =>
                (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(3, attempt =>
                TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)));

        var response = await policy.ExecuteAsync(() =>
            _http.GetAsync($"/api/student?aztec={Uri.EscapeDataString(aztecPayload)}", ct));

        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
        {
            _log.LogError("GSU API failed: {Status}", response.StatusCode);
            throw new HttpRequestException($"GSU API returned {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);

        // 3. Cache for 24h
        var cacheRow = cached ?? new GsuApiCache { AztecPayload = aztecPayload };
        cacheRow.ResponseJson = json;
        cacheRow.CachedAt = DateTime.UtcNow;
        cacheRow.ExpiresAt = DateTime.UtcNow.AddHours(24);
        if (cached == null) _db.GsuApiCache.Add(cacheRow);
        await _db.SaveChangesAsync(ct);

        return ParseTolerant(json);
    }

    // Tolerant parser — handles whatever JSON shape GSU returns.
    // Tries multiple field name variants (camelCase, snake_case, alternate spellings).
    private static GsuPersonInfo? ParseTolerant(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var firstName = TryGet(root, "firstName", "first_name", "givenName");
            var lastName  = TryGet(root, "lastName", "last_name", "surname", "familyName");
            var fullName  = TryGet(root, "fullName", "full_name", "name");
            var email     = TryGet(root, "email", "pantherEmail", "studentEmail", "student_email");

            return new GsuPersonInfo
            {
                FirstName = firstName,
                LastName = lastName,
                FullName = fullName ?? $"{firstName} {lastName}".Trim(),
                Email = email,
                RawJson = json,
            };
        }
        catch { return null; }
    }

    private static string? TryGet(JsonElement root, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        }
        return null;
    }
}
```

Install Polly:
```bash
dotnet add package Polly
```

Register in `Program.cs`:
```csharp
builder.Services.AddHttpClient<IGsuApiClient, GsuApiClient>();
```

### Replace stub QR check-in endpoint

See `LEAD_SCAFFOLDING.md` § 2.1 for the full updated `/checkin/qr` handler that:
- Calls `IGsuApiClient.LookupAsync`
- Upserts Person by gsu_email
- Detects first-time scans and queues enrichment email
- Inserts Attendance with `method=qr`
- Returns 502 ProblemDetails when GSU API is unreachable

### `.env.example`

```bash
# ─── GSU PantherCard API ──────────────────────────────
# Get from your GSU contact — see PRE_JUNE1_CHECKLIST.md
GSU_API_BASE_URL=https://api.gsu.edu
GSU_API_KEY=REPLACE_WITH_GSU_API_KEY
```

### Definition of done

- Scanning a real PantherCard QR returns the student's name + email
- Cache hit on second scan within 24h (verify in psql)
- GSU API down → 502 with helpful detail; manual fallback recommended

---

## 2.2 — Comms infrastructure (SES email + Telnyx SMS)

**Replaces:** stubbed `POST /api/comms/email` and `POST /api/comms/sms` from Phase 1.5

**Prereqs:**
- AWS SES production access granted (sandbox cap removed)
- Telnyx 10DLC brand AND campaign approved (this is the long pole)

### `apps/api/Services/SesEmailSender.cs`

```csharp
using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;

// SesEmailSender — wraps AWS SES SendEmail.
// Used for both transactional (welcome, enrichment) and broadcast (announcements).
public interface IEmailSender
{
    Task<string> SendAsync(EmailMessage msg, CancellationToken ct = default);
}

public record EmailMessage
{
    public required string To { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public string? ReplyTo { get; init; }
}

public class SesEmailSender : IEmailSender
{
    private readonly IAmazonSimpleEmailService _ses;
    private readonly string _fromEmail;

    public SesEmailSender(IConfiguration cfg)
    {
        _ses = new AmazonSimpleEmailServiceClient(
            cfg["AWS_ACCESS_KEY_ID"],
            cfg["AWS_SECRET_ACCESS_KEY"],
            RegionEndpoint.GetBySystemName(cfg["AWS_SES_REGION"] ?? "us-east-1"));
        _fromEmail = cfg["AWS_SES_FROM_EMAIL"] ?? "noreply@shpegsu.com";
    }

    public async Task<string> SendAsync(EmailMessage msg, CancellationToken ct = default)
    {
        var req = new SendEmailRequest
        {
            Source = _fromEmail,
            Destination = new Destination { ToAddresses = new() { msg.To } },
            Message = new Message
            {
                Subject = new Content(msg.Subject),
                Body = new Body { Text = new Content(msg.Body) },
            },
        };
        if (msg.ReplyTo != null)
            req.ReplyToAddresses = new() { msg.ReplyTo };

        var res = await _ses.SendEmailAsync(req, ct);
        return res.MessageId;  // Used later to correlate bounce/complaint webhooks
    }
}
```

Install: `dotnet add package AWSSDK.SimpleEmail`

### `apps/api/Services/TelnyxSmsSender.cs`

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;

// TelnyxSmsSender — wraps Telnyx Messages API (HTTP, JSON).
// Subject to 10DLC throughput: ~2 messages per second per number on long code.
public interface ISmsSender
{
    Task<string> SendAsync(SmsMessage msg, CancellationToken ct = default);
}

public record SmsMessage
{
    public required string To { get; init; }  // E.164: +14705551234
    public required string Body { get; init; }
}

public class TelnyxSmsSender : ISmsSender
{
    private readonly HttpClient _http;
    private readonly string _fromNumber;

    public TelnyxSmsSender(HttpClient http, IConfiguration cfg)
    {
        _http = http;
        _http.BaseAddress = new Uri("https://api.telnyx.com/");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", cfg["TELNYX_API_KEY"]);
        _fromNumber = cfg["TELNYX_PHONE_NUMBER"]!;
    }

    public async Task<string> SendAsync(SmsMessage msg, CancellationToken ct = default)
    {
        var res = await _http.PostAsJsonAsync("v2/messages", new
        {
            from = _fromNumber,
            to = msg.To,
            text = msg.Body,
        }, ct);
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<TelnyxResponse>(cancellationToken: ct);
        return body!.Data.Id;
    }

    private record TelnyxResponse(TelnyxData Data);
    private record TelnyxData(string Id);
}
```

### Resolve segment → person IDs → enqueue

See `LEAD_SCAFFOLDING.md` § 2.2 for `CommsService.cs` which:
- Resolves predefined segment (`all_members`, `all_admins`, `all_panelists`) to person IDs
- For SMS, filters `sms_opt_in=true` and logs excluded count
- Inserts `comms_blast` row + N `comms_recipient` rows (status=queued)
- Enqueues Quartz job to batch-send

### Webhooks for bounce/complaint and STOP

- `POST /api/comms/webhooks/ses` — SNS notifications for bounces & complaints. Flip person `email = null` or mark do-not-send if hard bounce.
- `POST /api/comms/webhooks/telnyx` — delivery status + inbound messages. When STOP comes in, flip `person.sms_opt_in = false` and audit log.

### Definition of done

- Test send to your own email arrives within 30 seconds
- Test send SMS to your own phone arrives (must be on opted-in list)
- Reply STOP → opt-in flips off in DB, audit logged
- Bounced email → recipient row status=bounced

---

## 2.3 — Retention cleanup job (Quartz.NET)

**Why:** GDPR / privacy hygiene. People who haven't engaged in 2 years should be soft-deleted, then hard-deleted 30 days later.

### Install Quartz

```bash
dotnet add package Quartz.Extensions.Hosting
```

### `apps/api/Jobs/RetentionCleanupJob.cs`

```csharp
using Quartz;
using Microsoft.EntityFrameworkCore;

// RetentionCleanupJob — runs monthly via Quartz.
// 1. Soft-delete: people with no attendance in 24 months, not active eboard/alumni/panelist
// 2. Hard-delete: soft-deleted records older than 30 days
//
// All actions go through audit_log so super admins can review what was removed.
public class RetentionCleanupJob : IJob
{
    private readonly AppDbContext _db;
    private readonly ILogger<RetentionCleanupJob> _log;

    public RetentionCleanupJob(AppDbContext db, ILogger<RetentionCleanupJob> log)
    {
        _db = db; _log = log;
    }

    public async Task Execute(IJobExecutionContext ctx)
    {
        var cutoff = DateTime.UtcNow.AddMonths(-24);
        var hardCutoff = DateTime.UtcNow.AddDays(-30);

        // Soft-delete stale People (no recent attendance, no protected role)
        var stale = await _db.People
            .Where(p => p.DeletedAt == null)
            .Where(p => !_db.Attendances.Any(a => a.PersonId == p.Id && a.CheckedInAt > cutoff))
            .Where(p => !_db.PersonRoles.Any(r => r.PersonId == p.Id &&
                (r.Role == "eboard" || r.Role == "alumni" || r.Role == "panelist")))
            .ToListAsync();

        foreach (var p in stale)
        {
            p.DeletedAt = DateTime.UtcNow;
            _db.AuditLogs.Add(new AuditLog
            {
                Action = "RETENTION_SOFT_DELETE",
                TargetType = "person",
                TargetId = p.Id,
                At = DateTime.UtcNow,
            });
        }

        // Hard-delete people soft-deleted > 30 days ago
        var hardCandidates = await _db.People
            .IgnoreQueryFilters()  // Bypass soft-delete filter
            .Where(p => p.DeletedAt != null && p.DeletedAt < hardCutoff)
            .ToListAsync();

        _db.People.RemoveRange(hardCandidates);
        foreach (var p in hardCandidates)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                Action = "RETENTION_HARD_DELETE",
                TargetType = "person",
                TargetId = p.Id,
                At = DateTime.UtcNow,
            });
        }

        await _db.SaveChangesAsync();
        _log.LogInformation("Retention: soft-deleted {Soft}, hard-deleted {Hard}", stale.Count, hardCandidates.Count);
    }
}
```

### Register in `Program.cs`

```csharp
// Quartz — runs background jobs.
// Single job for now: retention cleanup on the 1st of every month at 3am UTC.
builder.Services.AddQuartz(q =>
{
    q.AddJob<RetentionCleanupJob>(o => o.WithIdentity("retention"));
    q.AddTrigger(t => t.ForJob("retention")
        .WithCronSchedule("0 0 3 1 * ?"));
});
builder.Services.AddQuartzHostedService();
```

### Super-admin preview endpoint

`POST /api/admin/retention/preview` runs the same SQL in a transaction, returns row counts, then rolls back. Lets a super-admin see what would be deleted before actually running it.

### Definition of done

- Job runs on schedule (verify by tightening to `0 * * * * ?` temporarily)
- Preview endpoint returns counts without actually deleting
- Audit log fills with `RETENTION_*` rows

---

## 2.4 — Polish + security audit

### OWASP Top 10 checks

- **SQL injection:** N/A (EF parameterizes all queries; no raw SQL outside cleanup job)
- **Auth bypass:** test every admin endpoint with a member-only token → must return 403
- **CSRF:** mitigated by Clerk + same-site cookie defaults
- **Open redirect:** no user-controlled redirects in code
- **XSS:** React escapes by default; review any `dangerouslySetInnerHTML`
- **Sensitive data exposure:** verify HTTPS-only in production, no logging of tokens
- **Insecure dependencies:** Dependabot enabled, snapshot before deploy

### Penetration test

1. Sign up a regular member account
2. Get their JWT
3. Curl every `/api/admin/*` and `/api/super/*` endpoint
4. Verify 401/403 on every one
5. Document results in `docs/runbooks/security-audit-2026-XX-XX.md`

### TCPA + FERPA review

- Schedule 30-min call with faculty advisor
- Walk through SMS opt-in flow + audit logging
- Walk through student data handling
- Get written sign-off (email is fine)

### Production deploy

- Use the same DO App Platform spec as staging
- Different DB (`db-s-1vcpu-1gb` for production)
- Different Spaces bucket (`shpegsu-prod`)
- DNS: `shpegsu.com` apex points to App Platform
- SES production access verified before launch

### Demo

Final 15-minute demo to faculty + team. Show every vertical. Hand over admin credentials. Project complete.
