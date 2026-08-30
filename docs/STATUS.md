# STATUS.md — Current Build Position

> Updated by Claude Code at the end of every session. This is the only place that says where the build is.

**Project:** Unified IT Management System (ITMS)
**Phase:** 0 — Foundation
**Current WP:** `WP-0.7 — Audit spine` `[SENSITIVE]`
**Branch:** `feat/wp-0.7-audit-spine`
**Last completed:** `WP-0.6 — Minimal directory (departments & locations)` (2026-08-31)
**Last updated:** 2026-08-31

---

## Progress

| Phase | Packages | Done | Tag |
|---|---|---|---|
| 0 — Foundation | 0.1 – 0.9 | 6 / 9 | — |
| 1 — Helpdesk | 1.1 – 1.10 | 0 / 10 | — |
| 2 — Assets & directory | 2.1 – 2.7 | 0 / 7 | — |
| 3 — Monitoring & alerts | 3.1 – 3.8 | 0 / 8 | — |
| 4 — Knowledge, search, notifications | 4.1 – 4.5 | 0 / 5 | — |
| 5 — Dashboard, reports, admin | 5.1 – 5.9 | 0 / 9 | — |
| 6 — Hardening | 6.1 – 6.6 | 0 / 6 | — |

---

## In flight / noticed

*(Things spotted during a session that are real but out of that package's scope. Each one either becomes a work package or gets consciously dropped — nothing lives here indefinitely.)*

- **No README yet.** `CONVENTIONS.md` requires one covering prerequisites, `aspire run`, seed data, default login, and how to run tests. Everything it needs now exists — the seeded accounts and their password are in *Environment notes* below — so the README is writeable at the Phase 0 gate and nothing else blocks it.
- **`Itms.ArchitectureTests` references every module project.** That is deliberate — it cannot inspect assemblies it does not reference. WP-0.3 wrote the rules against the source projects only and added `Rules_are_written_against_source_assemblies_only` as the guard; keep it that way.
- **`dotnet test` reports "Zero tests ran" for every test project.** The assemblies themselves are fine — each runs green under `dotnet run --project <test project>`, and `<exe> --list-tests` enumerates them — but the SDK's Microsoft.Testing.Platform driver and xUnit v3 4.0.0's `mtp-v2` host do not agree, and the run ends in 250 ms with exit code 5. It is not specific to WP-0.5's tests; it happens on the architecture suite too. Until it is resolved the regression command is the three `dotnet run` invocations below. Fixing it is a package of its own: the likely levers are an SDK patch or moving xUnit off the `mtp-v2` line, and both are repo-wide dependency decisions.
- **The ProblemDetails middleware has no automated test.** `UseExceptionHandler`/`UseStatusCodePages` are wired in `Program.cs`, and the mapping itself is unit-tested, but nothing asserts that a real 404 from the host comes back as a problem document — that needs `Microsoft.AspNetCore.Mvc.Testing`, which is a new dependency. Deliberately deferred at WP-0.4 (that package's harness is a database harness, not an HTTP one); fold it into WP-0.9.
- **One of the four lookup contracts still has no implementation.** `IUserLookup` is implemented by Identity (WP-0.5); `IDepartmentLookup` and `ILocationLookup` by Directory (WP-0.6). `IAssetLookup` is still an interface only and belongs to Phase 2. The architecture rule "cross-module reads go through Contracts" still cannot be asserted positively until a module actually *reads* across a boundary — nothing consumes any lookup from outside its owning module yet, so it remains enforced negatively, by forbidding the reference.
- **`ITicketLookup` and `IDeviceLookup` were deliberately not written.** Add them in the package that first needs them rather than speculatively.
- **`CsvParser` is hand-written** (about 120 lines) rather than taken from CsvHelper. It covers RFC 4180 including quoted newlines and doubled quotes. If import requirements grow past that — encodings, delimiter sniffing, streaming a 50 MB file — swap it for a library rather than growing it.
- **The .NET 10 SDK now defaults `dotnet new sln` to `.slnx`.** `ITMS.sln` was forced to the classic format because `CONVENTIONS.md` and WP-0.1 both name it. Migrating to `.slnx` later is a one-command change (`dotnet sln migrate`) and would need a `CONVENTIONS.md` edit.
- **`Itms.Web.Host` references two modules so far.** Alongside `Itms.ServiceDefaults`, `Itms.Platform`, `Itms.Contracts`, and `Itms.Messaging` it references `Itms.Modules.Identity` and `Itms.Modules.Directory`, and calls `AddXxxModule()` / `MapXxxEndpoints()` for each. Every later module joins the same way, in the same places, and nowhere else. WP-0.6 also added a `MigrateDirectoryAsync()` and a `DevelopmentDirectorySeeder` call to the Development-only startup block.
- **`AddMessaging(builder.Configuration)` is called with no consumer assemblies.** The bus cannot reference a module, so the composition root names the assemblies scanned for `IEventConsumer<T>`. No module has a consumer yet; every module package from WP-0.7 onward that adds one must add its assembly to that call, or the consumer silently never runs.
- **`Aspire.Npgsql` stays the bare `NpgsqlDataSource`, deliberately.** WP-0.4 considered the swap to `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` and rejected it: every `DbContext` is built on the one connection `IDbSession` hands out, so an Aspire-registered context with a pool of its own would defeat the shared transaction. Module contexts must be registered the same way — `UseNpgsql(session.Connection)`, never `AddNpgsqlDbContext`. WP-0.5 made that possible for a module, which may not reference the bus: the connection reaches it through `IModuleDbSession` in `Itms.Platform`, adapted onto `IDbSession` by the host. `Itms.Modules.Identity` is the worked example.
- **`/health` returns 503 when Redis is down.** `ARCHITECTURE.md` §4 says the system must survive a Redis flush, and a flush is not a failure, but an unreachable Redis currently takes the whole readiness probe red. Whether Redis belongs in readiness or only in a `degraded` signal is a Phase 6 hardening question, not a WP-0.2 one.
- **Health endpoints are Development-only**, exactly as ServiceDefaults ships them. Production exposure needs a deliberate decision about who can reach them (Phase 6).
- **No React client resource and no poller resource in the AppHost.** They join in WP-0.8 and Phase 3 respectively.
- **MailHog is archived upstream** (`mailhog/mailhog:v1.0.1`, no release since 2020). It works, and `ARCHITECTURE.md` §10 names it. `CommunityToolkit.Aspire.Hosting.MailPit` is the maintained equivalent with a first-class Aspire integration; swapping gets more expensive once WP-0.9 and the Notifications module depend on it.

### Noticed during WP-0.4

- **The outbox has no dead-letter requeue path and no operator view.** A message that exhausts `MaxAttempts` is parked with `failed_at` set and stays there; reviving one today means a manual `UPDATE`. That is the right default for Phase 0, but Phase 6 hardening should decide whether an admin-only requeue endpoint is wanted, and whether a parked message should raise an alert.
- **Processed outbox rows are never pruned.** The filtered index keeps the dispatcher's query small regardless, but the table grows without bound. A retention job belongs with the monitoring rollup job in Phase 3, or at the Phase 6 gate — the same hosted-service pattern covers both.
- **The dispatcher is single-instance-safe but never runs multi-instance in V1.** The lease and `FOR UPDATE SKIP LOCKED` are there so a second host would be correct, but nothing tests two dispatchers racing. If V1 ever scales past one host, that test is the prerequisite, not an afterthought.
- **`OutboxProcessor` invokes consumers by reflection** (`MethodInfo.Invoke` per message per consumer). That is bounded by the event rate, not by request throughput, so it is fine at this scale; if the outbox ever becomes hot, compiled delegates are the fix, not a redesign.
- **`MessagingOptions.LeaseDuration` must exceed the slowest consumer.** It defaults to five minutes and nothing enforces the relationship. A consumer that runs longer than the lease would let a second dispatcher pass pick up a message still in flight — the consumption row makes the *effect* safe, but the work would be done twice.
- **`Itms.TestSupport` does not end in `Tests`,** so `Directory.Build.props` does not apply the test-project property overrides to it and `.editorconfig` does not relax CA1707 there. That is intentional — it is a library, not a suite — but it means helpers in it are held to production analyzer rules.

### Noticed during WP-0.5

- **There is no way for a signed-in person to revoke their own other sessions.** Logout ends the current session and a password change ends every other one, but "sign me out everywhere" has no endpoint — it was deliberately left out of WP-0.5. No later package mentions it: `WP-5.8 — Administration` covers managing users and roles, and `WP-6.1 — Security hardening` covers CSRF, rate limits, and headers, but neither names session management. It needs a home; WP-5.8 is the natural one, since an admin revoking *another* user's sessions belongs in the same screen and the two share a handler.
- **Password reset is not built.** ARCHITECTURE.md §7 says reset flows use Identity defaults, but a reset needs email and the Notifications module does not exist. `AddDefaultTokenProviders` is registered, so the token half is already there; the delivery half belongs with Notifications (Phase 4).
- **Expired session rows are never deleted.** ARCHITECTURE.md §4 says expired sessions are hard-deleted, and `ix_sessions_expires_at` exists precisely so a sweep can find them, but nothing sweeps yet. This is the same shape of problem as the unpruned processed outbox rows and wants the same hosted-service pattern — pair the two in Phase 3 or at the Phase 6 gate.
- **CSRF and login rate limiting are already implemented here**, because CONVENTIONS.md's security floor requires them of any cookie-auth endpoint and WP-0.5 is where cookie auth arrives. `WP-6.1 — Security hardening` also names both. That package should verify and extend them — search rate limits, the endpoint sweep, HSTS and headers — rather than build them again.
- **The Identity framework tables carry no `created_at`/`created_by` columns**, unlike every table this system designs itself. `UserManager` writes `user_roles`, `user_claims`, `user_logins`, and `user_tokens` and has nowhere to put an actor. Who changed someone's role is recorded where ARCHITECTURE.md §8 says it belongs: in the audit log, which WP-0.7 builds. If WP-0.7 finds it needs a column here after all, that is a migration on the identity schema and not a redesign.
- **`users.department_id` and `users.location_id` exist but nothing populates them.** They are plain identifiers with no foreign key, per §3 rule 6, and `ItmsUser.PlaceIn` is the method that sets them. WP-0.6 created the rows they will point at; the package that adds user administration (WP-5.8) is what will actually set them.
- **The user directory endpoints (`GET /api/v1/users`, `GET /api/v1/users/{id}`) are slightly ahead of WP-0.5's text.** They were added because every later module needs a requester/assignee picker, because `IUserLookup` had to be implemented and proved anyway, and because the role matrix needed a real Technician-guarded endpoint to be enforced on. They are read-only and carry no credential state. User *administration* — create, edit, deactivate, assign roles — is still WP-5.8.
- **Nothing publishes `UserDeactivated` yet.** `ItmsUser.Deactivate` exists and the cookie validator already refuses a deactivated user on their next request, but no endpoint calls it and no event is raised, so the consumers ARCHITECTURE.md §5 anticipates have nothing to consume. Raise it in the package that adds deactivation to the API.

### Noticed during WP-0.6

- **The antiforgery endpoint filter now exists twice.** `Itms.Platform.Security.AntiforgeryFilter` was added because a module may not reference another module and every module that writes behind a cookie needs the check; `Itms.Modules.Identity.Security.AntiforgeryFilter` is identical and was deliberately left alone under the standing rule about auth configuration. `WP-6.1 — Security hardening` already owns CSRF and is the right place to delete Identity's copy and point its four endpoints at the kernel's.
- **`SearchPattern` in Directory duplicates the ILIKE escaping inlined in `UserLookupService`.** Both wrap a term in wildcards and escape `\`, `%`, and `_`. Every module with a picker or a filter will want it. It is shared-kernel material — the third copy is the point at which it should move to `Itms.Platform` rather than be written again.
- **The location depth guard is currently unreachable.** `LocationHierarchy.MaxDepth` is 5 and the rank rule already caps a branch at five levels, so `CanAdopt`'s depth half and `MoveLocationHandler.CheckDepthAsync` can never fire today. Both were kept as the backstop that bounds the materialised `path` column if a sixth kind is ever added; `CheckDepthAsync` costs one indexed aggregate per move, which is the price of that.
- **Directory raises no domain events.** ARCHITECTURE.md §5 names no directory event and nothing consumes one yet, so none was invented. §3 rule 6 says denormalised display data is refreshed on the owning entity's change event — the moment a ticket or an asset caches a department name or a location path, this module needs a `DepartmentRenamed`/`LocationMoved` event and the packages that cache will have to add it. A rename already rewrites every descendant's path here, so the event has real work to announce.
- **Nothing audits a directory change.** WP-0.7 is the audit spine, and the department and location handlers are exactly the "mutations that do not warrant a domain event" that ARCHITECTURE.md §8 says call `IAuditWriter` directly. The `created_by`/`updated_by` columns are populated and the handlers already have `ICurrentUser`, so the hook is a one-line addition per handler when the writer exists.
- **A concurrent rename and a concurrent child insert are not serialised against each other.** A rename rewrites descendants by prefix; a child inserted between the rewrite and the commit would be written with a path composed from the pre-rename parent. The transaction makes it atomic but not isolated at PostgreSQL's default Read Committed. It needs either a `SELECT ... FOR UPDATE` on the ancestor or Serializable isolation. It is a two-administrators-at-once problem on a screen one person uses, so it is recorded rather than solved; ARCHITECTURE.md §6 already wants optimistic concurrency on tickets and assets, and this is the same conversation.
- **The WP-0.6 integration suite has not been run on this machine.** `dotnet build`, the 180 unit tests, and the 46 architecture tests are green, but Docker Desktop's WSL integration was not mounting `/var/run/docker.sock` in the session that built this package, so `Itms.IntegrationTests` — which includes every department, location-tree, and lookup-contract test — could not start its Testcontainers PostgreSQL. The suite compiles. It must be run and seen green before this branch is merged; if the socket is missing again, restarting the WSL session after Docker Desktop is up restores it.
- **There is no bulk import for the directory.** SPEC.md §16 lists CSV import for assets and users only, so this is correct for V1 — but an operator entering a four-hundred-room estate through `POST /api/v1/locations` one call at a time will ask. `CsvParser` already exists in `Itms.Platform` if the answer ever changes.

## Known issues

- none

## Environment notes

- **CA1716 is off repo-wide** (`.editorconfig`). It flags names that are keywords in other .NET languages — `Error`, `Next`, `Handle` — and this solution is C#-only and ships no library to VB or F#.
- Repo lives in the WSL filesystem at `~/projects/itms` — never under `/mnt/c/`.
- `global.json` pins the SDK to 10.0.x (`rollForward: latestFeature`) **and** selects the `Microsoft.Testing.Platform` test runner. xUnit v3 is an MTP runner and the .NET 10 SDK will not drive one through VSTest, so removing that `test.runner` block breaks `dotnet test` for the whole repo.
- Aspire templates are pinned at **13.5.3** (`Aspire.AppHost.Sdk/13.5.3` in the AppHost csproj). Run `aspire update` at every phase gate per `ARCHITECTURE.md` §10.
- Package versions are centrally managed in `Directory.Packages.props`. A `<PackageReference>` with a `Version` attribute is a build error, not a style nit.
- **The integration suite needs a running Docker daemon** from WP-0.4 onward (Testcontainers starts one `postgres:17.6` per test assembly). On WSL that means Docker Desktop running with integration enabled for this distro; without it `dotnet test` fails in `Itms.IntegrationTests` with a Docker connection error rather than a test failure. The first run also pays ~25s to pull the image.
- **EF migrations are added per context**, e.g. `dotnet ef migrations add <Name> --project src/Itms.Messaging/Itms.Messaging.csproj --output-dir Outbox/Migrations`. Each context carries an `IDesignTimeDbContextFactory` pointing at a deliberately unreachable connection string, so scaffolding can never touch a real database.
- **`IDE0161` (file-scoped namespaces) is a suggestion under `**/Migrations/`**, because EF generates block namespaces and a migration is never edited after merge.
- Start everything with `aspire run` from the repo root. Postgres, Redis, MailHog, and the web host all come up; the dashboard URL with its login token is printed to the console.
- Postgres and Redis run with `ContainerLifetime.Persistent`, so they **stay up after Ctrl+C** and are reused by the next `aspire run`. That is deliberate — it is what makes migrations and seed data survive a session. `docker rm -f postgres-* redis-*` if a container ever needs a clean start; `docker volume rm itms-postgres-data itms-redis-data` to also drop the data.
- MailHog is session-lifetime and ephemeral: it is removed on shutdown and captured mail does not survive it.
- The Postgres password is generated by Aspire and lives in the AppHost user-secrets (`UserSecretsId` in `Itms.AppHost.csproj`). It is never in the repository and never needs to be.
- `aspire run` on WSL warns `PartiallyFailedToTrustTheCertificate` for the ASP.NET dev certificate. The dashboard and the API still work; the browser will show a certificate warning on `https://localhost:7014`. `dotnet dev-certs https --trust` does not fully apply under WSL.
- **Dev credentials.** `aspire run` seeds three accounts, one per role: `admin` / `tech` / `user` (their addresses are `admin@itms.local`, `tech@itms.local`, `user@itms.local`; either identifier signs in). All three use the password **`Dev!Passw0rd123`**. They are created only when `ASPNETCORE_ENVIRONMENT` is `Development` — the seeder checks and returns after seeding roles otherwise — and they must not exist in a production deployment, which gets its first administrator from the first-run setup in WP-6.6. The roles themselves are seeded in every environment.
- **The authentication settings are configuration**, bound from the `Identity` section: cookie name, cookie lifetime (8 h sliding), absolute session lifetime (24 h), lockout threshold and duration, minimum password length, and the credential rate limit. Every value has a production-safe default and is validated at startup, so an empty section is a hardened configuration rather than an open one; a value below the floor — a password minimum under 12, a session shorter than the cookie — fails the deployment.
- **The integration suite boots the real host** with `Microsoft.AspNetCore.Mvc.Testing` against its own `itms_web_tests` database inside the shared container. It is a second database rather than a second container because the host runs the outbox dispatcher, which would otherwise claim the messages the outbox tests are asserting on.
