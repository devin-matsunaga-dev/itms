# WORK_PACKAGES.md — ITMS Build Packages

> The executable plan. One work package ≈ one Claude Code session ≈ one branch ≈ one squashed commit on `main`.

## Session model

- **One session per work package.** Small adjacent packages may be bundled (say so explicitly at kickoff: "do WP-2.6 and WP-2.7 together"). Never carry a session across a phase boundary.
- **Continuity lives in the steering files, not in chat history.** `STATUS.md` says where the build is; `DECISIONS.md` says what has already been settled. A fresh session with no memory of the last one must be able to continue correctly. If that stops being true, the steering files are wrong and fixing them is the priority.
- **Lifecycle:** branch → new session → scope gate → build → verify → append `DECISIONS.md` → stop → human reviews, merges, tags.

## Package protocol

Every session follows this, without being reminded:

1. Load `ARCHITECTURE.md`, `CONVENTIONS.md`, `DESIGN.md` (if the package touches UI), `STATUS.md`, `DECISIONS.md`, and this file's entry for the current WP.
2. **State the scope summary and stop.** List what will be built, what will not, which files will be created or changed, and any dependency that needs approval. Wait for "go". Do not write code before "go".
3. Build only what the package specifies. Something out of scope but worth doing goes into `STATUS.md` under *In flight / noticed*, not into the diff.
4. Finish with the **Package Completion Report**: what was built, files added/changed, decisions made, dependencies added and why, anything deferred, the regression command to run, and a numbered **manual verification checklist** the human can walk through in the browser.
5. Update `STATUS.md` (mark this WP done, point Current WP at the next one with its branch name) and append any decisions to `DECISIONS.md` as one-liners: "chose X over Y because Z (WP-N.N, date)". If no decisions were made, say so explicitly.
6. **Then stop.** Do not start the next package. The human verifies, commits, merges, and opens the next session.

**Never modify without an explicit instruction in the current WP:** auth configuration, credential handling, the outbox/bus wiring, migration history from earlier packages, or `DESIGN.md` tokens.

**`[SENSITIVE]`** marks packages the human reviews line by line: anything touching auth, permissions, credentials, SLA math, audit integrity, or import writes.

---

# Phase 0 — Foundation

### WP-0.1 — Solution skeleton
Create `ITMS.sln` targeting `net10.0` with the project layout from `CONVENTIONS.md`: AppHost, ServiceDefaults, Web.Host, Platform, Contracts, empty module projects, four test projects. `Directory.Build.props` carries nullable/implicit-usings/warnings-as-errors. `.gitignore`, `.editorconfig`.
**Done when:** `dotnet build` succeeds, `dotnet test` runs (zero tests is fine), no project targets anything but `net10.0`.

### WP-0.2 — Aspire orchestration
AppHost wiring PostgreSQL 17+, Redis, and MailHog with persistent volumes; Web.Host registered with health endpoints and OpenTelemetry via ServiceDefaults; connection strings flow from Aspire, none hardcoded.
**Done when:** `aspire run` brings up the dashboard with every resource healthy; the API responds on `/health`.

### WP-0.3 — Platform & Contracts
`Platform`: `Result<T>`, `IClock`, `ICurrentUser`, paging primitives, `ProblemDetails` mapping, validation filter, CSV helpers. `Contracts`: the domain event base type and the public lookup interfaces named in `ARCHITECTURE.md` §3.
**Done when:** `Itms.ArchitectureTests` exists and asserts Platform references no module; the test passes and runs in CI.

### WP-0.4 — In-process bus + transactional outbox
Outbox table, publisher that enrolls in the caller's transaction, background dispatcher with retry and idempotency by event ID, handler registration by convention.
**Done when:** an integration test writes an entity and an event in one transaction, the dispatcher delivers it exactly once, a forced consumer failure retries without duplicating side effects, and a rolled-back transaction publishes nothing.

### WP-0.5 — Identity, roles, cookie auth `[SENSITIVE]`
ASP.NET Core Identity on Postgres; Admin/Technician/User roles; cookie auth per `ARCHITECTURE.md` §7; login, logout, current-user endpoint, password change; lockout and password policy; seeded admin plus one technician and one user for dev.
**Done when:** each role logs in; an unauthenticated call to a protected endpoint returns 401 not a redirect-to-HTML; role policies are enforced server-side; session revocation works; no token is written to browser storage.

### WP-0.6 — Minimal directory (departments & locations)
Department entity plus a Location entity carrying the Organization → Site → Building → Floor/Area → Room hierarchy as a self-referencing tree with a materialized path. CRUD endpoints, seed data, no UI yet beyond what admin needs.
**Done when:** the tree persists and queries a full path efficiently; deleting a location with children is rejected with a clear error.

### WP-0.7 — Audit spine `[SENSITIVE]`
Append-only audit table, `IAuditWriter`, an event consumer that audits every domain event, field-level diff capture, actor and IP recorded. Login success and failure audited.
**Done when:** logging in and failing to log in both write rows; the table has no update or delete code path anywhere in the solution; a test asserts that.

### WP-0.8 — React shell `[UI]`
Vite + React 19 + TS strict + Tailwind + shadcn/ui + lucide. Implement the tokens, sidebar, topbar, page frame, and auth flow from `DESIGN.md` against `docs/design/reference-dashboard.png`. Routing with protected routes and role-filtered nav. TanStack Query provider, toast system, skeleton and empty-state primitives.
**Done when:** the shell is visually indistinguishable from the reference in structure, palette, type, and density; nav filters by role; login/logout work end to end; keyboard focus is visible throughout.

### WP-0.9 — OpenAPI + generated client + CI
OpenAPI document at build; client types generated into the React app; GitHub Actions running build, unit tests, integration tests with Testcontainers, architecture tests, `ruff`/`mypy`, and the frontend build.
**Done when:** CI is green on `main`, hand-written API types are absent, and a contract change that breaks the client fails the build.

🏁 **Phase 0 gate** — see `ROADMAP.md`. Tag `v0.1-phase0`.

---

# Phase 1 — Helpdesk

### WP-1.1 — Reference data: categories & priorities
Configurable ticket categories (seeded per `SPEC.md` §2) and priorities with response/resolution targets. Endpoints and seed migration.
**Done when:** renaming a category propagates to existing tickets by ID; deleting one in use is blocked.

### WP-1.2 — Ticket domain & numbering
Ticket entity with the full field set, sequential human-readable numbering (`TKT-####`) generated safely under concurrency, requester/department/category/priority/status required at creation.
**Done when:** 500 concurrent creations produce 500 unique sequential numbers with no gaps or collisions.

### WP-1.3 — Ticket state machine `[SENSITIVE]`
`New → Assigned → In Progress → Waiting → Resolved → Closed`, `Cancelled` from any pre-Resolved state, reopen from Resolved to In Progress, Closed terminal. Enforced in the entity; illegal transitions return 409.
**Done when:** every legal and illegal transition is unit-tested, and the API rejects illegal ones even when the client sends them directly.

### WP-1.4 — Ticket history
Every status, priority, assignment, and resolution change writes a history entry in the same transaction as the change, with actor, timestamp, from-value, and to-value.
**Done when:** a test asserts a failed transaction leaves no orphan history row; the detail page renders a coherent timeline.

### WP-1.5 — Create, list, detail endpoints
Creation with validation; list with filtering (status, priority, category, assignee, department, requester, date range), sorting, and paging; detail with history, comments, and links.
**Done when:** list queries project directly to DTOs, use no lazy loading, and stay under 200ms on 50,000 seeded tickets.

### WP-1.6 — Assignment & reassignment
Assign, reassign, and unassign to a technician; assignment writes history and raises `TicketAssigned`.
**Done when:** assigning moves New → Assigned automatically; reassigning an in-progress ticket preserves status.

### WP-1.7 — Comments, internal notes, attachments `[SENSITIVE]`
Public comments visible to the requester, internal notes visible only to Technician and Admin, attachments with the upload rules from `CONVENTIONS.md`.
**Done when:** a User fetching their own ticket receives no internal notes in the API payload — verified at the API level, not just the UI; an attachment cannot be fetched by a user without access to its ticket.

### WP-1.8 — Basic SLA `[SENSITIVE]`
Per-priority response and resolution targets from ticket creation; Waiting pauses the resolution clock; approaching (80%) and breached flags computed against `IClock`.
**Done when:** clock-controlled tests cover pause/resume across multiple Waiting periods, priority changes mid-flight, and the exact boundary at 80% and 100%.

### WP-1.9 — Helpdesk UI: queue & list `[UI]`
Ticket list per `DESIGN.md`: dense table, URL-synced filters, saved default views (My tickets, Unassigned, Overdue), priority dots, status pills, skeletons, empty state.
**Done when:** it matches the reference table treatment; filters survive a page reload and are linkable.

### WP-1.10 — Helpdesk UI: detail & create `[UI]`
Ticket detail with header pills, properties panel, comment/note composer with a clear visual distinction, attachment list, history timeline, and the transition buttons — illegal transitions not rendered. Create form with the New Ticket button in the sidebar.
**Done when:** the whole Phase 1 gate walkthrough is doable in the browser without touching the API directly.

🏁 **Phase 1 gate.** Tag `v0.2-phase1`.

---

# Phase 2 — Assets, directory, relationships

### WP-2.1 — Asset domain & lifecycle
Asset entity with identification, assignment, and lifecycle fields; asset types and statuses as configurable reference data; asset tag unique and immutable; serial unique per manufacturer where present.
**Done when:** attempts to change an asset tag are rejected at the domain level; duplicate tags return 409 with a useful message.

### WP-2.2 — Asset history
Assignment, transfer, repair, return to service, retirement — each an explicit domain method writing history in the same transaction, raising `AssetAssigned` / `AssetStatusChanged`.
**Done when:** transferring between two users produces exactly one history entry with both parties, and both user pages read correctly afterward.

### WP-2.3 — Asset list, detail, endpoints
Filtering by type, status, department, location, assigned user, warranty window; search by tag, serial, hostname; paging and sorting.
**Done when:** warranty-expiring-within-N-days is a first-class filter and matches the dashboard tile.

### WP-2.4 — Directory: full hierarchy & pickers
Complete department and location management, cascading location picker, moving a subtree, usage counts before deletion.
**Done when:** moving a building between sites updates every descendant path in one transaction.

### WP-2.5 — Relationships: user ↔ asset ↔ ticket
`IAssetLookup`, `IUserLookup`, `ITicketLookup` contract implementations; ticket ↔ asset link; user detail aggregating assigned assets, open tickets, and past tickets; asset detail showing ticket history.
**Done when:** the architecture test still passes — Helpdesk references no Assets assembly — and the aggregated user page is a single round trip per panel.

### WP-2.6 — Asset UI `[UI]`
Asset list, detail with the history timeline, create/edit forms, assign/transfer/retire actions with confirmation.
**Done when:** status and lifecycle actions use the semantic colors from `DESIGN.md` and illegal actions are absent rather than disabled-in-place.

### WP-2.7 — User & directory UI `[UI]`
User list, user 360 page (profile, assets, open tickets, history), department and location management screens.
**Done when:** the spec's acceptance shape holds — search a user, immediately see equipment and support history.

🏁 **Phase 2 gate.** Tag `v0.3-phase2`.

---

# Phase 3 — Monitoring & alerts

### WP-3.1 — Monitoring domain
Monitored device as a projection over assets with hostname, IP, monitoring enabled, poll interval, failure threshold, SNMP settings. Check-result table with the retention and index strategy from `ARCHITECTURE.md` §4.
**Done when:** monitoring cannot create a device that is not an asset; a test asserts it.

### WP-3.2 — Poller: ICMP `[SENSITIVE]`
Python service: pulls device configuration from the host, async ICMP checks with jitter and timeouts, POSTs results to the ingestion endpoint with a service credential.
**Done when:** `ruff` and `mypy --strict` are clean; killing the poller mid-cycle loses no committed results; the service credential is not in the repo.

### WP-3.3 — Ingestion & device state machine
Authenticated bulk ingestion endpoint; consecutive-failure threshold before declaring offline; one success restores; transitions raise `DeviceWentOffline` / `DeviceRecovered`.
**Done when:** flapping at the threshold boundary is covered by tests and does not produce alert storms.

### WP-3.4 — Availability, latency, outage history
Rollup hosted service (raw 30 days → hourly aggregates), availability percentage, latency series, outage list, 24h/7d/30d query endpoints.
**Done when:** availability computed from raw and from rollups agree for an overlapping window.

### WP-3.5 — SNMP (read-only) `[SENSITIVE]`
sysName, sysDescr, sysUpTime, manufacturer/model where derivable, interface list with operational status. Read-only enforced; no write path exists.
**Done when:** `snmpsim` devices report correctly; a grep for SNMP set operations across the repo returns nothing; unreachable SNMP degrades gracefully without affecting ICMP state.

### WP-3.6 — Alert lifecycle
Alert entity with device, type, severity, start, status, resolution, duration; offline alerts raised from device events; recovery alerts pair with and close the originating alert; location context captured at raise time.
**Done when:** a device that goes down and up produces one alert with an accurate duration, not two orphans.

### WP-3.7 — Alert → Ticket `[SENSITIVE]`
One action creating a ticket pre-populated with device, location, timestamp, and recent monitoring context, permanently linked to the alert in both directions.
**Done when:** the created ticket carries readable context in its description, the link renders on both records, and creating a second ticket from the same alert is prevented or explicitly confirmed.

### WP-3.8 — Monitoring & alerts UI `[UI]`
Device list with live status, device detail with the three time-range views, alert feed matching the reference alert list treatment, and the Alert → Ticket button.
**Done when:** the alert feed matches `docs/design/reference-dashboard.png` in icon treatment, severity coloring, and relative-time formatting.

🏁 **Phase 3 gate.** Tag `v0.4-phase3`.

---

# Phase 4 — Knowledge, search, notifications

### WP-4.1 — Knowledge base
Article entity with title, category, content, author, last updated, Draft/Published; sanitized rich text; ticket linking; seeded procedures from `SPEC.md` §9.
**Done when:** Users cannot retrieve drafts through any endpoint; sanitization is tested against a stored-XSS payload.

### WP-4.2 — Global search backend
Postgres full-text plus trigram, one search endpoint spanning ticket number and title, user and email, asset tag and serial, hostname and IP, returning grouped and related results with permission filtering applied per role.
**Done when:** searching a serial returns the asset, its user, and its tickets grouped; a User's search never surfaces another person's ticket; p95 under 300ms on the seeded dataset.

### WP-4.3 — Global search UI `[UI]`
The topbar search pill opens a keyboard-driven palette with grouped results, recent searches, and direct navigation.
**Done when:** it is fully operable from the keyboard and matches the reference search-pill treatment.

### WP-4.4 — In-app notifications
Notification entity, event consumers for assignment, comments, SLA approaching/exceeded, alerts and recovery; unread counts fanned out over SignalR with polling fallback; the bell badge and notification panel.
**Done when:** counts are correct after a socket drop and reconnect, and marking read is idempotent.

### WP-4.5 — Email notifications
Templated email for ticket created, assigned, technician response, resolved; sent from an outbox consumer so email failures never roll back business transactions; MailHog in dev; per-user opt-out.
**Done when:** an SMTP outage retries and does not lose or duplicate messages, and no email is sent inside a database transaction.

🏁 **Phase 4 gate.** Tag `v0.5-phase4`.

---

# Phase 5 — Dashboard, reports, import/export, administration

### WP-5.1 — Dashboard backend
One efficient endpoint per tile group: ticket counters with deltas, tickets by status and priority, recent/open tickets, asset status summary, upcoming expirations, recent alerts. Permission-scoped.
**Done when:** the whole dashboard loads in one round trip per card, under 500ms server time on 10,000 tickets and 2,000 assets.

### WP-5.2 — Dashboard UI `[UI]`
Build the reference screen: KPI row, Tickets Overview donut with legend, Recent Alerts, Open Tickets table, Assets Status donut, Upcoming Expirations. Every tile clicks through to its filtered list.
**Done when:** placed beside `docs/design/reference-dashboard.png` the structure, palette, spacing, and density match, and every counter agrees with the list it links to.

### WP-5.3 — Quick actions
New Ticket, New Asset, Find Asset, Find User wired to the right destinations from the dashboard and sidebar.

### WP-5.4 — Helpdesk reports
Opened/closed over time, by category, department, technician; average resolution time; SLA compliance. Date-range and filter controls.
**Done when:** report numbers reconcile exactly with the equivalent filtered ticket lists.

### WP-5.5 — Asset & monitoring reports
Assets by type, department, location, status; warranty expiration; unassigned assets. Offline devices, availability, recent outages, frequently unavailable devices.

### WP-5.6 — CSV export
Streaming export for every report and every major table, respecting the current filters, with correct escaping and a UTF-8 BOM for Excel.
**Done when:** a 100,000-row export streams without buffering the whole set in memory.

### WP-5.7 — CSV import `[SENSITIVE]`
Asset and user import with the columns from `SPEC.md` §16: upload, parse, preview, validate, detect duplicates, per-row error report, all-or-nothing commit.
**Done when:** a deliberately dirty 200-row file previews correctly, blocks duplicates, reports errors per row with line numbers, and commits nothing on failure.

### WP-5.8 — Administration `[SENSITIVE]`
Manage users and roles; departments and locations; ticket categories and priorities; asset types and statuses; monitoring configuration; notification settings. Admin-only, every change audited.
**Done when:** a non-Admin cannot reach any admin endpoint even with a hand-crafted request, and every configuration change appears in the audit log.

### WP-5.9 — Audit log viewer
Filterable, paged, read-only view over the audit table with the field-level diff rendered legibly. Admin only.
**Done when:** the UI offers no edit or delete affordance and the API exposes no write route.

🏁 **Phase 5 gate.** Tag `v0.6-phase5`.

---

# Phase 6 — Production hardening

### WP-6.1 — Security hardening `[SENSITIVE]`
HTTPS via Nginx, HSTS and security headers, CSRF verification, rate limits on login/reset/search, a validation sweep over every endpoint, dependency audit.

### WP-6.2 — Observability
Structured logging with correlation IDs across host and poller, health and readiness endpoints, key operational metrics, log levels correct for production.

### WP-6.3 — Data safety `[SENSITIVE]`
Migration and rollback procedure, nightly `pg_dump` with retention, off-box copy, and a **restore rehearsal actually performed and documented**.
**Done when:** last night's backup has been restored into a scratch database and logged into. Not "when the script exists."

### WP-6.4 — Performance pass
Index review against the real query set, N+1 sweep, dashboard and list query tuning, frontend bundle split, seeded load test at target volumes.

### WP-6.5 — Seed & demo data
Clean separation between migrations, reference-data seeds, and demo data. Production deploys never carry demo rows.

### WP-6.6 — Deployment & runbook
`aspire publish` Compose hardened for production, deployment runbook, operator README, upgrade and rollback procedure, first-run admin setup.
**Done when:** a clean VM is deployed from the runbook alone, with no step discovered along the way that is not written down.

🏁 **Phase 6 gate → V1.** Tag `v1.0`. Then stop building and start listening.
