# STATUS.md — Current Build Position

> Updated by Claude Code at the end of every session. This is the only place that says where the build is.

**Project:** Unified IT Management System (ITMS)
**Phase:** 0 — Foundation
**Current WP:** `WP-0.3 — Platform & Contracts`
**Branch:** `feat/wp-0.3-platform-contracts`
**Last completed:** `WP-0.2 — Aspire orchestration` (2026-08-28)
**Last updated:** 2026-08-28

---

## Progress

| Phase | Packages | Done | Tag |
|---|---|---|---|
| 0 — Foundation | 0.1 – 0.9 | 2 / 9 | — |
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
- **`Itms.ArchitectureTests` references every module project.** That is deliberate — it cannot inspect assemblies it does not reference — but it means the architecture rules must never be written as "no test project references two modules". WP-0.3 writes those rules and should exclude the test assemblies explicitly.
- **Three package versions are declared but unreferenced** in `Directory.Packages.props`: `NetArchTest.Rules` (WP-0.3), `Testcontainers.PostgreSql` and `Respawn` (WP-0.4). If those packages turn out to be the wrong choice, delete the lines rather than leaving them to rot.
- **The .NET 10 SDK now defaults `dotnet new sln` to `.slnx`.** `ITMS.sln` was forced to the classic format because `CONVENTIONS.md` and WP-0.1 both name it. Migrating to `.slnx` later is a one-command change (`dotnet sln migrate`) and would need a `CONVENTIONS.md` edit.
- **`Itms.Web.Host` references no module project yet.** It now references `Itms.ServiceDefaults`; module registration still arrives with each module's own package.
- **`Aspire.Npgsql` registers a bare `NpgsqlDataSource`.** WP-0.5 brings EF Core and will most likely want `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` instead. Swap the package rather than stacking both — two registrations of the same connection is how you get a pool nobody is watching.
- **`/health` returns 503 when Redis is down.** `ARCHITECTURE.md` §4 says the system must survive a Redis flush, and a flush is not a failure, but an unreachable Redis currently takes the whole readiness probe red. Whether Redis belongs in readiness or only in a `degraded` signal is a Phase 6 hardening question, not a WP-0.2 one.
- **Health endpoints are Development-only**, exactly as ServiceDefaults ships them. Production exposure needs a deliberate decision about who can reach them (Phase 6).
- **No React client resource and no poller resource in the AppHost.** They join in WP-0.8 and Phase 3 respectively.
- **MailHog is archived upstream** (`mailhog/mailhog:v1.0.1`, no release since 2020). It works, and `ARCHITECTURE.md` §10 names it. `CommunityToolkit.Aspire.Hosting.MailPit` is the maintained equivalent with a first-class Aspire integration; swapping gets more expensive once WP-0.9 and the Notifications module depend on it.

## Known issues

- none

## Environment notes

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
