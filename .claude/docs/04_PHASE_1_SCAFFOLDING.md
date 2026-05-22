# Phase 1 — Vertical Scaffolding

> **Goal:** Build a working skeleton for each of the 5 verticals so the owning dev can open it, see a real page calling a real endpoint with typed data, and start polishing — not starting from zero.
>
> **What "scaffolded" means** (not "done"):
> - Entities + migrations in place
> - Endpoints returning real-shaped data
> - TypeScript types defining the contract
> - Routes registered, pages render
> - Layout + component shells per feature
> - Clear TODOs marking what the owner needs to add

## Order of work

1. **1.1 Public site (Dev 2)** — fewest dependencies, quick win
2. **1.2 Events (Dev 3)** — depended on by check-in and portal
3. **1.4 Member portal (Dev 5)** — depends on Person, Event
4. **1.3 Check-in & Attendance (Dev 4)** — depends on Event, Person, Attendance
5. **1.5 Admin internals (Dev 6)** — largest surface area

> See the original `LEAD_SCAFFOLDING.md` document for the complete endpoint specs, entity definitions, and page scaffolds for each vertical. It is the authoritative source. This file points you to the right sections.

---

## 1.1 — Public site

**Owner after handoff:** Dev 2

**What you scaffold:**
- Entities: `EBoardMember`, `Sponsor`, `SponsorshipPacket`
- Configurations + migration
- Public endpoints (no auth): `/api/events?scope=public`, `/api/public/eboard`, `/api/public/sponsors`
- Real auth signup endpoint: `POST /api/auth/signup`
- Page stubs: `HomePage`, `EBoardPage`, `SponsorsPage`, `LoginPage`, `SignupPage`, `ForgotPage`, `EnrichPage`
- TCPA opt-in checkbox in signup (audit-logged)

**See full spec in `LEAD_SCAFFOLDING.md` § 1.1**

**Time estimate:** ~10 hours

**Definition of done (handoff to Dev 2):**
- All 7 public pages route correctly
- `/` loads with hero + about + calendar (real upcoming events)
- `/eboard` fetches real E-Board (seed a few rows)
- `/sponsors` fetches real sponsors
- `/signup` creates Person + member role + audit log + TCPA enforcement
- TODOs mark every place Dev 2 needs to add styling, copy, missing sections

---

## 1.2 — Events

**Owner after handoff:** Dev 3

**What you scaffold:**
- Entities: `Event`, `EventAdmin` (junction), `EventNote`, `EventFile`
- Configurations + migration
- Endpoints:
  - `GET /api/events` (with member/admin projection switch)
  - `GET /api/events/{id}`
  - `POST /api/events` (admin)
  - `PATCH /api/events/{id}` (admin)
  - `DELETE /api/events/{id}` (super_admin)
  - `GET/POST /api/events/{id}/notes` (with kind=note|todo)
  - `GET/POST /api/events/{id}/files`
- Page stubs: `AdminEventsListPage`, `AdminEventNewPage`, `EventDetailLayout` with 4 sub-tabs (Overview, Attendees, Tasks, Notes, Files)
- `EventAttendeesTab` is a stub only — Dev 4 owns it

**See full spec in `LEAD_SCAFFOLDING.md` § 1.2**

**Time estimate:** ~12 hours

**Definition of done (handoff to Dev 3):**
- Admin events list shows real events
- Create form creates events
- Detail page navigates 4 tabs
- Each tab has list + add control
- TODOs mark kanban view, markdown rendering, file gallery, attendance count badges

---

## 1.3 — Check-in & Attendance

**Owner after handoff:** Dev 4 (UI). **You own the backend GSU API integration end-to-end in Phase 2.**

**What you scaffold:**
- Entities: `Attendance`, `EnrichmentToken`, `GsuApiCache`
- Configurations + migration
- Endpoints (stub for Phase 1; real Phase 2):
  - `POST /api/events/{id}/checkin/qr` — **stub** returns fake person data so Dev 4 can build UI
  - `POST /api/events/{id}/checkin/manual` — **real** even in Phase 1
  - `GET /api/events/{id}/attendees` — real
  - `DELETE /api/events/{id}/attendees/{personId}` — real (undo)
  - `GET /api/enrich/{token}` + `POST /api/enrich/{token}` — real
- Component: `AztecScanner` using `@zxing/browser`
- Page stubs: `EventAttendeesTab` (scanner + manual), `EnrichPage`

**See full spec in `LEAD_SCAFFOLDING.md` § 1.3**

**Time estimate:** ~10 hours

**Definition of done (handoff to Dev 4):**
- Scanner UI integrates `@zxing/browser` and calls stub backend
- Manual fallback works against real backend
- Roster shows real data
- Undo works
- `/enrich/:token` renders and submits
- TODOs mark mobile polish, success animations, camera permission flow

**You come back in Phase 2.1 to replace the QR stub with real GSU API integration.**

---

## 1.4 — Member portal

**Owner after handoff:** Dev 5

**What you scaffold:**
- Endpoints (mostly real):
  - `GET /api/me` — current user's profile + roles
  - `PATCH /api/me` — update profile (SMS opt-in audit-logged)
  - `GET /api/me/dashboard` — next event + pending resume reviews
  - `GET /api/resume-reviews/mine` — own submissions
  - `POST /api/resume-reviews` — submit new (TODO Phase 2: queue SES email)
- Page stubs: `PortalDashboard`, `PortalProfile`, `PortalResumeReviews`, `PortalPanelists`, `PortalAlumni`, `PortalPhotos`, `PortalEventsListPage`, `PortalEventDetailPage`

**See full spec in `LEAD_SCAFFOLDING.md` § 1.4**

**Time estimate:** ~10 hours

**Definition of done (handoff to Dev 5):**
- All 8 portal pages route correctly
- Dashboard shows real-ish aggregate
- Profile loads/saves with SMS audit logging
- Resume submission flow works end-to-end (upload + record creation)
- Panelist/alumni lists fetch real data (filtered server-side by share_contact)
- TODOs mark lightbox, profile completeness widget, status badges, empty states

---

## 1.5 — Admin internals

**Owner after handoff:** Dev 6

**What you scaffold:**
- Entities: `CommsBlast`, `CommsRecipient`, `InternalMeeting`, `AuditLog` (already exists)
- Endpoints (sending is stub; real in Phase 2.2):
  - People directory + duplicates + person detail + merge + role mgmt
  - Comms preview (real) + send (stub)
  - Comms history + recipients
  - Internal meetings CRUD (eboard-gated)
  - E-Board management
  - Sponsors management
  - Super-admin: dashboard, history, audit log view, retention preview/run (stubbed)
- Page stubs: 9 admin pages

**See full spec in `LEAD_SCAFFOLDING.md` § 1.5**

**Time estimate:** ~15 hours (largest scaffold — pace yourself)

**Definition of done (handoff to Dev 6):**
- People directory works with filter + pagination
- Person detail loads
- Comms compose renders with audience picker; "Send" calls stub backend
- Comms history shows real (stub-sent) blasts
- Meetings list/create works (eboard-gated)
- E-Board admin CRUD works
- Sponsors admin CRUD works
- Settings page renders with section stubs (populated by Dev 1 in Phase 2)
- TODOs mark rich text editor, markdown preview, bounce/delivery detail, retention UI

---

## Handoff playbook

For each vertical:

1. Code lives in main with everything above
2. Write a 1-page `docs/handoffs/<area>.md` covering:
   - What's done (link to PRs)
   - What's left (TODOs in code)
   - Known issues / gotchas
   - Who to ping for what
3. Live demo for 15 minutes with the owning dev
4. They open a follow-up PR within 48 hours

**Suggested handoff dates:**
- End of week 3: Public site → Dev 2; Events → Dev 3
- Mid week 4: Check-in UI → Dev 4; Member portal → Dev 5
- End of week 4: Admin internals → Dev 6

---

## Definitions

**Scaffolded** = entities, real-shape endpoints (real or stubbed), TypeScript types, routes, page shells, TODOs.

**Done** = scaffolded + polished UI + validation + empty states + loading states + mobile responsive + accessibility + edge cases + staging tested + documented.

You ship "scaffolded." The other devs ship "done." Don't blur the line.
