# SHPE @ GSU — Claude Code Master Context

> This file is auto-read by Claude Code every session. It is the bridge between Opus (architecture) and Claude Code (execution). Never skip it.

## Project

SHPE @ Georgia State University — member management web app.

- **Lead dev:** Alex Henriquez
- **Team of 6 starts:** June 1, 2026
- **Your role:** Dev 1 (Lead). You build the foundation, scaffold all 5 verticals, build all cross-cutting systems.

## Stack

| Layer | Tool |
|---|---|
| Backend | .NET 10, ASP.NET Core Minimal APIs |
| ORM | EF Core 10 + Npgsql |
| Database | Postgres 16 (Docker locally, DO Managed in prod) |
| Auth | Clerk (JWT via JWKS) |
| File storage | DigitalOcean Spaces (S3-compatible) |
| Email | Amazon SES |
| SMS | Telnyx (10DLC registered) |
| Background jobs | Quartz.NET |
| Frontend | React 18 + Vite + TypeScript + Tailwind v4 |
| Local dev | Docker Compose |
| CI/CD | GitHub Actions |
| Hosting | DigitalOcean App Platform |

## Repo location

```
~/Desktop/SHPE-ProdWeb/
├── apps/api/        ← .NET 10 backend (SHPE-api)
├── apps/web/        ← React frontend
├── docs/            ← planning docs
├── .claude/         ← sub-agent definitions, commands
├── CLAUDE.md        ← this file
├── docker-compose.yml
├── Makefile
└── .env.example
```

## Current state — read before doing anything

Read `docs/claude-code/01_PROJECT_STATE.md` for the live checklist.

**Current step as of last session: 0.6 — Audit log middleware**

## Hard rules — non-negotiable

1. **Heavy comments on every file.** Explain what AND why AND gotchas. Alex is learning.
2. **Snake-case in Postgres.** `full_name` not `FullName`. Fluent API in `Data/Configurations/` always.
3. **One configuration file per entity.** Add `Event.cs`? Add `EventConfiguration.cs`. No exceptions.
4. **Soft-delete pattern.** Nullable `DeletedAt` + `HasQueryFilter(x => x.DeletedAt == null)`.
5. **Never invent secrets.** Use `REPLACE_WITH_<thing>` placeholders in `.env.example`.
6. **Role filter, never inline.** `app.MapPost(...).RequireAdmin()` not `if (!HasRole("admin"))` in handler.
7. **Audit log:** middleware handles generic mutations. Services write directly for specific actions.
8. **Test after each step.** `make up` + hit endpoint + verify migrations. Don't move on broken.
9. **Conventional commits:** `feat(auth): add Clerk JWT middleware`
10. **Update `01_PROJECT_STATE.md`** at end of every session — mark done, note next step.
11. **Never stop and ask for permission.** Make decisions autonomously. When there are options, pick the best one, do it, and tell Alex what you chose and why. Never wait for a yes/no answer.

## Step workflow

1. Read `docs/claude-code/01_PROJECT_STATE.md`
2. Confirm current step with Alex
3. Write files with full comments
4. Run verification (`make up`, curl, `make migrate-up`, `make psql`)
5. Commit
6. Update `01_PROJECT_STATE.md`

## Sub-agents available

Defined in `.claude/agents/`. Claude Code auto-delegates when matching:
- `backend-builder` — .NET API work (models, services, endpoints, middleware)
- `frontend-builder` — React/TypeScript work (pages, components, hooks, types)
- `db-manager` — migrations, schema, Postgres operations
- `devops` — Docker, CI/CD, DigitalOcean deployment

## Key docs

- Full phase-by-phase guide: `.claude/docs/02_PHASE_0_FOUNDATION.md`
- Frontend guide: `.claude/docs/03_PHASE_0_5_FRONTEND.md`
- Vertical scaffolds: `.claude/docs/04_PHASE_1_SCAFFOLDING.md`
- Integrations (GSU, SES, Telnyx): `.claude/docs/05_PHASE_2_INTEGRATIONS.md`
- Deployment: `.claude/docs/06_DEPLOYMENT.md`
- API keys needed: `.claude/docs/07_PLACEHOLDERS.md`
- Glossary: `.claude/docs/08_GLOSSARY.md`
- Original scaffolding spec: `docs/LEAD_SCAFFOLDING.md`

## Verified working (as of 2026-05-19)

- `make up` boots Postgres + API
- `localhost:5001/api/health` returns 200
- `localhost:5001/swagger` loads
- Tables in Postgres: `person`, `person_role`, `auth_account`, `audit_log`
- .NET 10, Docker 29.x, Git 2.44 all installed
