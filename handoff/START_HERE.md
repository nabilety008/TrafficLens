# DO NOT READ THE WHOLE REPOSITORY FIRST.

<!-- The line above is the most important sentence in this file. -->

This repository has ~800 published files and a deep history. Loading it all wastes
context and buries the answer. Read these files, in this order, and stop.

## Recommended loading order

1. `handoff/START_HERE.md` (this file)
2. `handoff/CURRENT_STATE.md`
3. `TASKS.md`
4. task-specific handoff/docs from the `handoff/` folder
5. task-specific source files only

Do **not** open `bin/`, `obj/`, or `artifacts/publish/` — they are build output.

## What TrafficLens is

TrafficLens is a **local-first, Windows-only network monitor**. It shows total
network throughput, per-application (per-process) traffic, active connections,
historical usage, threshold alerts, and offers a compact always-on-top Floating
Widget plus a system tray icon.

It is a **read-only observer**. V1 is monitoring-only by design: no blocking, no
bandwidth limiting, no firewall manipulation, and it never captures packet
payloads.

- **Platform:** Windows 10/11, **x64 only** (`Platform=x64` is mandatory)
- **Runtime:** .NET 8 (`net8.0-windows10.0.19041.0`)
- **UI:** **WinUI 3** (unpackaged, no MSIX) via Windows App SDK 2.5.1
- **Pattern:** MVVM + `Microsoft.Extensions.DependencyInjection`
- **Storage:** SQLite for history; JSON for settings
- **Languages:** English + **Persian (fa-IR)**, with full RTL support
- **Backend:** IP Helper tables, `GetIfEntry2` adapter counters, and **ETW** for
  per-process accounting

## Current state (short version)

| | |
|---|---|
| Version | **v0.1.4** |
| Branch | `feature/winui3-migration` |
| Release/docs HEAD | `67c334e06aa0d567f50a75992bb042feec292a41` |
| Final app source | `fd7c1762361d93c8dff5adb44d091c9dd07c3ec9` |
| Status | **FINAL LOCAL RELEASE** — human UI verification PASSED |
| Tests | **731/731 pass** (App 270 / Network 212 / Infrastructure 84 / WinUI 165) |
| Release build | 0 warnings, 0 errors |
| Signing | **UNSIGNED** (deferred) |
| GitHub | **NOT CONFIGURED** (deferred) — no remote exists |

Full detail, including artifact paths and SHA-256 values, is in
`handoff/CURRENT_STATE.md`.

## Where to find future tasks

`TASKS.md` is the task list of record. Task IDs (`TL-0xx`, `WUI-0xx`) are stable —
never renumber them. Longer narrative history lives in `CHANGELOG.md` and
`docs/PROJECT_STATUS.md`.

Planned/deferred work is listed in `handoff/NEXT_TASKS.md`. Known traps and
limitations are in `handoff/KNOWN_ISSUES.md` — **read that before touching
packaging, the title bar, RTL layout, or the network collectors.**

## Freebuff / OpenCode quick start

For a new Freebuff or OpenCode session, read **ONLY**:

```
handoff/START_HERE.md
handoff/CURRENT_STATE.md
TASKS.md
```

Then run:

```
git status
git log -5 --oneline
```

Do **not** scan the whole repository. Wait for a specific task before loading any
additional files.

## Rules for future agents

1. Run `git status` before making changes. If uncommitted work exists, run
   `git diff` and understand it first.
2. **Preserve unfinished valid work.** Never `git reset --hard`, `git checkout --`,
   `git clean`, or `git revert` casually to "get back to a clean state."
3. Root-cause fixes only. Do not paper over a symptom or add a special case that
   hides a deeper bug.
4. **No duplicate network polling pipelines.** One collector, one poll loop, one
   owner per signal. Reuse the existing services and events.
5. Avoid unnecessary timers, pollers, background workers, or positioning loops. A
   self-removing event handler is preferred over a repeating timer.
6. Preserve low CPU / RAM / battery usage. TrafficLens is expected to idle cheaply.
7. **Never capture packet payloads.**
8. Reuse existing services and events instead of adding parallel mechanisms.
9. **Technical values stay LTR in the Persian UI** (numbers, units, rates, IPs) even
   though the surrounding layout is RTL.
10. **The UI is locked.** It passed human visual verification for v0.1.4. Any UI
    change requires new human visual verification before it can be called done.
11. Update `docs/PROJECT_STATUS.md`, `TASKS.md`, and `CHANGELOG.md` after completing
    work. The release record lives there.
12. Run the relevant tests and the build before reporting PASS. Never fabricate a
    measurement or a test result, and never hide a failure.
13. Do not expose secrets. No key material, certificates, or credentials in the repo.
14. Leave the working tree clean when you finish.

## Handoff folder

| File | Purpose |
|---|---|
| `START_HERE.md` | This file. Orientation and rules. |
| `CURRENT_STATE.md` | The exact final v0.1.4 state, artifacts, hashes. |
| `ARCHITECTURE_MAP.md` | How the pieces fit together and how data flows. |
| `IMPORTANT_FILES.md` | Which files to open for a given task. |
| `BUILD_AND_TEST.md` | Exact commands, environment, PowerShell caveats. |
| `KNOWN_ISSUES.md` | Current limits, deferred work, and fixed traps to avoid. |
| `NEXT_TASKS.md` | Legitimate future work and its dependencies. |
