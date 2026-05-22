# Deployment Guide

Covers CI/CD, staging, and production environments.

---

## Environments

| Env | URL | DB | Purpose |
|---|---|---|---|
| Local dev | `localhost:5001` / `:5173` | Docker Postgres | Daily development |
| Staging | `staging.shpegsu.com` | DO Managed Postgres ($15/mo) | Per-PR verification |
| Production | `shpegsu.com` | DO Managed Postgres ($30/mo) | Real members |

---

## CI/CD pipeline

See `02_PHASE_0_FOUNDATION.md` § 0.12 for the GitHub Actions workflow.

**Flow:**
1. Open PR → `ci.yml` runs (build + test + lint for both api and web)
2. PR must be green to merge
3. Merge to `main` → DO App Platform auto-deploys to staging
4. Production deploys are **manual** (separate workflow or click in DO dashboard)

**Branch protection rules:**
- `main`: requires 1 approval, CI green, no force pushes
- `production`: same, plus require 2 approvals

---

## DigitalOcean App Platform setup

### One-time setup before first deploy

1. **Account + credit:** confirm $200 student credit applied
2. **Create Project:** `shpe-gsu` in DO dashboard
3. **Create Managed Postgres:** smallest tier ($15/mo), region NYC3
   - Note the `DATABASE_URL` — App Platform auto-injects this for connected apps
4. **Create Spaces bucket:** `shpegsu-staging` in NYC3
   - Spaces → Settings → Access Keys → Generate New Key
   - Save key + secret
5. **Container Registry:** if needed for private images (App Platform builds from source so usually not required)
6. **DNS:** Cloudflare A/CNAME records pointing to App Platform default URLs

### Deploy spec

See `02_PHASE_0_FOUNDATION.md` § 0.13 for the `.do/app.yaml` file.

### First deploy

```bash
# Commit the spec
git add .do/app.yaml
git commit -m "chore(deploy): add DO App Platform spec"
git push origin main

# Then in DO dashboard:
# - Create App → "From GitHub" → select repo → it auto-reads .do/app.yaml
# - Add secrets via UI (CLERK_*, BOOTSTRAP_*, DO_SPACES_*, AWS_*, TELNYX_*)
# - Click Deploy
```

### Secrets to set in DO dashboard

These are NOT in code. Set them as encrypted env vars in App Platform → Settings → App-Level Environment Variables.

```
CLERK_ISSUER
CLERK_SECRET_KEY                ← used by future server-side Clerk operations
BOOTSTRAP_SUPER_ADMIN_EMAIL
DO_SPACES_KEY
DO_SPACES_SECRET
AWS_ACCESS_KEY_ID
AWS_SECRET_ACCESS_KEY
TELNYX_API_KEY
GSU_API_KEY                     ← when GSU sends credentials
GSU_API_BASE_URL                ← when GSU confirms endpoint
VITE_CLERK_PUBLISHABLE_KEY      ← attached to the static site component, not the API
```

### Migrations on deploy

App Platform builds the Docker image. The migration runs on startup automatically because of this in `Program.cs`:

```csharp
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
```

For production, set `ASPNETCORE_ENVIRONMENT=Production` and run migrations as a separate pre-deploy job — never auto-migrate prod.

---

## Domain + DNS

### Domain: `shpegsu.com`

Registered, Cloudflare DNS in front. Existing records (Vercel, Resend, SES) coexist with App Platform routing — be careful not to overwrite.

### DNS records for App Platform

| Type | Host | Value |
|---|---|---|
| CNAME | `staging` | (App Platform default URL — DO gives you this) |
| CNAME | `staging-api` | (same) |
| A / CNAME | apex `@` | (production app — set after prod app exists) |
| CNAME | `api` | (same) |

### SES DNS records

Already added to Cloudflare:
- 3 CNAMEs under `_domainkey` (DKIM verification)
- TXT `_dmarc` (DMARC policy)
- TXT for SPF when MAIL FROM domain is configured

---

## Production launch checklist

Do not flip the prod DNS until **all** are true:

- [ ] All 5 verticals fully polished (handed off + accepted)
- [ ] SES production access granted
- [ ] Telnyx 10DLC campaign approved (brand + campaign)
- [ ] Penetration test complete with all admin endpoints verified non-admin returns 403
- [ ] Faculty advisor TCPA + FERPA sign-off received
- [ ] Backup verified: take a manual DB backup and restore to a temp DB
- [ ] Monitoring: Sentry projects created and DSNs set
- [ ] Rate limits tuned for production volume (likely tighten `comms` further)
- [ ] Retention cleanup tested in staging
- [ ] `BOOTSTRAP_SUPER_ADMIN_EMAIL` set for production (NOT the same as staging — use Alex's GSU email)
- [ ] Production Spaces bucket created with separate access keys (`shpegsu-prod`)
- [ ] Production Postgres sized appropriately (db-s-1vcpu-1gb or larger)
- [ ] DNS for apex domain prepared (changeable in < 5 min when ready)
- [ ] Smoke tests passing in staging end-to-end
- [ ] Demo recording made before launch for handoff to next year's team

---

## Rollback plan

If production breaks:

1. **App Platform: Revert** — DO supports one-click rollback to previous deployment
2. **DB migration broken:** restore from last automated backup (DO does these hourly), then redeploy with the migration reverted
3. **External dep down (SES/Telnyx):** the app handles 502s gracefully; users see "service unavailable, try again later" Problem responses. No data loss.

---

## Monitoring

### Sentry

`apps/api/Program.cs`:

```csharp
// Sentry — captures unhandled exceptions and surfaces them with stack traces.
// Free tier: 5K errors/month — fine for staging + early prod.
builder.WebHost.UseSentry(o =>
{
    o.Dsn = builder.Configuration["SENTRY_DSN"];
    o.Environment = builder.Environment.EnvironmentName;
    o.TracesSampleRate = 0.1;  // 10% of requests get full traces
});
```

`apps/web/src/main.tsx`:

```typescript
import * as Sentry from '@sentry/react';

Sentry.init({
  dsn: import.meta.env.VITE_SENTRY_DSN,
  environment: import.meta.env.MODE,
  tracesSampleRate: 0.1,
});
```

### Health checks

DO App Platform pings `/api/health` every 10s. Three failures → marks unhealthy → triggers alert.

### Manual log inspection

```bash
# Tail API logs
doctl apps logs <app-id> --component api --follow

# Postgres slow queries
doctl databases logs <db-id>
```
