# WORKFLOW.md — Playbook: Setup → First Package → Every Package After

Set the machine up once, bootstrap the repo once, then repeat one loop about 54 times. Every session starts the same way: **"Read docs/SESSION.md and proceed."**

---

# Part A — Machine setup (one time)

## Version policy

LTS or latest-supported only. Never scaffold or pull base images for anything below this table.

| Tech | Pinned | Support until | Note |
|---|---|---|---|
| .NET SDK | **10 (LTS)** | Nov 2028 | Never 8 or 9 — both EOL Nov 2026 |
| Aspire | **13.x (latest)** | rolling | Only the newest release is supported; `aspire update` at phase gates |
| Node.js | **24 LTS** | ~Apr 2028 | Node 26 becomes LTS Oct 2026; optional hop then |
| Python | **3.13 min, 3.14 preferred** | 2029 / 2030 | Depends on `pysnmp` / `icmplib` |
| React / Vite | **19 / latest** | rolling | Always scaffold from the current template |
| PostgreSQL | **17+** | | |
| Ubuntu (WSL + prod) | **LTS** | | Match dev and prod |

## Windows side

1. `wsl --install -d Ubuntu-24.04` from an admin PowerShell, reboot, create your Linux user.
2. Install **Docker Desktop** → Settings → Resources → WSL Integration → enable your distro.
3. Install **VS Code** plus the **WSL** extension.
4. Create `C:\Users\<you>\.wslconfig`:
   ```
   [wsl2]
   memory=12GB
   processors=6
   ```
   Then `wsl --shutdown` once to apply.

## WSL side (everything below runs inside the Ubuntu terminal)

```bash
sudo apt update && sudo apt install -y git curl build-essential unzip

# .NET 10 SDK
sudo apt install -y dotnet-sdk-10.0 \
  || (curl -fsSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0)

# Aspire CLI
curl -fsSL https://aspire.dev/install.sh | bash

# Node 24 LTS via nvm
curl -fsSL https://raw.githubusercontent.com/nvm-sh/nvm/master/install.sh | bash
nvm install 24 && nvm alias default 24

# Python 3.13+ and uv
sudo apt install -y python3 python3-venv
curl -LsSf https://astral.sh/uv/install.sh | sh

# Claude Code (native installer — the recommended method on Linux/WSL)
curl -fsSL https://claude.ai/install.sh | bash

git config --global user.name "Your Name"
git config --global user.email "you@example.com"
```

Verify, all inside WSL: `dotnet --version` (10.x) · `aspire --version` (13.x) · `node -v` (24.x) · `python3 --version` (3.13+) · `docker run hello-world` · `claude --version`.

Then run `claude` once and log in through the browser prompt. Claude Code needs a Pro, Max, Team, Enterprise, or Console account — the free plan does not include it. If anything looks off later, `claude doctor` prints installation and settings diagnostics without starting a session.

> The single rule that saves the most pain: **the repo lives in the WSL filesystem (`~/projects/…`), never under `/mnt/c/`.** Builds, file watchers, and Docker mounts are many times faster and actually reliable there.

---

# Part B — Repo bootstrap (one time)

```bash
cd ~ && mkdir -p projects && cd projects
mkdir it-platform && cd it-platform
git init -b main
mkdir -p docs/design .claude
```

1. Copy the ten steering files into `docs/`: `SESSION.md`, `STATUS.md`, `WORK_PACKAGES.md`, `WORKFLOW.md`, `ARCHITECTURE.md`, `CONVENTIONS.md`, `DESIGN.md`, `DECISIONS.md`, `SPEC.md`, `ROADMAP.md`. Put `reference-dashboard.png` in `docs/design/`.
2. Copy `CLAUDE.md` to the **repo root** — not into `docs/`. Claude Code loads it automatically at the start of every session.
3. Copy `.claude/settings.json` (the permissions allowlist) into `.claude/`.
4. **Read `ARCHITECTURE.md` and `CONVENTIONS.md` once and edit them to your taste.** After this commit they are law, and every session obeys them verbatim. Changing them later is fine; changing them casually mid-build is not.
5. Verify and commit:
   ```bash
   for f in SESSION STATUS WORK_PACKAGES WORKFLOW ARCHITECTURE CONVENTIONS DESIGN DECISIONS SPEC ROADMAP; do
     [ -f "docs/$f.md" ] && echo "OK  docs/$f.md" || echo "MISSING  docs/$f.md"
   done
   [ -f CLAUDE.md ] && echo "OK  CLAUDE.md" || echo "MISSING  CLAUDE.md"
   [ -f docs/design/reference-dashboard.png ] && echo "OK  screenshot" || echo "MISSING  screenshot"

   git add -A
   git commit -m "chore: bootstrap steering docs"
   git remote add origin <your-remote-url>
   git push -u origin main
   ```
6. Open the project: `code .` from WSL. Its integrated terminal is your WSL shell — do everything there.

## Claude Code configuration

**`CLAUDE.md` at the root** is read at every session start; it imports `docs/SESSION.md`, so the protocol is in context before you type anything.

**`.claude/settings.json`** pre-approves the commands you would otherwise approve fifty times a day, and denies the ones that should never run unattended:

```json
{
  "permissions": {
    "allow": [
      "Bash(dotnet build:*)", "Bash(dotnet test:*)", "Bash(dotnet ef:*)",
      "Bash(npm run:*)", "Bash(npm test:*)", "Bash(npx vitest:*)",
      "Bash(uv run:*)", "Bash(ruff:*)", "Bash(mypy:*)",
      "Bash(git status)", "Bash(git diff:*)", "Bash(git log:*)"
    ],
    "deny": [
      "Bash(git push:*)", "Bash(git reset --hard:*)", "Bash(rm -rf:*)",
      "Read(./**/.env)", "Read(./**/*.pem)"
    ]
  }
}
```

Merging and pushing stay yours. That is deliberate — the human is the merge gate in this workflow.

**Auto memory** is on by default and Claude will accumulate its own notes per repository. That is harmless here, but it is not the source of truth: `STATUS.md` and `DECISIONS.md` are. If a session ever cites something that is not in the steering files, correct it and check `/memory`.

---

# Part C — First package (WP-0.1), the shakedown run

```bash
cd ~/projects/it-platform
git checkout -b feat/wp-0.1-skeleton
claude
```

1. Type: **"Read docs/SESSION.md and proceed."**
2. Claude loads the steering files, sees `STATUS.md` pointing at WP-0.1, and states its scope summary — the solution skeleton on `net10.0`. Read it. If it matches, say **"go"**.
3. It builds, ends with the Package Completion Report, updates `STATUS.md` and `DECISIONS.md`, and stops.
4. Verify: `dotnet build` succeeds, nothing targets anything but `net10.0`, the layout matches `CONVENTIONS.md`, and its manual checklist passes.
5. Merge with the standard block (memorize this shape):
   ```bash
   git add -A
   git commit -m "feat: solution skeleton (WP-0.1)"
   git checkout main
   git merge --squash feat/wp-0.1-skeleton
   git commit -m "feat: solution skeleton (WP-0.1)"
   git push
   git branch -D feat/wp-0.1-skeleton
   ```
6. Back in Claude Code, run `/clear`. The system is now self-advancing.

---

# Part D — Every package after (the steady-state loop)

**1. Orient (1 min).** Open `docs/STATUS.md`. Note the Current WP, its branch name, and anything under *In flight*.

**2. Branch (1 min).**
```bash
git checkout main && git pull
git checkout -b feat/wp-X.Y-short-name
```

**3. Kick off (10 sec).** In Claude Code, `/clear` for a fresh context, then: **"Read docs/SESSION.md and proceed."**

For anything large or `[SENSITIVE]`, enter **plan mode** first — press `Shift+Tab` until the mode indicator shows Plan. Claude then researches and proposes without touching files, which is exactly the shape of the scope gate.

**4. Gate the scope.** Claude states what it will and will not build and waits. Read it properly. Matches your intent → **"go"**. Doesn't → correct it now, while no code exists. Bundling small packages? Say "do WP-2.6 and WP-2.7 together" at kickoff.

**5. Build.** Let it work. It may want a dependency — approve only if the package justifies it. If it starts drifting: "note that in STATUS.md under In flight and stay in scope."

**6. Receive.** It ends with the Completion Report, updates `STATUS.md` and `DECISIONS.md`, and stops on its own.

**7. Verify (15–45 min — your actual job, never skipped).**
- Run the regression command it gave you. Red → step 8.
- `aspire run`, then walk its manual checklist personally in your Windows browser. Click the things. Try the failure cases, not just the happy path.
- `git diff main` in VS Code — skim normally; **line by line if the package is `[SENSITIVE]`**.
- Read the `STATUS.md` and `DECISIONS.md` updates for accuracy. A wrong `STATUS.md` breaks the next session.

**8. Fix loop (only if needed).** Same session: *"Manual check 3 failed — expected 409, got 500. Fix it."* Then re-verify. If the session has gone long and confused, don't fight it — that's what branches are for:
```bash
git checkout main && git branch -D feat/wp-X.Y-short-name
git checkout -b feat/wp-X.Y-short-name
# /clear, kick off again
```

**9. Merge (2 min).**
```bash
git add -A
git commit -m "feat(module): short description (WP-X.Y)"
git checkout main
git merge --squash feat/wp-X.Y-short-name
git commit -m "feat(module): short description (WP-X.Y)"
git push
git branch -D feat/wp-X.Y-short-name
```
`-D` is required after a squash merge — git can't tell the branch is merged. It's safe; the commit is on `main`.

**10. `/clear`.** The next package starts at step 1, and `STATUS.md` already points at it.

## Context management inside a session

- `/clear` between packages, always. A stale context is how a session starts "remembering" a decision that was never made.
- If a single package genuinely runs long, `/compact` preserves the thread. The root `CLAUDE.md` is re-read from disk after compaction, so the protocol survives.
- `/context` shows which memory files actually loaded. If `CLAUDE.md` isn't listed there, you launched from the wrong directory.

## Phase gates

After a phase's final package, run the 🏁 gate from `ROADMAP.md`, plus the dependency-health pass: `aspire update`, review Dependabot, confirm nothing in the version table has crossed EOL. Then:
```bash
git tag v0.N-phaseN && git push --tags
```

---

# Part E — Quick reference

| Situation | Action |
|---|---|
| Start any session | `/clear` → "Read docs/SESSION.md and proceed." → read summary → "go" |
| Large or `[SENSITIVE]` package | `Shift+Tab` to plan mode before kicking off |
| Tests red / manual check failed | Tell the same session exactly what failed; re-verify |
| Session confused or looping | Delete the branch, re-branch, `/clear`, retry the package |
| Package fights you across two sessions | Split it — edit `WORK_PACKAGES.md`, it's yours |
| Claude wants a new library | It must justify it; anything not in `ARCHITECTURE.md` needs your explicit OK |
| Claude scaffolds `net8.0`, Node 18, an old Vite template | Reject it — the version table is law; have it re-scaffold |
| Machine-level install (sudo, Windows-side) | You do it; Claude writes the commands into `docs/SETUP.md` |
| Claude says it can't find `docs/SESSION.md` | Check `pwd` — launch from the repo root, not from `docs/` |
| `localhost` unreachable from the Windows browser | `wsl --shutdown`, relaunch |
| Phase's last package merged | Run the 🏁 gate + dependency-health pass → tag |

**Non-negotiables:** repo in the WSL filesystem · `main` always green · manual checklist every package · line-by-line diff on `[SENSITIVE]` · one package = one commit on `main` · LTS or latest-supported versions only · nothing from the Defer column of `SPEC.md`.
