# CONVENTIONS.md — Code Standards

> Binding. Read before writing code in any session. These are not preferences to be re-argued per work package.

## Repository layout

```
it-platform/
├─ CLAUDE.md                  # points Claude Code at docs/SESSION.md
├─ ITMS.sln
├─ docs/                      # steering files (this folder)
│  └─ design/reference-dashboard.png
├─ src/
│  ├─ Itms.AppHost/           # Aspire orchestration
│  ├─ Itms.ServiceDefaults/   # Aspire shared config
│  ├─ Itms.Web.Host/          # ASP.NET Core host, composition root only
│  ├─ Itms.Platform/          # shared kernel — references no module
│  ├─ Itms.Contracts/         # public interfaces + domain events
│  ├─ Modules/
│  │  ├─ Itms.Modules.Identity/
│  │  ├─ Itms.Modules.Directory/
│  │  ├─ Itms.Modules.Helpdesk/
│  │  ├─ Itms.Modules.Assets/
│  │  ├─ Itms.Modules.Monitoring/
│  │  ├─ Itms.Modules.Alerts/
│  │  ├─ Itms.Modules.Knowledge/
│  │  ├─ Itms.Modules.Search/
│  │  ├─ Itms.Modules.Notifications/
│  │  ├─ Itms.Modules.Reporting/
│  │  └─ Itms.Modules.Audit/
│  ├─ Itms.Web.Client/        # React 19 + Vite
│  └─ itms-poller/            # Python service
└─ tests/
   ├─ Itms.ArchitectureTests/
   ├─ Itms.UnitTests/
   ├─ Itms.IntegrationTests/
   └─ itms-poller-tests/
```

Inside a module, organize by **feature**, not by layer:

```
Itms.Modules.Helpdesk/
├─ Features/Tickets/{Create,Assign,ChangeStatus,List,GetById}/
├─ Domain/          # entities, value objects, state machine
├─ Persistence/     # DbContext, configurations, migrations
├─ Contracts/       # implementations of Itms.Contracts interfaces
└─ HelpdeskModule.cs
```

## .NET

- Target **net10.0** everywhere, `LangVersion` latest. Never scaffold `net8.0`/`net9.0`.
- `Nullable` enabled, implicit usings on, **warnings as errors**, file-scoped namespaces, `sealed` by default.
- One `Directory.Build.props` at the repo root holds these settings; individual `.csproj` files stay nearly empty.
- Vertical slices: one folder per feature holding request, handler, validator, and endpoint. No generic `IRepository<T>`, no service-layer-over-everything, no AutoMapper — map explicitly.
- Validation with **FluentValidation**, executed by endpoint filter. Handlers assume valid input.
- Return `Result<T>` from handlers; endpoints translate to HTTP. Exceptions are for the genuinely exceptional, never for control flow.
- Entities keep private setters and expose intent-named methods (`ticket.Assign(technicianId, actor)`), not anemic property bags. Invariants live in the entity.
- EF Core: explicit `IEntityTypeConfiguration<T>` per entity, `AsNoTracking()` on all reads, projections to DTOs in the query — never load an aggregate to render a list. No lazy loading, ever.
- Async everywhere with `CancellationToken` threaded through to the database call.
- Logging via `ILogger<T>` with structured properties (`logger.LogInformation("Ticket {TicketNumber} assigned to {TechnicianId}", …)`). No string interpolation into log messages, no `Console.WriteLine`.
- Never log secrets, passwords, session cookies, SNMP community strings, or full request bodies.

## Naming

- Commands are imperative (`CreateTicket`), queries are `GetX`/`ListX`, events are past tense (`TicketCreated`).
- Database: snake_case tables and columns, plural tables, `id` primary keys, `<entity>_id` foreign keys, explicit index names (`ix_tickets_assignee_status`).
- API routes: kebab-case plural (`/api/v1/tickets/{id}/status-changes`).
- Git branches: `feat/wp-X.Y-short-name`. Commits: Conventional Commits with the WP tag — `feat(helpdesk): ticket state machine (WP-1.3)`.

## Frontend

- TypeScript strict; `any` is a review failure. API types are **generated from OpenAPI** — never hand-written.
- Feature folders under `src/features/<module>/` with `components/`, `hooks/`, `api/`, `routes/`. Truly shared pieces go in `src/components/ui` (shadcn) and `src/lib`.
- Server state is TanStack Query only. Client state is component state or a small Zustand store — no Redux, no context-as-state-manager.
- No `useEffect` for data fetching. No manual `fetch` in components.
- All styling through Tailwind tokens defined in `DESIGN.md`. No raw hex outside the token config.
- Every list screen: URL-synced filters, sorting, and paging; skeletons while loading; a real empty state; an error state with retry.
- Forms: react-hook-form + zod, schema shared with the display layer, server errors mapped back onto fields.
- Accessibility is part of "done": labeled inputs, keyboard-reachable actions, focus trap in dialogs, `aria-live` for toasts.

## Python (poller)

- Python 3.13 minimum (3.14 preferred), `asyncio` throughout, **type hints mandatory**, `ruff` + `mypy --strict` clean.
- `uv` for dependency management; pinned lockfile committed.
- Configuration from environment only; no config files with credentials, no hardcoded hosts.
- Structured JSON logging to stdout. The container never writes log files.
- Every network call has a timeout. No unbounded retries — exponential backoff with a ceiling.

## Testing

The testing rules exist to keep the suite fast enough that it actually gets run every package.

- **Unit tests** for domain logic: state machines, SLA calculation, priority rules, availability math, CSV validation. These are pure, in-memory, and must run in well under a second each.
- **Integration tests** for endpoints and persistence, using **Testcontainers** for PostgreSQL. Reuse **one container per test assembly**, not per test — spinning a container per test is the single most common way these suites become unusable.
- Reset state between tests by truncating tables via Respawn, not by re-running migrations.
- No `Thread.Sleep` in tests. Poll with a timeout helper, or inject the clock. `IClock` exists in `Platform` precisely so time can be controlled.
- Frontend: Vitest + Testing Library for component behavior. Playwright only for the handful of critical end-to-end paths (log in → create ticket → assign → resolve; alert → ticket). Do not build a large E2E suite in V1.
- Every bug fixed gets a test that fails without the fix.
- Target: the full `dotnet test` run stays under two minutes on a dev machine. If it drifts past that, fixing it is the next work package.

## Security floor

- Parameterized queries only (EF handles this; raw SQL must use parameters).
- Output encoding by default; no `dangerouslySetInnerHTML` except for sanitized KB article content, which goes through an allowlist sanitizer server-side.
- File uploads (ticket attachments): allowlist of extensions, size cap, content-type sniffing, stored outside the web root with generated names, served through an authorized endpoint that re-checks permission.
- Rate limits on login, password reset, and search endpoints.
- CSRF protection enabled for cookie auth.
- Secrets come from environment or user-secrets in dev. Nothing sensitive in `appsettings.json`, ever.
- Dependencies: Dependabot on; review at every phase gate.

## Documentation

- Public contract interfaces and domain events carry XML doc comments explaining *why* they exist.
- Anything non-obvious gets a comment explaining the reason, not the mechanics. Do not narrate code.
- README stays current with: prerequisites, `aspire run`, seed data, default login, and how to run tests.
