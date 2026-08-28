# ROADMAP.md — Phases & Gates

> Reference document. `WORK_PACKAGES.md` is what sessions execute; this explains the sequence and defines the 🏁 gate at the end of each phase.

## Sequencing logic

`SPEC.md` lists a delivery order. Two deviations, both deliberate:

1. **Minimal departments and locations land in Phase 0**, not Phase 2. A user cannot be created without a department, and an asset cannot be placed without a location. Building them late means backfilling every seed and every form. Phase 0 gets the tables and CRUD; Phase 2 gets the hierarchy, the pickers, and the management UI.
2. **The audit spine lands in Phase 0**, not near the end. Auditing is a cross-cutting consumer of domain events; adding it after ten modules exist means revisiting all ten. Phase 0 builds the writer and the event consumer; each later module verifies its own coverage as part of its own work package.

Everything else follows the spec's order.

## Phase 0 — Foundation

*Goal: an empty but real application — someone can log in, see the shell, and every later package has rails to run on.*

Solution skeleton on net10.0, Aspire AppHost with Postgres/Redis/MailHog, Platform and Contracts, in-process bus with transactional outbox, Identity with the three roles, cookie auth, minimal departments and locations, audit spine, React shell matching `DESIGN.md` (sidebar, topbar, page frame, token config, shadcn theme), OpenAPI + generated client types, CI running build + tests + architecture tests.

🏁 **Gate:** fresh clone → `aspire run` → log in as each of the three seeded roles → the sidebar shows the right items per role → a login writes an audit row → a test event round-trips through the outbox → CI green on main.

## Phase 1 — Helpdesk

*Goal: the module that makes the system worth deploying.*

Ticket domain and state machine, numbering, categories and priorities as configurable reference data, create/list/detail, assignment and reassignment, status and priority changes with history, comments split into internal notes and user-visible replies, attachments, basic SLA targets with approaching/breached flags, the technician queue, and the requester's own-tickets view.

🏁 **Gate:** a User submits a ticket → a Technician picks it up, changes priority, adds an internal note and a public reply → resolves it → the requester sees the resolution and cannot see the internal note → the full history renders → every one of those steps has an audit row → an illegal transition returns 409.

## Phase 2 — Assets, directory, and relationships

*Goal: the backbone, and the connections that make it a unified system.*

Asset domain with types, identification, assignment, lifecycle statuses and asset history; asset list and detail; department and location hierarchy with pickers and management UI; the user detail page showing assigned assets plus open and past tickets; ticket ↔ asset linking; asset detail showing its ticket history.

🏁 **Gate:** create an asset → assign it to a user → open that user's page and see the asset, their open tickets, and their history → raise a ticket against the asset → see it on the asset's page → transfer the asset to another user and watch both histories update correctly.

## Phase 3 — Monitoring and alerts

*Goal: live infrastructure context, and the alert-to-ticket bridge.*

Python poller with ICMP checks, result ingestion endpoint, device state machine with failure thresholds, latency and availability history with 24h/7d/30d views, read-only SNMP for identity and interface status, alert lifecycle with offline/recovery pairing, the alert feed, and **Alert → Ticket** with full context carried across.

🏁 **Gate:** a seeded unreachable device goes offline within the threshold → an alert is raised with location context → one click creates a ticket carrying device, location, and timestamp → the device comes back → the alert auto-resolves with a duration and the recovery is visible → the 7-day availability view is correct → `snmpsim` devices report identity and interfaces.

## Phase 4 — Knowledge, search, and notifications

*Goal: the layer that makes the system feel like one product.*

Knowledge base with draft/published articles, categories, and ticket linking; global search across tickets, users, assets, hostnames and IPs, with grouped results and a keyboard-driven palette; in-app notification center; email notifications for the four ticket events via MailHog in dev.

🏁 **Gate:** searching a serial number surfaces the asset, its assigned user, and its ticket history in one result set → assigning a ticket produces both an in-app notification and an email → publishing a KB article makes it linkable from a ticket while drafts stay invisible to Users.

## Phase 5 — Dashboard, reports, import/export, administration

*Goal: the operational surface and the configuration surface.*

Dashboard matching the reference screenshot pixel-for-intent, with every tile clicking through to its filtered list; helpdesk, asset, and monitoring reports with CSV export; CSV import for assets and users with preview, validation, duplicate detection, and per-row error reporting; the administration area for users, roles, departments, locations, categories, priorities, asset types and statuses, monitoring configuration, and notification settings; the audit log viewer.

🏁 **Gate:** the dashboard matches `docs/design/reference-dashboard.png` in structure, palette, and density → every counter clicks through to the matching filtered list and the numbers agree → a deliberately dirty CSV of 200 assets imports with a preview, blocks duplicates, and reports errors per row → an Admin changes a ticket category name and it propagates → the audit log shows that change.

## Phase 6 — Production hardening

*Goal: something that can be handed to a real IT department.*

HTTPS termination and security headers, health checks and readiness probes, structured logging with correlation IDs, rate limiting, input validation sweep, database migration and rollback procedure, backup and **rehearsed** restore, seed/demo data separation, performance pass on the dashboard and list queries, dependency audit, deployment runbook, and the operator README.

🏁 **Gate:** deploy to a clean VM from the runbook alone, with no undocumented step → TLS valid, headers pass a scanner → kill the poller and confirm the app degrades without erroring → restore last night's backup into a scratch database and log in against it → load the dashboard with 10,000 tickets and 2,000 assets under 500ms server time.

## After V1

Stop. Deploy. Run it in a real department, gather operational feedback, and let that determine V2 priorities — not this document.
