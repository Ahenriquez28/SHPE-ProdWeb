# Placeholders + External Dependencies

> Every API key, secret, or external resource the project needs. For each: where to get it, where to put it, and what's blocking it.

---

## Critical path — needed before code goes to staging

### 🟡 1. Clerk JWT template

**What:** A template named `default` that the API client requests via `getToken({ template: 'default' })`.

**Where to get:** Clerk dashboard → JWT Templates → New template

**Configuration:**
- Name: `default`
- Token lifetime: 60 minutes
- Custom claims (recommended): `email`

**Where to put:** No code change. Just create it in Clerk dashboard. Your existing publishable + secret keys already work.

---

### 🟡 2. GSU PantherCard API

**Status:** **🔴 BLOCKING — email NOT YET SENT.**

**What's needed:**
- Bearer token for staging/dev use
- Sample response payload (real or anonymized)
- Confirmed endpoint path + query parameter
- Confirmed rate limit (assumption: 60 req/min/IP)

**Where to get:** Email the GSU API contact. CC faculty advisor. Escalate via advisor if no reply in 7 days.

**Where to put:**

`.env`:
```bash
GSU_API_BASE_URL=https://...
GSU_API_KEY=...
```

App Platform secrets in DO dashboard: same names.

**Without this:** check-in QR scan flow stays stubbed; Dev 4 builds UI against the stub backend. Phase 2.1 cannot proceed.

---

### 🟡 3. AWS SES production access

**Status:** Sandbox mode active. Daily cap 200 emails, only verified addresses.

**Blocked on:** SES domain verification of `shpegsu.com` (DNS records added, waiting propagation).

**Once verified:** Click "Request production access" in SES dashboard. Fill out:
- Mail type: Transactional
- Website URL: https://shpegsu.com
- Use case: student organization sending event reminders + announcements to opted-in members
- Process for handling complaints: complaints flip person `email = null` and audit-log; STOP via Telnyx separately handles SMS

**Where to put credentials:**

`.env`:
```bash
AWS_ACCESS_KEY_ID=...
AWS_SECRET_ACCESS_KEY=...
AWS_SES_REGION=us-east-1
AWS_SES_FROM_EMAIL=noreply@shpegsu.com
```

IAM user: create one specifically for SES with policy `AmazonSESFullAccess`. Don't use root keys.

**Without this:** Phase 2.2 email sending cannot launch.

---

### 🟡 4. Telnyx 10DLC

**Status:** Brand submitted, awaiting carrier approval (1-3 business days). Campaign not yet created.

**Once brand approved:**
1. In Telnyx portal → Compliance → 10DLC → Campaigns → Create Campaign
2. Brand: select the approved brand
3. Vertical: Education
4. Use case: Mixed
5. Sample messages (you submitted these already with brand registration)
6. Pay campaign fee (~$10)

**Once campaign approved:** Assign your number `+1-470-672-8573` to the campaign.

**Where to put credentials:**

`.env`:
```bash
TELNYX_API_KEY=...
TELNYX_PHONE_NUMBER=+14706728573
```

**Without this:** SMS sending is throttled / blocked by carriers regardless of code quality.

---

### 🟡 5. DigitalOcean Spaces

**What:** S3-compatible blob storage for file uploads.

**Where to get:**
1. DO dashboard → Create → Spaces Object Storage
2. Region: NYC3
3. Name: `shpegsu-staging` (separate `shpegsu-prod` for production)
4. CORS: allow origin `https://staging.shpegsu.com` and `http://localhost:5173`
5. Generate access keys: Spaces → Settings → Access Keys → Generate New Key

**Where to put:**

`.env`:
```bash
DO_SPACES_KEY=...
DO_SPACES_SECRET=...
DO_SPACES_BUCKET=shpegsu-staging
DO_SPACES_REGION=nyc3
```

**Without this:** file upload endpoints return 500 (Spaces client can't init).

---

### 🟡 6. DigitalOcean App Platform + Managed Postgres

**What:** the production hosting for API + web + database.

**Where to get:**
1. DO dashboard → Apps → Create App → From GitHub
2. Connect repo, select `main` branch
3. App Platform auto-detects `.do/app.yaml`
4. Create the Managed Postgres separately first (smallest tier — $15/mo)

**Where to put:** Set all secrets in App Platform UI (Settings → App-Level Environment Variables). Mark each as encrypted.

---

### 🟡 7. Sentry (optional but recommended)

**What:** error monitoring.

**Where to get:**
1. Sign up at sentry.io
2. Create org `shpe-gsu`
3. Create projects: `shpe-gsu-api` (platform: .NET), `shpe-gsu-web` (platform: React)
4. Copy each project's DSN

**Where to put:**

`.env`:
```bash
SENTRY_DSN=https://...
VITE_SENTRY_DSN=https://...  # separate DSN for the web project
```

---

## Summary table

| Item | Status | Action | Blocker |
|---|---|---|---|
| Clerk JWT template `default` | 🟡 To do | Create in dashboard | None |
| GSU API | 🔴 Blocked | Send email today | No reply yet |
| AWS SES production | 🟡 Blocked | Wait for DNS, then click button | Domain verification |
| Telnyx 10DLC campaign | 🟡 Blocked | Wait for brand approval | Carrier review |
| DO Spaces bucket | 🟡 To do | Create + generate keys | None |
| DO App Platform | 🟡 To do | Create + connect repo | None |
| DO Managed Postgres | 🟡 To do | Create (smallest tier) | None |
| Sentry | 🟢 Nice-to-have | Create projects, copy DSNs | None |

---

## What Claude Code should do when a key is missing

1. **Never invent a fake key.** Use `REPLACE_WITH_<thing>` in `.env.example`.
2. **Code defensively.** Wrap external API calls in try/catch so missing creds fail with a clear log message, not a cryptic stack trace.
3. **Add a comment in the relevant Service:** `// Requires <env var>; see 07_PLACEHOLDERS.md`
4. **If a step truly cannot proceed without the key:** stop and ask Alex.

---

## What Alex should fill in by what date

| Date | What |
|---|---|
| May 19 (today) | Send GSU API email |
| May 19-20 | Create Clerk JWT template |
| May 19-20 | Create DO Spaces bucket |
| ~May 21 | Brand approval expected → start campaign registration |
| ~May 23 | SES DNS verified → request production access |
| May 24-31 | Create DO App Platform + Managed Postgres |
| ~May 31 | Telnyx campaign approval expected |
| ~May 31 | SES production access expected |
| Jun 1 | Team kickoff. All keys should be in App Platform secrets. |
