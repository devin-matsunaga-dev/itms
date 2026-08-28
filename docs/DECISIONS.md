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
