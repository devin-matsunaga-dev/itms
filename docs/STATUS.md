# STATUS.md — Current Build Position

> Updated by Claude Code at the end of every session. This is the only place that says where the build is.

**Project:** Unified IT Management System (ITMS)
**Phase:** 0 — Foundation
**Current WP:** `WP-0.2 — Aspire orchestration`
**Branch:** `feat/wp-0.2-aspire`
**Last completed:** `WP-0.1 — Solution skeleton` (2026-08-28)
**Last updated:** 2026-08-28

---

## Progress

| Phase | Packages | Done | Tag |
|---|---|---|---|
| 0 — Foundation | 0.1 – 0.9 | 1 / 9 | — |
| 1 — Helpdesk | 1.1 – 1.10 | 0 / 10 | — |
| 2 — Assets & directory | 2.1 – 2.7 | 0 / 7 | — |
| 3 — Monitoring & alerts | 3.1 – 3.8 | 0 / 8 | — |
| 4 — Knowledge, search, notifications | 4.1 – 4.5 | 0 / 5 | — |
| 5 — Dashboard, reports, admin | 5.1 – 5.9 | 0 / 9 | — |
| 6 — Hardening | 6.1 – 6.6 | 0 / 6 | — |

---

## In flight / noticed

*(Things spotted during a session that are real but out of that package's scope. Each one either becomes a work package or gets consciously dropped — nothing lives here indefinitely.)*

- **No README yet.** `CONVENTIONS.md` requires one covering prerequisites, `aspire run`, seed data, default login, and how to run tests. Three of those five do not exist until WP-0.2 and WP-0.5, so the README is best written at the Phase 0 gate. Not in the WP-0.1 diff.
- **`Itms.ArchitectureTests` references every module project.** That is deliberate — it cannot inspect assemblies it does not reference — but it means the architecture rules must never be written as "no test project references two modules". WP-0.3 writes those rules and should exclude the test assemblies explicitly.
- **Three package versions are declared but unreferenced** in `Directory.Packages.props`: `NetArchTest.Rules` (WP-0.3), `Testcontainers.PostgreSql` and `Respawn` (WP-0.4). If those packages turn out to be the wrong choice, delete the lines rather than leaving them to rot.
- **The .NET 10 SDK now defaults `dotnet new sln` to `.slnx`.** `ITMS.sln` was forced to the classic format because `CONVENTIONS.md` and WP-0.1 both name it. Migrating to `.slnx` later is a one-command change (`dotnet sln migrate`) and would need a `CONVENTIONS.md` edit.
- **`Itms.Web.Host` references no module project yet.** Composition-root wiring arrives with the packages that need it, starting with `Itms.ServiceDefaults` in WP-0.2.

## Known issues

- none

## Environment notes

- Repo lives in the WSL filesystem at `~/projects/itms` — never under `/mnt/c/`.
- `global.json` pins the SDK to 10.0.x (`rollForward: latestFeature`) **and** selects the `Microsoft.Testing.Platform` test runner. xUnit v3 is an MTP runner and the .NET 10 SDK will not drive one through VSTest, so removing that `test.runner` block breaks `dotnet test` for the whole repo.
- Aspire templates are pinned at **13.5.3** (`Aspire.AppHost.Sdk/13.5.3` in the AppHost csproj). Run `aspire update` at every phase gate per `ARCHITECTURE.md` §10.
- Package versions are centrally managed in `Directory.Packages.props`. A `<PackageReference>` with a `Version` attribute is a build error, not a style nit.
- Start everything with `aspire run` from the repo root — not functional until WP-0.2 wires the resources.
- Dev credentials are seeded in WP-0.5 and documented in the README; they are dev-only and must not exist in a production deployment.
