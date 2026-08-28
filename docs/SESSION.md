# SESSION.md — Claude Code Session Kickoff

**If you have been told to read this file, it is your complete instruction set for this session. Follow it exactly.**

## Step 1 — Load context, in this order

1. `docs/ARCHITECTURE.md` — system design and invariants. **Binding.**
2. `docs/CONVENTIONS.md` — code standards, including the testing-speed rules. **Binding.**
3. `docs/DESIGN.md` — the visual system, with the reference at `docs/design/reference-dashboard.png`. **Binding for any package marked `[UI]`.**
4. `docs/STATUS.md` — where the build currently is, plus in-flight notes.
5. `docs/DECISIONS.md` — choices already settled. Do not relitigate them.
6. `docs/WORK_PACKAGES.md` — read the **Package protocol** section and the entry for the Current WP named in `STATUS.md`.

Read `SPEC.md` and `ROADMAP.md` only if the current package explicitly points you there.

## Step 2 — State scope and stop

Before writing any code, output:

- **Package:** the WP number and title.
- **Building:** the concrete deliverables.
- **Not building:** the adjacent things you are deliberately leaving alone.
- **Files:** what you expect to create or modify.
- **Needs approval:** any new dependency, any schema change beyond the package's own tables, anything that would touch auth, the bus, or existing migrations.
- **Questions:** anything ambiguous in the package. Ask now, not halfway through.

Then **stop and wait for "go"**. Do not begin implementation before it.

If `STATUS.md` and this file disagree about where the build is, say so and stop — do not guess.

## Step 3 — Build

- Stay inside the package. Something out of scope but worth doing goes in the report and in `STATUS.md` under *In flight / noticed* — not in the diff.
- Follow `CONVENTIONS.md` on layout, naming, error handling, and testing. Follow `DESIGN.md` tokens exactly on UI work; the reference screenshot wins over your own taste.
- Write tests as you go, at the level `CONVENTIONS.md` specifies. A package with no tests is not done.
- Never scaffold on EOL versions. `net10.0`, Node 24, Python 3.13+, React 19, current Vite. If a template or a habit produces `net8.0`, correct it.
- Run the build and the tests yourself before reporting. Do not hand back a red tree.

## Step 4 — Package Completion Report

End with, in this order:

1. **Built** — what now exists that did not before.
2. **Files** — added and changed.
3. **Decisions** — choices you made that a future session must respect.
4. **Dependencies** — anything added, and why it was necessary.
5. **Deferred** — what you deliberately left, and where you noted it.
6. **Regression command** — the exact command to run (e.g. `dotnet test && npm test --prefix src/Itms.Web.Client`).
7. **Manual verification checklist** — numbered steps a human can walk in the browser, each with the expected result. Include at least one failure case.

## Step 5 — Update the steering files

- `docs/STATUS.md`: mark this package complete, set **Current WP** to the next one with its branch name, and record anything in flight.
- `docs/DECISIONS.md`: append one line per decision — "chose X over Y because Z (WP-N.N, YYYY-MM-DD)". If none were made, say so in the report.

## Step 6 — Stop

Do not start the next package. Do not merge. Do not tag. The human verifies, commits, merges, and opens the next session.

---

## Standing rules

- **Never modify without an explicit instruction in the current package:** auth configuration, credential handling, outbox/bus wiring, migration history from earlier packages, or `DESIGN.md` tokens.
- **Never** commit secrets, write SNMP set operations, delete or update audit rows, or add anything from the Defer column of `SPEC.md`.
- If the package is marked `[SENSITIVE]`, say so in your scope summary — the human will review your diff line by line.
- If you find yourself two hours into a package and fighting it, stop and say so. Splitting the package is cheaper than a bad merge.
