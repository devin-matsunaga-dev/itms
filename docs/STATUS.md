# STATUS.md — Current Build Position

> Updated by Claude Code at the end of every session. This is the only place that says where the build is.

**Project:** Unified IT Management System (ITMS)
**Phase:** 0 — Foundation
**Current WP:** `WP-0.4 — In-process bus + transactional outbox`
**Branch:** `feat/wp-0.4-bus-outbox`
**Last completed:** `WP-0.3 — Platform & Contracts` (2026-08-29)
**Last updated:** 2026-08-29

---

## Progress

| Phase | Packages | Done | Tag |
|---|---|---|---|
| 0 — Foundation | 0.1 – 0.9 | 3 / 9 | — |
| 1 — Helpdesk | 1.1 – 1.10 | 0 / 10 | — |
| 2 — Assets & directory | 2.1 – 2.7 | 0 / 7 | — |
| 3 — Monitoring & alerts | 3.1 – 3.8 | 0 / 8 | — |
| 4 — Knowledge, search, notifications | 4.1 – 4.5 | 0 / 5 | — |
| 5 — Dashboard, reports, admin | 5.1 – 5.9 | 0 / 9 | — |
| 6 — Hardening | 6.1 – 6.6 | 0 / 6 | — |

---

## In flight / noticed

*(Things spotted during a session that are real but out of that package's scope. Each one either becomes a work package or gets consciously dropped — nothing lives here indefinitely.)*

- **No README yet.** `CONVENTIONS.md` requires one covering prerequisites, `aspire run`, seed data, default login, and how to run tests. `aspire run` and the test commands exist now; seed data and the default login do not until WP-0.5, so the README is still best written at the Phase 0 gate.
- **`Itms.ArchitectureTests` references every module project.** That is deliberate — it cannot inspect assemblies it does not reference. WP-0.3 wrote the rules against the source projects only and added `Rules_are_written_against_source_assemblies_only` as the guard; keep it that way.
- **Two package versions are declared but unreferenced** in `Directory.Packages.props`: `Testcontainers.PostgreSql` and `Respawn` (WP-0.4). If those packages turn out to be the wrong choice, delete the lines rather than leaving them to rot.
- **`FakeClock` lives in `tests/Itms.UnitTests/Platform/`.** The integration suite will want it too from WP-0.4 onward. Either move it to a small `Itms.TestSupport` project then, or duplicate it knowingly — do not have `Itms.IntegrationTests` reference `Itms.UnitTests`.
- **The ProblemDetails middleware has no automated test.** `UseExceptionHandler`/`UseStatusCodePages` are wired in `Program.cs`, and the mapping itself is unit-tested, but nothing asserts that a real 404 from the host comes back as a problem document — that needs `Microsoft.AspNetCore.Mvc.Testing`, which is a new dependency. Worth folding into WP-0.4's integration harness or WP-0.9.
- **The lookup contracts have no implementations.** `IAssetLookup`, `IUserLookup`, `IDepartmentLookup`, and `ILocationLookup` are defined; each owning module implements its own in its package (WP-0.6 for Directory, Phase 1/2 for the rest). The architecture rule "cross-module reads go through Contracts" cannot be asserted positively until a module actually reads across a boundary — today it is enforced negatively, by forbidding the reference.
- **`ITicketLookup` and `IDeviceLookup` were deliberately not written.** Add them in the package that first needs them rather than speculatively.
- **`CsvParser` is hand-written** (about 120 lines) rather than taken from CsvHelper. It covers RFC 4180 including quoted newlines and doubled quotes. If import requirements grow past that — encodings, delimiter sniffing, streaming a 50 MB file — swap it for a library rather than growing it.
- **The .NET 10 SDK now defaults `dotnet new sln` to `.slnx`.** `ITMS.sln` was forced to the classic format because `CONVENTIONS.md` and WP-0.1 both name it. Migrating to `.slnx` later is a one-command change (`dotnet sln migrate`) and would need a `CONVENTIONS.md` edit.
- **`Itms.Web.Host` references no module project yet.** It references `Itms.ServiceDefaults`, `Itms.Platform`, and `Itms.Contracts`, and calls `AddPlatform()`; module registration still arrives with each module's own package.
- **`Aspire.Npgsql` registers a bare `NpgsqlDataSource`.** WP-0.5 brings EF Core and will most likely want `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` instead. Swap the package rather than stacking both — two registrations of the same connection is how you get a pool nobody is watching.
- **`/health` returns 503 when Redis is down.** `ARCHITECTURE.md` §4 says the system must survive a Redis flush, and a flush is not a failure, but an unreachable Redis currently takes the whole readiness probe red. Whether Redis belongs in readiness or only in a `degraded` signal is a Phase 6 hardening question, not a WP-0.2 one.
- **Health endpoints are Development-only**, exactly as ServiceDefaults ships them. Production exposure needs a deliberate decision about who can reach them (Phase 6).
- **No React client resource and no poller resource in the AppHost.** They join in WP-0.8 and Phase 3 respectively.
- **MailHog is archived upstream** (`mailhog/mailhog:v1.0.1`, no release since 2020). It works, and `ARCHITECTURE.md` §10 names it. `CommunityToolkit.Aspire.Hosting.MailPit` is the maintained equivalent with a first-class Aspire integration; swapping gets more expensive once WP-0.9 and the Notifications module depend on it.

## Known issues

- none

## Environment notes

- **CA1716 is off repo-wide** (`.editorconfig`). It flags names that are keywords in other .NET languages — `Error`, `Next`, `Handle` — and this solution is C#-only and ships no library to VB or F#.
- Repo lives in the WSL filesystem at `~/projects/itms` — never under `/mnt/c/`.
- `global.json` pins the SDK to 10.0.x (`rollForward: latestFeature`) **and** selects the `Microsoft.Testing.Platform` test runner. xUnit v3 is an MTP runner and the .NET 10 SDK will not drive one through VSTest, so removing that `test.runner` block breaks `dotnet test` for the whole repo.
- Aspire templates are pinned at **13.5.3** (`Aspire.AppHost.Sdk/13.5.3` in the AppHost csproj). Run `aspire update` at every phase gate per `ARCHITECTURE.md` §10.
- Package versions are centrally managed in `Directory.Packages.props`. A `<PackageReference>` with a `Version` attribute is a build error, not a style nit.
- Start everything with `aspire run` from the repo root. Postgres, Redis, MailHog, and the web host all come up; the dashboard URL with its login token is printed to the console.
- Postgres and Redis run with `ContainerLifetime.Persistent`, so they **stay up after Ctrl+C** and are reused by the next `aspire run`. That is deliberate — it is what makes migrations and seed data survive a session. `docker rm -f postgres-* redis-*` if a container ever needs a clean start; `docker volume rm itms-postgres-data itms-redis-data` to also drop the data.
- MailHog is session-lifetime and ephemeral: it is removed on shutdown and captured mail does not survive it.
- The Postgres password is generated by Aspire and lives in the AppHost user-secrets (`UserSecretsId` in `Itms.AppHost.csproj`). It is never in the repository and never needs to be.
- `aspire run` on WSL warns `PartiallyFailedToTrustTheCertificate` for the ASP.NET dev certificate. The dashboard and the API still work; the browser will show a certificate warning on `https://localhost:7014`. `dotnet dev-certs https --trust` does not fully apply under WSL.
- Dev credentials are seeded in WP-0.5 and documented in the README; they are dev-only and must not exist in a production deployment.
