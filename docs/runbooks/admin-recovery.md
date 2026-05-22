# Admin Recovery Runbook

Use this when all super-admin accounts are lost and no one can access the admin panel.
Three recovery hatches, in order of preference.

---

## Hatch 1 — Bootstrap env var (preferred)

Works as long as you can redeploy the API with a new environment variable.

1. Set `BOOTSTRAP_SUPER_ADMIN_EMAIL=<your-email>` in the production environment
   - DigitalOcean App Platform: App → Settings → App-Level Environment Variables
2. Redeploy (push a commit, or trigger a manual deploy)
3. Have that email address sign up through the normal flow
4. `PeopleService.CheckBootstrapSuperAdminAsync` runs on signup and grants
   `super_admin`, `admin`, and `member` to that person
5. Verify via psql: `SELECT role FROM person_role WHERE person_id = '<your-id>';`
   — should show 3 rows
6. Unset the env var and redeploy again (prevents accidental re-trigger)

**Why it's safe:** The service checks `AnyAsync(r => r.Role == Roles.SuperAdmin)` before
granting anything. Once a super_admin row exists, this code path exits immediately on every
subsequent call — even if the env var still matches. You can't accidentally bootstrap twice.

---

## Hatch 2 — Admin CLI tool

Not yet built. Planned for `tools/AdminCli/` in Phase 2.4.

When it exists: `dotnet run --project tools/AdminCli grant-super-admin <email>`

---

## Hatch 3 — Raw SQL (last resort)

Use only if Hatches 1 and 2 are unavailable. Requires direct database access.

```sql
-- Step 1: find the person's ID
SELECT id, email, full_name FROM person WHERE email = 'your@email.com';

-- Step 2: grant the three roles (replace <person-uuid> with the ID from Step 1)
INSERT INTO person_role (id, person_id, role, assigned_at)
VALUES
  (gen_random_uuid(), '<person-uuid>', 'super_admin', now()),
  (gen_random_uuid(), '<person-uuid>', 'admin',       now()),
  (gen_random_uuid(), '<person-uuid>', 'member',      now());

-- Step 3: write an audit log entry so the change is on record
INSERT INTO audit_log (id, actor_person_id, action, target_type, target_id, details, at)
VALUES (
  gen_random_uuid(),
  '<person-uuid>',
  'MANUAL_SUPER_ADMIN_GRANT',
  'person',
  '<person-uuid>',
  'Granted via raw SQL during admin recovery — see runbook',
  now()
);
```

Connect via `make psql` (local) or the DigitalOcean managed database console (production).

---

## After recovery

Once any super-admin is back:
1. Audit the `audit_log` table for anything suspicious during the outage period
2. Rotate any API keys or secrets that may have been exposed
3. Update this runbook if the recovery process revealed a gap
