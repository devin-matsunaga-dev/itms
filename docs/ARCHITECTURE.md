# ARCHITECTURE.md — Unified IT Management System (ITMS)

> Read this fully before writing any code. It is binding. If a work package requires changing something here, updating this file is part of that package and the change gets logged in `DECISIONS.md`.

## 1. What this system is

A production-scoped IT operations platform combining **Helpdesk**, **IT Asset Management**, and **Basic Device Monitoring**, plus the connective tissue that makes them one product: users, departments, locations, alerts, knowledge base, global search, notifications, reports, audit logging.

The core operating loop the system must serve end to end:

> Issue reported → ticket managed → user/device identified → technician investigates → device status checked → work documented → issue resolved → history retained.

**The asset record is the backbone.** Tickets, alerts, monitored devices, and users all reference the same asset rows. Nothing gets its own parallel copy of "the device."

### Scope discipline

`SPEC.md` §"V1 Now vs. V2+ Later" is the scope boundary. Anything in the Defer column is out — no change management, no procurement, no NetFlow, no automated remediation, no event correlation engine, no AD/Entra sync, no BI report builder, no granular RBAC. If a work package seems to want one of those, it is misread — stop and ask.

## 2. Topology

A **modular monolith** in one ASP.NET Core host, plus one Python poller service, orchestrated by .NET Aspire.

```
┌── Aspire AppHost ───────────────────────────────────────────┐
│                                                             │
│  Itms.Web.Host (ASP.NET Core, net10.0)                      │
│    ├─ Modules.Identity      users, roles, auth, sessions    │
│    ├─ Modules.Directory     departments, locations          │
│    ├─ Modules.Helpdesk      tickets, comments, SLA          │
│    ├─ Modules.Assets        assets, assignment, lifecycle   │
│    ├─ Modules.Monitoring    devices, checks, results        │
│    ├─ Modules.Alerts        alerts, alert→ticket            │
│    ├─ Modules.Knowledge     KB articles                     │
│    ├─ Modules.Search        global search                   │
│    ├─ Modules.Notifications in-app + email                  │
│    ├─ Modules.Reporting     operational reports + CSV       │
│    ├─ Modules.Audit         audit log (cross-cutting)       │
│    └─ Platform              shared kernel, no module refs   │
│                                                             │
│  Itms.Web.Client (React 19 + Vite, served/proxied)          │
│  Itms.Poller (Python 3.13+, ICMP + read-only SNMP)          │
│  PostgreSQL 17+   ·   Redis   ·   MailHog (dev SMTP)        │
└─────────────────────────────────────────────────────────────┘
```

Why a monolith: V1 has one team, one database, one deployment target, and heavy cross-module querying (a technician screen shows a user's assets *and* their tickets *and* their device state). Microservices would buy nothing and cost transactions. The module boundaries are enforced in code so extraction stays possible later.

The poller is separate because ICMP and SNMP are far better served by Python's ecosystem (`icmplib`, `pysnmp`) than by .NET, and because polling load should never be able to stall the web host.

## 3. Module rules (enforced, not aspirational)

1. A module owns its tables. No other module writes them, and no other module queries them directly.
2. Cross-module reads go through the owning module's **public contract** interface, defined in `Itms.Contracts` and implemented inside the owning module. `Modules.Helpdesk` never references `Modules.Assets` — it references `IAssetLookup`.
3. Cross-module reactions go through **domain events** on the in-process bus (see §5). No module calls another module's command handler directly.
4. `Platform` holds only genuinely shared primitives: result types, clock abstraction, pagination, current-user accessor, validation helpers, CSV utilities. `Platform` never references a module.
5. Every module exposes exactly one `AddXxxModule(IServiceCollection)` and one `MapXxxEndpoints(IEndpointRouteBuilder)`.
6. Denormalized display data (requester name on a ticket, asset tag on an alert) is stored as an ID plus a cached display string that is refreshed on the owning entity's change event. Never a foreign key across module boundaries in the schema.

An architecture test in `tests/Itms.ArchitectureTests` asserts rules 1, 2, and 4 by inspecting assembly references. It runs in CI and its failure is a build failure.

## 4. Data

- **PostgreSQL 17+** is the only durable store. One database, one schema per module (`helpdesk`, `assets`, `monitoring`, …), one connection string.
- **EF Core** with per-module `DbContext`, each configured to its own schema and its own migrations history table. Migrations are per module and never edited after merge.
- **Monitoring check results** are the only high-volume table. Store raw results for 30 days; roll up to hourly availability/latency aggregates beyond that; the rollup job is a hosted service. Do not add TimescaleDB in V1 — plain Postgres with a BRIN index on `(device_id, checked_at)` is sufficient at this scale, and it is one less thing to operate. Revisit only if measured.
- **Redis** holds cache, the in-app notification fan-out, and rate-limit counters. Nothing in Redis is a source of truth; the system must survive a Redis flush.
- Every table carries `created_at`, `created_by`, `updated_at`, `updated_by` (UTC, `timestamptz`). Deletes are soft (`deleted_at`) for tickets, assets, users, and KB articles; hard for check results and expired sessions.
- All timestamps are stored UTC and converted at the edge. No exceptions.

## 5. Eventing

An **in-process bus with a transactional outbox**. A handler writes its state change and its outbound events in one database transaction; a background dispatcher publishes them.

- Events are past-tense facts in `Itms.Contracts`: `TicketCreated`, `TicketAssigned`, `TicketStatusChanged`, `TicketResolved`, `AssetAssigned`, `AssetStatusChanged`, `DeviceWentOffline`, `DeviceRecovered`, `AlertRaised`, `AlertResolved`, `UserDeactivated`.
- Consumers are idempotent and keyed on the event ID. Redelivery must be harmless.
- Audit entries, notifications, search index updates, and alert-to-ticket context are all built by consuming events — not by calling into those modules inline.
- No message broker in V1. The outbox is the durability mechanism. If the system later splits, the outbox is the seam.

The poller does not publish to the bus. It POSTs check results to an internal, service-authenticated endpoint on the host; the Monitoring module turns state transitions into events. This keeps state-transition logic in one place and the poller stateless.

## 6. API

- REST under `/api/v1`, minimal APIs grouped per module, versioned in the route from day one.
- Resource-plural routes, cursor-or-offset paging (`?page=&pageSize=`, max 200), consistent envelope `{ items, total, page, pageSize }`.
- Errors are RFC 7807 `ProblemDetails`, always. Validation failures are 400 with per-field errors; forbidden is 403 and never a 404 disguise except where enumerating IDs would leak.
- Optimistic concurrency on tickets and assets via `xmin`/rowversion returned as an ETag; conflicting writes get 409.
- OpenAPI generated at build; the React client's types are generated from it. Hand-written API types on the client are a review failure.
- SignalR hub for live updates (dashboard counters, ticket comments, alert feed). Live updates are an enhancement — every screen must be correct with polling alone if the socket drops.

## 7. Authentication & authorization

- Cookie-based auth with ASP.NET Core Identity, HttpOnly + Secure + SameSite=Lax, sliding expiry, server-side session revocation. **No JWT in the browser, no tokens in localStorage.**
- Three roles only: **Admin**, **Technician**, **User**. Their boundaries are in `SPEC.md` §14.
- Authorization is policy-based, evaluated server-side on every endpoint. The React app hides what a role cannot use; hiding is never the enforcement.
- Row-level rule that must be tested explicitly: a **User** may read and comment on tickets where they are the requester, and nothing else. They cannot see internal notes on their own tickets.
- Password policy, lockout, and password reset flows use Identity defaults hardened per `CONVENTIONS.md`. Local accounts only in V1 — no external IdP, no LDAP.
- The poller authenticates with a service credential scoped to one endpoint, held in configuration/secrets, never in the database.

## 8. Audit

Auditing is cross-cutting and built in Phase 0, not retrofitted. The Audit module consumes domain events and, for the mutations that do not warrant a domain event, an `IAuditWriter` called from the module's handler. Every entry records: actor, action, entity type, entity ID, timestamp, source IP, and a before/after diff of changed fields only. Audit rows are append-only — no update path, no delete path, no admin UI that can edit them.

Mandatory coverage: logins (success and failure), ticket modifications, asset modifications, assignment changes, administrative configuration changes, user and role changes.

## 9. Monitoring & the poller

- Python 3.13+, asyncio, one process, configuration pulled from the host at startup and refreshed on an interval.
- ICMP checks via `icmplib` (unprivileged sockets where possible), SNMP via `pysnmp` **read-only** — no SNMP writes exist in this codebase, and no write community string is ever accepted in configuration.
- Poll interval per device, default 60s, jittered to avoid thundering herds. Configurable failure threshold (default 3 consecutive failures) before a device is declared offline; one success restores it.
- SNMP scope is deliberately narrow: sysName, sysDescr, sysUpTime, manufacturer/model where derivable, and interface list with operational status. No traffic counters, no utilization graphs, no config pulls.
- The poller is stateless and restart-safe. Losing it loses monitoring, never data integrity.

## 10. Versions & environments

Pinned to LTS or latest-supported. Never scaffold or pull base images for EOL versions.

| Tech | Pinned | Notes |
|---|---|---|
| .NET SDK | **10 (LTS)** | Supported to Nov 2028. Never `net8.0`/`net9.0` — both EOL Nov 2026 |
| Aspire | **13.x (latest)** | Only the latest release is supported; `aspire update` at every phase gate |
| Node.js | **24 LTS** | Node 26 enters LTS Oct 2026; optional hop then |
| Python | **3.13 minimum, 3.14 preferred** | Depends on `pysnmp`/`icmplib` cleanliness |
| React / Vite | **19 / latest** | Scaffold from current `create-vite`; never an old template |
| PostgreSQL | **17+** | |
| Ubuntu (WSL + prod) | **LTS (24.04 or 26.04)** | Match dev and prod |

- **Dev:** WSL2 on Ubuntu LTS, everything started with `aspire run`. MailHog for SMTP, `snmpsim` for fake SNMP devices, a small set of seeded unreachable IPs for offline testing.
- **Prod:** single Linux VM, Docker Engine, Compose generated by `aspire publish` and hardened by hand (pinned image tags, named volumes, restart policies, resource limits). Nginx terminates TLS. Nightly `pg_dump` to off-box storage with a **restore procedure that has actually been executed once** before go-live.

## 11. Invariants

These are true at all times. A change that breaks one is a bug, not a trade-off.

1. A ticket always has a requester, a category, a priority, and a status.
2. Ticket status transitions follow the state machine in `SPEC.md` §2. Illegal transitions are rejected server-side with 409, not merely hidden in the UI.
3. Every meaningful ticket change (status, priority, assignee, resolution) writes a history entry in the same transaction as the change.
4. An asset tag is unique and immutable once created. Serial numbers are unique per manufacturer where present.
5. Assigning an asset to a user writes an asset-history entry; so does transfer, repair, return to service, and retirement.
6. A monitored device is always an asset. Monitoring cannot create device records of its own.
7. An alert always references a device and carries its location context at the time it was raised.
8. A ticket created from an alert is permanently linked to that alert and carries device, location, and timestamp context into its description.
9. Deactivating a user never deletes their tickets, comments, or asset history.
10. Audit entries are never modified or deleted through any code path in this system.
11. All times are stored UTC.
12. Nothing in the Defer column of `SPEC.md` gets built in V1.
