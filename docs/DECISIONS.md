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
