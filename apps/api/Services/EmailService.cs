// EmailService — AWS SES v2 integration for all outbound email from SHPE @ GSU.
//
// THREE SEND MODES:
//   SendAsync       — targeted send to specific addresses (admin one-offs, notifications)
//   BlastAsync      — fan-out to all active members, or a role-filtered subset
//   SendWelcomeAsync — onboarding email after a new member signs up
//
// ALL FIELDS PASSED TO SES:
//   From            : "SHPE @ Georgia State <noreply@shpegsu.com>"
//   To              : recipient list
//   Cc              : optional carbon copy list
//   ReplyTo         : optional reply-to override (e.g. president's address for event emails)
//   Subject         : plain string
//   Body (text)     : plain-text fallback (required for spam filters)
//   Body (html)     : SHPE-branded HTML wrapped by BuildHtmlEmail()
//
// IMPORTANT — SES SANDBOX MODE:
//   Until you apply for SES production access in the AWS Console, you can only
//   send TO verified email addresses. Go to:
//     AWS Console → SES → Verified Identities → Create Identity
//   Add the noreply@shpegsu.com domain AND any test recipient addresses.
//   After applying for production access, these restrictions are lifted.
//
// RATE / CONCURRENCY:
//   BlastAsync uses a SemaphoreSlim(10) to cap concurrent SES requests.
//   SES sandbox limit is ~1 email/sec; production is 200+/sec.
//   The HTTP endpoint is already rate-limited by the "comms" policy (1 blast/min per admin).

using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.EntityFrameworkCore;
using SesRequest = Amazon.SimpleEmailV2.Model.SendEmailRequest;

// ── DTOs (live here, imported globally via implicit usings) ──────────────────

// Targeted send to specific recipients.
public record EmailSendRequest(
    string[] To,                    // required — at least one recipient
    string   Subject,               // email subject line
    string   Body,                  // plain-text body (always required — spam filters expect it)
    string?  HtmlBody  = null,      // optional HTML version; if null, Body is wrapped in template
    string?  FromName  = null,      // display name override; default "SHPE @ Georgia State"
    string?  ReplyTo   = null,      // optional reply-to (e.g. "president@shpegsu.com")
    string[]? Cc       = null       // optional CC recipients
);

// Fan-out to all active members (or a role-filtered subset).
public record EmailBlastRequest(
    string   Subject,
    string   Body,
    string?  HtmlBody   = null,
    string?  RoleFilter = null      // null = all members; "eboard", "admin", "dev_team" etc.
);

// What every email method returns.
public record EmailResult(
    bool   Success,       // true if at least one email sent
    int    Sent,          // count successfully sent
    int    Failed,        // count that SES rejected
    string? Error = null  // human-readable error if Success = false
);

// ── Interface ────────────────────────────────────────────────────────────────

public interface ISesEmailService
{
    // Send to an explicit recipient list.
    Task<EmailResult> SendAsync(EmailSendRequest request, CancellationToken ct = default);

    // Blast to all active members (or role-filtered subset from DB).
    Task<EmailResult> BlastAsync(EmailBlastRequest request, CancellationToken ct = default);

    // Onboarding welcome email. Called internally after member signup.
    Task<bool> SendWelcomeAsync(string toEmail, string firstName, CancellationToken ct = default);
}

// ── Implementation ───────────────────────────────────────────────────────────

public class SesEmailService : ISesEmailService
{
    private readonly IAmazonSimpleEmailServiceV2 _ses;
    private readonly AppDbContext  _db;
    private readonly string _fromEmail;  // noreply@shpegsu.com
    private readonly string _fromName;   // SHPE @ Georgia State

    // SES client is Singleton; DB context is Scoped — ASP.NET handles lifecycle.
    public SesEmailService(
        IAmazonSimpleEmailServiceV2 ses,
        AppDbContext db,
        IConfiguration config)
    {
        _ses       = ses;
        _db        = db;
        _fromEmail = config["AWS_SES_FROM_EMAIL"] ?? "noreply@shpegsu.com";
        _fromName  = "SHPE @ Georgia State";
    }

    // ── SendAsync ────────────────────────────────────────────────────────────
    // Sends one message to the given To list.
    // Returns EmailResult with Sent = To.Length on full success,
    // or Sent = 0 + Error on SES rejection.
    public async Task<EmailResult> SendAsync(EmailSendRequest req, CancellationToken ct = default)
    {
        if (req.To.Length == 0)
            return new EmailResult(false, 0, 0, "No recipients specified.");

        var html = req.HtmlBody != null
            ? BuildHtmlEmail(req.Subject, req.HtmlBody)
            : BuildHtmlEmail(req.Subject, $"<p style=\"line-height:1.6;color:#0f172a;\">{System.Web.HttpUtility.HtmlEncode(req.Body).Replace("\n", "<br>")}</p>");

        var displayName = req.FromName ?? _fromName;
        var fromAddress = $"{displayName} <{_fromEmail}>";

        try
        {
            var sesReq = new SesRequest
            {
                // "Display Name <address>" format is the standard for friendly from lines
                FromEmailAddress = fromAddress,

                Destination = new Destination
                {
                    ToAddresses  = req.To.ToList(),
                    CcAddresses  = req.Cc?.ToList() ?? [],
                },

                // ReplyToAddresses lets replies go somewhere useful (admin inbox, not /dev/null)
                ReplyToAddresses = req.ReplyTo is not null
                    ? [req.ReplyTo]
                    : [],

                Content = new EmailContent
                {
                    Simple = new Message
                    {
                        Subject = new Content { Data = req.Subject, Charset = "UTF-8" },
                        Body    = new Body
                        {
                            // Plain-text is required — many spam filters penalize HTML-only emails
                            Text = new Content { Data = req.Body,  Charset = "UTF-8" },
                            Html = new Content { Data = html,      Charset = "UTF-8" },
                        },
                    },
                },
            };

            await _ses.SendEmailAsync(sesReq, ct);
            return new EmailResult(true, req.To.Length, 0);
        }
        catch (AmazonSimpleEmailServiceV2Exception ex)
        {
            // SES-specific errors: account paused, unverified recipient in sandbox,
            // message rejected, sending quota exceeded, etc.
            // ex.ErrorCode contains the SES error name (e.g. "MessageRejected")
            return new EmailResult(false, 0, req.To.Length, $"SES error [{ex.ErrorCode}]: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new EmailResult(false, 0, req.To.Length, $"Send failed: {ex.Message}");
        }
    }

    // ── BlastAsync ───────────────────────────────────────────────────────────
    // Queries all active members from the DB (optionally filtered by role),
    // then sends each one a copy of the email.
    //
    // Uses SemaphoreSlim(10) to send 10 emails concurrently — fast enough for
    // a student org but polite to SES rate limits.
    //
    // NOTE: In production with 500+ members, consider moving this to a
    // background job (Quartz.NET is already in the stack plan) so the HTTP
    // request doesn't time out waiting for all sends to complete.
    public async Task<EmailResult> BlastAsync(EmailBlastRequest req, CancellationToken ct = default)
    {
        // ── Resolve recipient list from DB ────────────────────────────────────
        // We query Person records directly, filtering to those who have an email address.
        // If RoleFilter is set, we only include people who have that role.
        IQueryable<Person> query = _db.People.Where(p => p.Email != null || p.GsuEmail != null);

        if (!string.IsNullOrWhiteSpace(req.RoleFilter))
        {
            // Join to PersonRoles to filter by role name
            query = query.Where(p => _db.PersonRoles
                .Any(r => r.PersonId == p.Id && r.Role == req.RoleFilter));
        }

        var recipients = await query
            .Select(p => new { p.FullName, Email = p.GsuEmail ?? p.Email })
            .ToListAsync(ct);

        if (recipients.Count == 0)
            return new EmailResult(false, 0, 0, "No recipients found matching the filter.");

        // ── Fan-out with concurrency cap ──────────────────────────────────────
        var semaphore  = new SemaphoreSlim(10, 10);
        var sent       = 0;
        var failed     = 0;
        var lastError  = string.Empty;

        var tasks = recipients.Select(async recipient =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                // Personalise the from line per recipient if desired in the future.
                // For now, every blast uses the same from address.
                var individualReq = new EmailSendRequest(
                    To:       [recipient.Email!],
                    Subject:  req.Subject,
                    Body:     req.Body,
                    HtmlBody: req.HtmlBody
                );
                var result = await SendAsync(individualReq, ct);
                if (result.Success)
                    Interlocked.Increment(ref sent);
                else
                {
                    Interlocked.Increment(ref failed);
                    lastError = result.Error ?? string.Empty;
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        return new EmailResult(
            Success: sent > 0,
            Sent:    sent,
            Failed:  failed,
            Error:   failed > 0 ? $"{failed} failed. Last error: {lastError}" : null
        );
    }

    // ── SendWelcomeAsync ─────────────────────────────────────────────────────
    // Branded onboarding email. Call this after inserting a new Person row.
    // Returns true on success, false on SES error (caller decides whether to throw).
    public async Task<bool> SendWelcomeAsync(string toEmail, string firstName, CancellationToken ct = default)
    {
        var html = $"""
            <h2 style="color:#001F5B;margin-top:0;">Welcome to SHPE @ GSU, {System.Web.HttpUtility.HtmlEncode(firstName)}! 🎉</h2>
            <p style="color:#0f172a;line-height:1.6;">
              You're now part of the Society of Hispanic Professional Engineers chapter
              at Georgia State University. We're glad you're here.
            </p>
            <p style="color:#0f172a;line-height:1.6;">
              <strong>What's next?</strong>
            </p>
            <ul style="color:#0f172a;line-height:1.8;">
              <li>Check upcoming events on your member dashboard</li>
              <li>Complete your profile so other members can connect with you</li>
              <li>Introduce yourself at the next meeting</li>
            </ul>
            <p style="margin-top:24px;">
              <a href="https://shpegsu.com/dashboard"
                 style="background:#FD652F;color:#fff;padding:12px 24px;border-radius:8px;
                        text-decoration:none;font-weight:bold;display:inline-block;">
                Go to Your Dashboard →
              </a>
            </p>
            <p style="color:#64748b;font-size:13px;margin-top:24px;">
              Questions? Reply to this email and a board member will get back to you.
            </p>
            """;

        var result = await SendAsync(new EmailSendRequest(
            To:       [toEmail],
            Subject:  $"Welcome to SHPE @ GSU, {firstName}!",
            Body:     $"Welcome to SHPE @ GSU, {firstName}! Log in at https://shpegsu.com/dashboard",
            HtmlBody: html,
            ReplyTo:  "president@shpegsu.com"
        ), ct);

        return result.Success;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    // Wraps arbitrary HTML content in the SHPE-branded email shell.
    // Using a method (not a file) keeps the template co-located with the sending logic
    // and avoids a dependency on a template engine for now.
    private static string BuildHtmlEmail(string subject, string bodyHtml)
    {
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="UTF-8">
              <meta name="viewport" content="width=device-width, initial-scale=1.0">
              <title>{{System.Web.HttpUtility.HtmlEncode(subject)}}</title>
            </head>
            <body style="margin:0;padding:0;background:#f4f6f9;font-family:Arial,Helvetica,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" role="presentation">
                <tr>
                  <td align="center" style="padding:40px 16px;">
                    <table width="600" cellpadding="0" cellspacing="0" role="presentation"
                           style="background:#ffffff;border-radius:12px;overflow:hidden;
                                  box-shadow:0 2px 8px rgba(0,31,91,0.1);max-width:600px;width:100%;">

                      <!-- Header -->
                      <tr>
                        <td style="background:#001F5B;padding:24px 32px;">
                          <p style="margin:0;font-size:26px;font-weight:800;color:#FD652F;
                                    letter-spacing:-0.5px;">SHPE</p>
                          <p style="margin:4px 0 0;font-size:11px;color:rgba(255,255,255,0.5);
                                    letter-spacing:2px;text-transform:uppercase;">
                            @ Georgia State University
                          </p>
                        </td>
                      </tr>

                      <!-- Subject bar -->
                      <tr>
                        <td style="background:#0070C0;padding:10px 32px;">
                          <p style="margin:0;font-size:13px;font-weight:600;color:#ffffff;">
                            {{System.Web.HttpUtility.HtmlEncode(subject)}}
                          </p>
                        </td>
                      </tr>

                      <!-- Body -->
                      <tr>
                        <td style="padding:32px;">
                          {{bodyHtml}}
                        </td>
                      </tr>

                      <!-- Footer -->
                      <tr>
                        <td style="background:#001040;padding:20px 32px;text-align:center;">
                          <p style="margin:0;font-size:11px;color:rgba(255,255,255,0.3);">
                            Society of Hispanic Professional Engineers · Georgia State University
                          </p>
                          <p style="margin:6px 0 0;font-size:11px;color:rgba(255,255,255,0.2);">
                            You're receiving this because you're a SHPE @ GSU member.
                          </p>
                        </td>
                      </tr>

                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }
}
