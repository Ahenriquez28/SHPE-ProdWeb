# Project State

> **Update this file at the end of every Claude Code session.** This is the handoff between sessions — when Alex's token limit refreshes and a new session starts, it reads this file to know exactly where to pick up.

## Last updated

`2026-05-22` — Fixed git (init in project root, pushed to GitHub), built and verified 0.12 CI.

## Current step

**🟡 0.13 — Staging deploy on DigitalOcean** (next up)

## Phase 0 checklist

- [x] **0.1** Docker Compose + Makefile + `.env.example`
- [x] **0.2** .NET API skeleton (Program.cs, folder structure)
- [x] **0.3** EF Core + Postgres (models + configurations + Init migration)
- [x] **0.4** Clerk JWT middleware
- [x] **0.5** Role + permission system
- [x] **0.11** Health check + Swagger (done as part of 0.2)
- [x] **0.6** Audit log middleware — verified: audit_log rows written on POST
- [x] **0.7** Bootstrap super-admin — PeopleService + Roles.cs + runbook
- [x] **0.8** File upload infrastructure — FileRecord, FileService, FilesEndpoints, RequireRoleFilter, migration applied
- [x] **0.9** Shared error handling — RFC 7807, ProblemDetailsResults.cs, UseStatusCodePages; verified traceId in 404 body
- [x] **0.10** Rate limiting — 4 policies (default/auth/comms/checkin); verified 429 + Retry-After: 60
- [x] **0.12** CI/CD — GitHub Actions workflow, restore+build passes green ✅
- [ ] **0.13** Staging deploy on DigitalOcean ← **NEXT**

## Phase 0.5 checklist

- [ ] Vite + React + TypeScript + Tailwind setup
- [ ] Shared API types
- [ ] Typed API client
- [ ] Layout components (Public, Member, Admin)
- [ ] UI primitives
- [ ] Routing + role-gated routes

## Phase 1 checklist

- [ ] 1.1 Public site scaffold (Dev 2)
- [ ] 1.2 Events scaffold (Dev 3)
- [ ] 1.3 Check-in scaffold (Dev 4)
- [ ] 1.4 Member portal scaffold (Dev 5)
- [ ] 1.5 Admin internals scaffold (Dev 6)

## Phase 2 checklist

- [ ] 2.1 GSU API integration + real Aztec check-in
- [ ] 2.2 Comms (SES email + Telnyx SMS + webhooks)
- [ ] 2.3 Retention cleanup job (Quartz.NET)
- [ ] 2.4 Security audit + polish + prod deploy

## Confirmed data model decisions

- `Person.GradYear` is `string?` (e.g. "May 2027"), NOT `int?`
- `Person` does NOT include `GsuVerified` or `Major`
- Models exist: `Person`, `PersonRole`, `AuthAccount`, `AuditLog`
- All tables verified in Postgres via `\dt`

## Infrastructure state

| Item | Status |
|---|---|
| Telnyx account | ✅ Created |
| Telnyx number `+1-470-672-8573` | ✅ Purchased |
| Telnyx messaging profile | ✅ Created |
| Telnyx 10DLC brand | 🟡 Submitted, waiting carrier approval (1-3 business days) |
| Telnyx 10DLC campaign | ⛔ Blocked on brand approval |
| AWS account | ✅ Created |
| AWS SES region | ✅ us-east-1 |
| AWS SES sending domain `shpegsu.com` | 🟡 DNS records added, waiting verification |
| AWS SES production access | ⛔ Blocked on domain verification |
| AWS SES verified test email `ahenriquez200528@gmail.com` | ✅ Verified |
| Clerk app "SHPE @ GSU" | ✅ Created |
| Clerk Google OAuth | ✅ Enabled |
| Clerk JWT template `default` | 🟡 Not yet created |
| DigitalOcean account | ✅ Created with $200 student credit |
| DigitalOcean Spaces bucket | 🟡 Not yet created |
| DigitalOcean App Platform | 🟡 Not yet created |
| Domain `shpegsu.com` | ✅ Owned, Cloudflare DNS configured |
| GitHub repo `Ahenriquez28/SHPE-ProdWeb` | ✅ Created |
| GitHub branch protection | 🟡 Blocked on first commit |
| GitHub team invites | 🟡 Need usernames before June 1 |
| GSU API contact email | 🔴 NOT SENT — HIGHEST PRIORITY |
| Slack/Discord workspace | 🟡 Not yet created |
| Welcome email to devs | 🟡 Not yet sent |
| Wednesday calendar invites | 🟡 Not yet sent |

## Files in repo currently

```
SHPE-ProdWeb/
├── apps/
│   ├── api/
│   │   ├── Models/
│   │   │   ├── Person.cs
│   │   │   ├── PersonRole.cs
│   │   │   ├── AuthAccount.cs
│   │   │   ├── AuditLog.cs
│   │   │   └── Roles.cs
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Configurations/
│   │   │   │   ├── PersonConfiguration.cs
│   │   │   │   ├── PersonRoleConfiguration.cs
│   │   │   │   ├── AuthAccountConfiguration.cs
│   │   │   │   └── AuditLogConfiguration.cs
│   │   │   └── Migrations/ (Init migration applied)
│   │   ├── Services/
│   │   │   └── ClerkJwtVerifier.cs
│   │   ├── Middleware/
│   │   │   └── ClerkAuthMiddleware.cs
│   │   ├── Filters/
│   │   │   └── RequireRoleFilter.cs
│   │   ├── Contracts/ (empty)
│   │   ├── Endpoints/ (empty)
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── Dockerfile
│   │   └── SHPE-api.csproj (.NET 10)
│   └── web/ (empty)
├── docs/ (planning docs go here)
├── tools/ (empty)
├── docker-compose.yml
├── Makefile
└── .env.example
```

## Verified working

- `make up` boots Postgres + API
- `localhost:5001/api/health` returns 200 OK
- `localhost:5001/swagger` loads
- `make migrate-up` applies migrations
- `make psql` drops into shell; tables: `person`, `person_role`, `auth_account`, `audit_log`, `__EFMigrationsHistory`

## Files in repo currently

```
apps/api/
├── Models/
│   ├── Person.cs
│   ├── PersonRole.cs
│   ├── AuthAccount.cs
│   ├── AuditLog.cs
│   └── Roles.cs           ← NEW (0.7)
├── Data/
│   ├── AppDbContext.cs
│   └── Configurations/
│       ├── PersonConfiguration.cs
│       ├── PersonRoleConfiguration.cs
│       ├── AuthAccountConfiguration.cs
│       └── AuditLogConfiguration.cs
├── Services/
│   ├── ClerkJwtVerifier.cs
│   └── PeopleService.cs   ← NEW (0.7)
├── Middleware/
│   ├── ClerkAuthMiddle.cs
│   └── AuditLogMiddleware.cs
├── Migrations/ (Init migration applied)
├── Program.cs
├── appsettings.json
└── Dockerfile
docs/
└── runbooks/
    └── admin-recovery.md  ← NEW (0.7)
```

## Session notes

Use this section to record decisions made mid-session, debugging context, anything weird that future sessions need to know.

### 2026-05-22 (session 2)
- Git fixed: `git init` inside SHPE-ProdWeb, remote set to Ahenriquez28/SHPE-ProdWeb, force-pushed.
- All 55 files committed in one initial commit `ca4d5a1`.
- `.gitignore` created: excludes bin/, obj/, .env, node_modules/, .claude/settings.local.json.
- CI workflow added at `.github/workflows/ci.yml` — restore+build only (no test projects yet).
  First attempt failed with 0 jobs (YAML complexity); simplified to lean restore+build → green ✅.
- 0.13 (staging deploy) requires DigitalOcean Spaces bucket + App Platform to exist first.
- Step 0.6 was already coded; verified by adding temp POST endpoint, confirmed row in audit_log, removed temp endpoint.
- Step 0.7 built: Roles.cs (static constants), PeopleService + IPeopleService (bootstrap logic), registered in Program.cs, runbook at docs/runbooks/admin-recovery.md.
- `dotnet watch` hot-reloads code changes inside the Docker container without a full restart. Program.cs top-level changes show a warning but apply immediately.

### 2026-05-19
- .NET 10 chosen over .NET 8 (Alex already has 10.0.101 installed locally)
- Port 5000 conflicts with macOS Control Center; API now exposes on 5001
- EF Core CLI installed inside Docker container so `make migrate-up` works through docker compose exec
- `Microsoft.AspNetCore.OpenApi` was missing locally; added with `--version 10.0.1`
