# ITMS — Unified IT Management System

@docs/SESSION.md

## Every session

`docs/SESSION.md` above is the protocol. Follow it exactly: load the steering files, state scope, **stop and wait for "go"**, build only the current work package, report, update `docs/STATUS.md` and `docs/DECISIONS.md`, then stop.

- Where the build is: `docs/STATUS.md`
- What to build: `docs/WORK_PACKAGES.md`
- How to build it: `docs/ARCHITECTURE.md`, `docs/CONVENTIONS.md`
- How it looks: `docs/DESIGN.md` and `docs/design/reference-dashboard.png`
- What is already settled: `docs/DECISIONS.md`

## Hard rules

- Never write code before the human says "go".
- Never start the next work package. Never merge, push, or tag — the human does that.
- Branch from an up-to-date `main`, never from another package's branch (`main` squash-merges, so a branch off a branch conflicts with itself).
- Target `net10.0`, Node 24, Python 3.13+, React 19. Never scaffold `net8.0` or `net9.0`.
- Never modify auth configuration, credential handling, outbox/bus wiring, earlier migrations, or `DESIGN.md` tokens unless the current package explicitly says to.
- Never commit secrets, write SNMP set operations, or add any update or delete path for audit rows.
- Never build anything from the Defer column of `docs/SPEC.md`.
- A package with no tests is not done.

## Commands

```bash
aspire run                                   # start everything (from repo root)
dotnet build && dotnet test                  # backend
npm test --prefix src/Itms.Web.Client        # frontend
uv run ruff check . && uv run mypy .         # poller (from src/itms-poller)
```
