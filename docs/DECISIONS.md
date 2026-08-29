# DECISIONS.md — Decision Log

> One line per settled choice: "chose X over Y because Z (WP-N.N, YYYY-MM-DD)". Appended by Claude Code at the end of every session. Never relitigated without a work package.

## Bootstrap

- Chose a modular monolith over microservices because V1 has one team, one database, and heavy cross-module querying; module boundaries are enforced in code so extraction stays possible (bootstrap, 2026-08-28)
- Chose an in-process bus with a transactional outbox over a message broker because V1 has one deployable host and the outbox provides the needed durability and the future seam (bootstrap, 2026-08-28)
- Chose plain PostgreSQL over TimescaleDB for monitoring history because raw-30-days plus hourly rollups is sufficient at this scale and is one less thing to operate; revisit only if measured (bootstrap, 2026-08-28)
- Chose a separate Python poller over in-process .NET polling because icmplib/pysnmp are the better tools and polling load must never stall the web host (bootstrap, 2026-08-28)
- Chose cookie auth with server-side sessions over browser-held JWTs because it allows real revocation and keeps no token in browser storage (bootstrap, 2026-08-28)
- Chose to move minimal departments and locations into Phase 0, ahead of the spec's stated order, because users and assets cannot be created without them and backfilling would touch every seed and form (bootstrap, 2026-08-28)
- Chose to move the audit spine into Phase 0, ahead of the spec's stated order, because auditing is an event consumer and retrofitting it after ten modules means revisiting all ten (bootstrap, 2026-08-28)
- Chose platform versions pinned to LTS or latest-supported — .NET 10, Aspire 13.x, Node 24, Python 3.13+, React 19, PostgreSQL 17+ (bootstrap, 2026-08-28)
- Chose Tailwind + shadcn/ui + lucide + Recharts as the UI stack, themed to the reference screenshot at docs/design/reference-dashboard.png (bootstrap, 2026-08-28)
- Chose the human as the merge gate: Claude Code branches and builds, never merges, pushes, or tags (bootstrap, 2026-08-28)

## WP-0.1 — Solution skeleton

- Chose xUnit v3 with Shouldly over xUnit v2 with FluentAssertions because v3 is the current xUnit line and FluentAssertions v8 is commercially licensed for non-open-source use, which this project would trip (WP-0.1, 2026-08-28)
- Chose the Microsoft.Testing.Platform runner, selected in `global.json`, over VSTest because xUnit v3 is an MTP runner and the .NET 10 SDK refuses to drive one through the VSTest target at all (WP-0.1, 2026-08-28)
- Chose central package management in `Directory.Packages.props` over per-project versions because CONVENTIONS.md requires near-empty `.csproj` files and two projects must never be able to disagree about a version (WP-0.1, 2026-08-28)
- Chose the classic `ITMS.sln` format over the .NET 10 SDK's new `.slnx` default because CONVENTIONS.md and WP-0.1 both name `ITMS.sln`; migrating later is one command plus a CONVENTIONS.md edit (WP-0.1, 2026-08-28)
- Chose an `AssemblyMarker` type in every library over locating assemblies by name string because the architecture tests and convention-based registration then fail at compile time when a project is renamed or dropped, not at runtime (WP-0.1, 2026-08-28)
- Chose to enable `EnforceCodeStyleInBuild` but elevate only the file-scoped-namespace rule to error, leaving other style rules at suggestion, because `TreatWarningsAsErrors` turns any warning-level style rule into a build break and a skeleton that fails on cosmetics is one nobody can extend (WP-0.1, 2026-08-28)
- Chose to disable CA1707 under `tests/` only, because underscored test names are how the suite stays readable and the rule is aimed at public API surface (WP-0.1, 2026-08-28)
- Chose to pin Aspire at 13.5.3 via the AppHost SDK attribute rather than a floating version because ARCHITECTURE.md §10 makes `aspire update` an explicit phase-gate step (WP-0.1, 2026-08-28)

## WP-0.2 — Aspire orchestration

- Chose to pin container image tags (`postgres:17.6`, `redis:7.4`, `mailhog/mailhog:v1.0.1`) over floating the Aspire defaults because ARCHITECTURE.md §10 requires a dev machine and a production compose file to run the same version, and a silent minor bump is the kind of drift nobody notices until it breaks (WP-0.2, 2026-08-28)
- Chose `ContainerLifetime.Persistent` plus named data volumes for PostgreSQL and Redis over recreating them each run because migrations and seed data have to survive a session, and container startup is the slowest part of `aspire run` (WP-0.2, 2026-08-28)
- Chose to leave MailHog session-lifetime and volume-less over persisting captured mail because it is dev SMTP capture with nothing worth keeping across a restart, and a maildir volume is one more thing to reason about (WP-0.2, 2026-08-28)
- Chose MailHog as a plain `AddContainer` resource over adopting `CommunityToolkit.Aspire.Hosting.MailPit` because ARCHITECTURE.md §10 and WP-0.2 both name MailHog; MailHog being archived upstream is recorded in STATUS.md as a swap to consider before the Notifications module depends on it (WP-0.2, 2026-08-28)
- Chose Redis 7.4 over 8.x because ARCHITECTURE.md pins PostgreSQL but is silent on Redis, and 7.4 is the Aspire default and the conservative choice for V1 (WP-0.2, 2026-08-28)
- Chose to have `Itms.Web.Host` actually consume the Aspire connection strings via `Aspire.Npgsql` and `Aspire.StackExchange.Redis` over merely receiving them because an unconsumed connection string proves nothing and leaves `/health` green while PostgreSQL is down; verified by stopping each container and watching `/health` return 503 (WP-0.2, 2026-08-28)
- Chose to bind the MailHog SMTP endpoint to `Smtp__Host`/`Smtp__Port` configuration keys over Aspire's `WithReference` service-discovery variables because SMTP has no connection-string convention and the Notifications module will read plain configuration (WP-0.2, 2026-08-28)
- Chose to leave the ServiceDefaults health endpoints Development-only rather than exposing them in production, because opening them is a security decision that belongs to Phase 6 and not to an orchestration package (WP-0.2, 2026-08-28)
- Chose to test the Aspire application model with `Aspire.Hosting.Testing` in Publish mode over starting the distributed application because Publish mode resolves connection strings to unresolved expressions, so the wiring is asserted without a Docker daemon and the suite stays inside the two-minute budget in CONVENTIONS.md (WP-0.2, 2026-08-28)
- Chose to assert configuration hygiene with a test that scans every checked-in `appsettings*.json` over trusting review because "no connection strings in the repository" is a rule that only holds if something enforces it (WP-0.2, 2026-08-28)

## WP-0.3 — Platform & Contracts

- Chose two independent sealed types, `Result` and `Result<T>`, over `Result<T> : Result` because CONVENTIONS.md says sealed by default and an inheritance root would let a future subclass redefine what success means (WP-0.3, 2026-08-29)
- Chose an `ErrorKind` enum mapped to HTTP in one place over handlers choosing status codes because ARCHITECTURE.md §6 says errors are ProblemDetails *always*, and "always" only survives forty endpoints if the translation lives in a single file (WP-0.3, 2026-08-29)
- Chose to put the ProblemDetails mapping and the validation endpoint filter in `Itms.Platform`, giving the shared kernel a `FrameworkReference` on `Microsoft.AspNetCore.App`, over a separate web-primitives project because the kernel still references no module and a fourth shared project would be ceremony (WP-0.3, 2026-08-29)
- Chose to clamp out-of-range paging input rather than reject it because a caller asking for page 0 wants results, not a 400, and the clamp is also what stops a hostile `pageSize` becoming a table scan (WP-0.3, 2026-08-29)
- Chose to camel-case FluentValidation's property names before returning them so the client maps errors straight onto form fields, over returning CLR names and translating per form (WP-0.3, 2026-08-29)
- Chose to treat a missing `IValidator<T>` as "no rules" rather than a failure because not every request model needs validation and failing closed would make adding an endpoint a two-step affair (WP-0.3, 2026-08-29)
- Chose a hand-written RFC 4180 `CsvParser` returning `Result` over CsvHelper because the import surface is two files (SPEC.md §12) and a malformed upload must come back as a ProblemDetails, not as an exception from someone else's parser; noted in STATUS.md as swappable if requirements grow (WP-0.3, 2026-08-29)
- Chose to neutralise leading `=`, `+`, `-`, and `@` in exported CSV fields because exported ticket and asset text is attacker-controlled and the export is the only place spreadsheet formula injection can be stopped (WP-0.3, 2026-08-29)
- Chose to enforce the §3 boundary rules from both the `.csproj` graph and the compiled assembly references because the modules are still empty and the C# compiler omits a project reference it sees no types from — the declared-reference check is the one that bites today (WP-0.3, 2026-08-29)
- Chose to write only the four lookup contracts that ARCHITECTURE.md's cross-module reads actually imply — asset, user, department, location — and to leave `ITicketLookup` and `IDeviceLookup` to the package that first needs one, because speculative contracts rot (WP-0.3, 2026-08-29)
- Chose to carry denormalised display text (ticket number, asset tag, location path) on domain events rather than have consumers look it up, per §3 rule 6, so a consumer can render without a cross-module call and an alert keeps the context it was raised with (WP-0.3, 2026-08-29)
- Chose `Guid.CreateVersion7()` for event ids over `Guid.NewGuid()` because the outbox table will be indexed on the id and v7 is time-ordered, which keeps that index from fragmenting (WP-0.3, 2026-08-29)
- Chose to disable CA1716 repo-wide over renaming `Error` because the rule protects cross-language consumers this C#-only solution will never have, and it would otherwise push good domain names into worse ones at every module (WP-0.3, 2026-08-29)
- Chose to wire `AddPlatform()` plus `UseExceptionHandler`/`UseStatusCodePages` into `Itms.Web.Host` in this package because an unregistered shared kernel proves nothing and framework-generated 404s would otherwise not be ProblemDetails (WP-0.3, 2026-08-29)

## WP-0.4 — In-process bus + transactional outbox

- Chose a separate `Itms.Messaging` project over putting the bus in `Itms.Platform` because publishing takes a `DomainEvent` from `Itms.Contracts`, and WP-0.3 deliberately keeps the shared kernel clear of Contracts so a cross-module read never drags the kernel along (WP-0.4, 2026-08-29)
- Chose to treat messaging as infrastructure rather than a module — its own `messaging` schema, its own migrations history, no module may reference it — because it owns no business data, and ARCHITECTURE.md §4's one-schema-per-module rule is about ownership (WP-0.4, 2026-08-29)
- Chose a scoped `IDbSession` owning one connection per scope, with every `DbContext` built on it, over each context opening its own because "the publisher enrols in the caller's transaction" is otherwise two transactions and a window between them (WP-0.4, 2026-08-29)
- Chose to throw when `PublishAsync` is called outside a transaction over publishing immediately, because a lone publish silently reintroduces the exact failure the outbox exists to remove and is a programming error, not a failure the caller can act on (WP-0.4, 2026-08-29)
- Chose a lease-based claim — one `FOR UPDATE SKIP LOCKED` statement that pushes `available_at` forward and commits — over holding a row lock across the consumers, because one slow consumer would otherwise block a whole batch and a crashed dispatcher would block it until the connection died (WP-0.4, 2026-08-29)
- Chose one transaction per consumer, committing the consumer's work and its consumption row together, over one transaction per message, because that is what makes a partial failure retry only the consumer that threw (WP-0.4, 2026-08-29)
- Chose a `(message_id, consumer_name)` composite primary key as the idempotency ledger over a processed flag on the message, because two consumers of one event must be tracked separately and the database itself then refuses a duplicate (WP-0.4, 2026-08-29)
- Chose to name consumption rows after the implementation type rather than the interface, so renaming a consumer makes it re-consume history rather than silently inherit another consumer's completions — the safe direction of that mistake (WP-0.4, 2026-08-29)
- Chose to park a message after `MaxAttempts` rather than delete it, and to leave no requeue path in this package, because a dead-lettered event is evidence and reviving one should be a deliberate operator act (WP-0.4, 2026-08-29)
- Chose to leave a message whose event type this build does not know outstanding — refunding the attempt — over dead-lettering it, because a rolling deployment can put a newer build's message in front of an older dispatcher and that is a wait, not a poisoning (WP-0.4, 2026-08-29)
- Chose `IEventConsumer<T>`/`ConsumeAsync` over `IEventHandler<T>`/`HandleAsync` because CA1711 reserves the `EventHandler` suffix for delegates, and "consumer" is the word ARCHITECTURE.md §5 already uses — a rename was cheaper than a repo-wide suppression (WP-0.4, 2026-08-29)
- Chose to keep `Aspire.Npgsql` (the bare `NpgsqlDataSource`) over swapping to `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` because every context is built on the connection `IDbSession` hands out, so an Aspire-registered context with a pool of its own would defeat the shared transaction; this supersedes the swap STATUS.md anticipated at WP-0.2 (WP-0.4, 2026-08-29)
- Chose source-generated `[LoggerMessage]` partials over `logger.LogXxx` calls because the repo builds warnings-as-errors and CA1848 is right about the dispatcher being a hot path (WP-0.4, 2026-08-29)
- Chose to split `IOutboxProcessor.ProcessOnceAsync` out of the hosted service so tests drive delivery a pass at a time, rather than starting the background loop and waiting on it, which CONVENTIONS.md's ban on `Thread.Sleep` in tests rules out anyway (WP-0.4, 2026-08-29)
- Chose to have the integration-test consumers write rows to a real table through the ambient session over counting in-memory invocations, because the latter would pass even if the consumption row and the side effect committed separately (WP-0.4, 2026-08-29)
- Chose an `Itms.TestSupport` project holding `FakeClock` and the Testcontainers/Respawn fixture over duplicating them, because STATUS.md ruled out `Itms.IntegrationTests` referencing `Itms.UnitTests` and a test project referencing another test project makes one suite's refactor the other's build break (WP-0.4, 2026-08-29)
- Chose to relax IDE0161 (file-scoped namespaces) to a suggestion under `**/Migrations/` because EF Core generates block namespaces and ARCHITECTURE.md §4 says a migration is never edited after merge (WP-0.4, 2026-08-29)
