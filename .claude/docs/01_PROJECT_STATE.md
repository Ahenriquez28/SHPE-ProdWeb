# Project State

> **Update this file at the end of every Claude Code session.** This is the handoff between sessions — when Alex's token limit refreshes and a new session starts, it reads this file to know exactly where to pick up.

## Last updated

`2026-05-24` — Clerk + AWS SES fully integrated (backend + frontend). Auth sync live. Next: test login, then 0.13 staging deploy.

## Current step

**🟡 0.13 — Staging deploy on DigitalOcean**

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
- [x] **0.9** Shared error handling — RFC 7807, ProblemDetailsResults.cs, UseStatusCodePages
- [x] **0.10** Rate limiting — 4 policies (default/auth/comms/checkin); verified 429 + Retry-After: 60
- [x] **0.12** CI/CD — GitHub Actions workflow, restore+build passes green ✅
- [ ] **0.13** Staging deploy on DigitalOcean ← **NEXT**

## Phase 0.5 checklist (frontend scaffold)

- [x] Vite + React 19 + TypeScript + Tailwind v4 setup (`apps/web/`)
- [x] Shared API types (`src/types/api.ts`)
- [x] Typed API client + ApiException (`src/lib/api.ts`)
- [x] Three-step file upload helper (`src/lib/upload.ts`)
- [x] Color scheme: navy + cream + SHPE orange (`src/index.css`)
- [x] Layout components: PublicLayout, MemberLayout, AdminLayout
- [x] UI primitives: Button, Input, Card, Badge, Spinner, Skeleton, Modal, Toast
- [x] Auth guards: RequireAuth (Clerk redirect), RequireRole (403 message)
- [x] `useMe` hook (fetches /api/me, React Query, role-aware)
- [x] App.tsx routing (public / member / admin routes, nested)
- [x] main.tsx with ClerkProvider + QueryClientProvider
- [x] Stub pages: Home, Events, Sponsors, About, Dashboard, Profile, Directory, AdminMembers, AdminEvents, AdminEBoard, AdminSponsors
- [x] `.env.example` + `.env.local` (placeholder — needs real Clerk key)
- [ ] **BLOCKED**: Can't fully test until `VITE_CLERK_PUBLISHABLE_KEY` is set in `.env.local`

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
- Models exist: `Person`, `PersonRole`, `AuthAccount`, `AuditLog`, `FileRecord`
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
| Clerk JWT template `default` | 🟡 **Not yet created — NEEDED BEFORE AUTH WORKS** |
| Clerk publishable key in `.env.local` | ✅ Set — `pk_test_dW5...` |
| DigitalOcean account | ✅ Created with $200 student credit |
| DigitalOcean Spaces bucket `shpegsu-staging` | 🟡 Not yet created |
| DigitalOcean App Platform | 🟡 Not yet created |
| Domain `shpegsu.com` | ✅ Owned, Cloudflare DNS configured |
| GitHub repo `Ahenriquez28/SHPE-ProdWeb` | ✅ Created, CI passing ✅ |
| GitHub branch protection | 🟡 Set up when team joins June 1 |
| GitHub team invites | 🟡 Need dev usernames before June 1 |
| GSU API contact email | 🔴 **NOT SENT — HIGHEST PRIORITY** |
| Slack/Discord workspace | 🟡 Not yet created |
| Welcome email to devs | 🟡 Not yet sent |
| Wednesday calendar invites | 🟡 Not yet sent |

## Web app file tree (as of 2026-05-24)

```
apps/web/src/
├── types/api.ts            — all shared TS types (Person, Event, Sponsor, etc.)
├── lib/
│   ├── api.ts              — useApi() hook, ApiException, typed request()
│   └── upload.ts           — 3-step presigned upload helper
├── layouts/
│   ├── PublicLayout.tsx    — nav + footer (cream bg, navy nav)
│   ├── MemberLayout.tsx    — sidebar nav (navy), member area
│   └── AdminLayout.tsx     — thin wrapper, gates to admin/super_admin
├── components/
│   ├── RequireAuth.tsx     — Clerk redirect if not signed in
│   ├── RequireRole.tsx     — 403 message if wrong role
│   └── ui/
│       ├── Button.tsx      — primary/secondary/ghost/danger variants
│       ├── Input.tsx       — labeled input with error state
│       ├── Card.tsx        — white surface with optional border
│       ├── Badge.tsx       — role/status pill labels
│       ├── Spinner.tsx     — loading indicator
│       ├── Skeleton.tsx    — animated placeholder
│       ├── Modal.tsx       — overlay dialog with backdrop
│       └── Toast.tsx       — auto-dismiss notifications
├── hooks/
│   └── useMe.ts            — fetches /api/me, caches 5 min
├── pages/
│   ├── public/  (Home, Events, Sponsors, About)
│   ├── member/  (Dashboard, Profile, Directory)
│   └── admin/   (Members, Events, EBoard, Sponsors)
├── App.tsx                 — full router (public / member / admin routes)
├── main.tsx                — ClerkProvider + QueryClientProvider entry
└── index.css               — Tailwind v4 + color tokens (navy/cream/brand)
```

## Verified working

- `make up` boots Postgres + API
- `localhost:5001/api/health` returns 200 OK
- `localhost:5001/swagger` loads
- `make migrate-up` applies migrations
- Tables in Postgres: `person`, `person_role`, `auth_account`, `audit_log`, `file_record`
- TypeScript check on web: `pnpm tsc --noEmit` → 0 errors ✅
- CI on GitHub Actions: restore + build green ✅

## Session notes

### 2026-05-24 (session 4)
- Clerk fully integrated (frontend + backend).
- `.env.local`: real publishable key `pk_test_dW5...` (issuer: `https://unified-phoenix-54.clerk.accounts.dev`).
- Backend: Clerk Issuer updated in appsettings.Development.json; ClerkAuthMiddleware now sets `ctx.Items["ClerkUserId"]` on valid JWTs even before AuthAccount exists; `AuthEndpoints.cs` added with POST /api/auth/sync + GET /api/me.
- Frontend: ClerkProvider restored in main.tsx; RequireAuth uses `useAuth + useClerk.redirectToSignIn()` (v6 hook API, no deprecated components); api.ts passes `getToken` from `useAuth`; useMe has `enabled: isLoaded && isSignedIn` guard; `useAuthSync` hook fires POST /api/auth/sync once per sign-in; PublicLayout shows SignInButton/UserButton; MemberLayout shows UserButton.
- First sign-in flow: Clerk → useAuthSync → POST /api/auth/sync → finds Alex's pre-seeded Person by email → creates AuthAccount → returns super_admin/admin/member roles.
- AWS SES domain `shpegsu.com` still pending verification — emails blocked until then.
- Clerk JWT template `default` may be needed if email claim is absent from tokens.

### 2026-05-24 (session 3)
- Phase 0.5 frontend scaffold complete (all files written, tsc passes clean).
- Color scheme: navy (#1e3a5f) + cream (#faf8f5) + SHPE orange (#f48024).
- `.env.local` created with placeholder — Alex must paste real Clerk publishable key.
- Clerk JWT template `default` still needs to be created in Clerk dashboard.
- DO Spaces bucket `shpegsu-staging` still needs to be created manually.
- PROJECT_STATE.md was in wrong location (docs/claude-code/ vs .claude/docs/) — corrected path.

### 2026-05-22 (session 2)
- Git fixed: `git init` inside SHPE-ProdWeb, remote set to Ahenriquez28/SHPE-ProdWeb, force-pushed.
- All 55 files committed in one initial commit.
- `.gitignore` created: excludes bin/, obj/, .env, node_modules/, .claude/settings.local.json.
- CI workflow added at `.github/workflows/ci.yml` — restore+build only (no test projects yet).
- 0.13 (staging deploy) requires DigitalOcean Spaces bucket + App Platform to exist first.

### 2026-05-19 (session 1)
- .NET 10 chosen over .NET 8 (Alex already has 10.0.101 installed locally)
- Port 5000 conflicts with macOS Control Center; API now exposes on 5001
- EF Core CLI installed inside Docker container so `make migrate-up` works through docker compose exec
