# SHPE @ GSU — Claude Code Master Guide

## Read this first

You are Claude Code, working on the SHPE @ Georgia State University member management web app. The lead developer (Alex Henriquez) is bootstrapping this project as the foundation for a team of 6 developers who will join on **June 1, 2026**.

Your job: help build the **foundation (Phase 0)** before the team arrives, then scaffold the verticals (Phase 1) for the team to flesh out, then build the cross-cutting systems (Phase 2) end-to-end.

## Hard rules — read carefully

These are non-negotiable. Other devs will copy your patterns.

1. **Every file you create must have explanatory comments.** Alex is learning. For every non-trivial block of code, add a comment explaining:
   - **What** it does (one line)
   - **Why** it exists (one or two lines)
   - **Gotchas** if any (caching behavior, ordering requirements, side effects)

   Bad comment: `// open a connection`
   Good comment: `// Open a fresh Postgres connection — EF reuses connections via the pool, but migrations need an exclusive one to acquire the lock table`

2. **Snake-case column names in Postgres.** Always. Never `FullName` in the DB — `full_name`. Use Fluent API in `Data/Configurations/`, never `[Attribute]` annotations on entity classes.

3. **One configuration file per entity.** Add a `Person.cs`? You also add `PersonConfiguration.cs`. Without exception.

4. **Soft-delete pattern.** Soft-deletable entities get a nullable `DeletedAt` field + `HasQueryFilter(x => x.DeletedAt == null)` in their configuration. So queries auto-exclude deleted rows.

5. **Never invent secrets.** When a step needs an API key (Telnyx, SES, Clerk, etc.), use a placeholder like `REPLACE_WITH_TELNYX_KEY` in `.env.example` and clearly mark it. Never put a fake key in committed code.

6. **Test after each step.** Run `make up`, hit the relevant endpoint, verify migrations. Don't move on with broken code.

7. **Use the role filter, never inline role checks.** Correct: `app.MapPost(...).RequireAdmin()`. Wrong: `if (!ctx.HasRole("admin")) return Forbid()` inside the handler.

8. **Audit log writes:** middleware handles generic mutations automatically. Services write to `db.AuditLogs` directly only for **specific actions** that need richer context (merging duplicates, bootstrapping super-admin, role changes).

9. **One step at a time.** Each step has a definition-of-done. Meet it, verify it, commit, move on. Don't batch steps unless they're trivially small.

10. **When stuck, ask Alex.** Don't guess at infrastructure decisions Alex hasn't made (which region, which package version, etc.). Pause and ask.

11. **Commit messages follow conventional commits:**
    - `feat(auth): add Clerk JWT middleware`
    - `fix(events): correct timezone handling on event create`
    - `chore(ci): add dotnet test to pipeline`
    - `docs(readme): update setup instructions`

12. **Update `01_PROJECT_STATE.md` at the end of every working session.** Mark steps done, note what's in progress, note any blockers.

## Workflow for each step

1. Read the relevant section of the phase doc
2. Confirm with Alex what step you're on (e.g. "Working on 0.6 — Audit log middleware")
3. Create/modify files with full inline comments
4. Run the verification command (usually `make up` + a curl, or `make migrate-up` + `make psql`)
5. Commit with a clean message
6. Update `01_PROJECT_STATE.md` to mark the step done
7. Tell Alex what's next

## Files in this guide

Read these in order:

1. **`00_CLAUDE_CODE_README.md`** — this file
2. **`01_PROJECT_STATE.md`** — what's already built, what's left, current step
3. **`02_PHASE_0_FOUNDATION.md`** — backend foundation
4. **`03_PHASE_0_5_FRONTEND.md`** — frontend infrastructure
5. **`04_PHASE_1_SCAFFOLDING.md`** — vertical scaffolds for the team
6. **`05_PHASE_2_INTEGRATIONS.md`** — GSU API, SES, Telnyx, Quartz jobs
7. **`06_DEPLOYMENT.md`** — CI/CD + DigitalOcean staging + production
8. **`07_PLACEHOLDERS.md`** — every API key/secret, where to get it, where to put it
9. **`08_GLOSSARY.md`** — quick reference (JWKS, RFC 7807, A2P 10DLC, etc.)

## When Alex's token limit hits

Save state to `01_PROJECT_STATE.md`. When work resumes (next day, next session), the new session reads that file and picks up exactly where the last one stopped. The state file is the source of truth.

## Phase summary

- **Phase 0** — backend foundation (week 1-2). Alex builds. Solo.
- **Phase 0.5** — frontend infrastructure (week 2). Alex builds. Solo.
- **Phase 1** — vertical scaffolding (weeks 3-4). Alex scaffolds 5 verticals, then hands off to the 5 other devs.
- **Phase 2** — cross-cutting systems (weeks 5-10). Alex builds end-to-end while other devs polish their verticals.
- **Polish + deploy** (weeks 11-12). Security audit, production deploy.
