# TrafficLens — AGENTS.md

Local-first, Windows-only .NET 8 network monitor. Read-only observer: no blocking,
no bandwidth limiting, no firewall manipulation, and it never captures packet
payloads.

## Before Making Changes — read in this order

**Do NOT scan the whole repository first.** This repo is large and deep; loading it
all wastes context and buries the answer. Read these, in order, and stop:

1. `AGENTS.md` (this file)
2. `handoff/START_HERE.md`
3. `handoff/CURRENT_STATE.md`
4. `TASKS.md`
5. **Only** the task-specific docs and source files

Then run `git status --short` and `git log -5 --oneline`. Wait for a specific task
before loading anything else. Do not open `bin/`, `obj/`, or `artifacts/publish/`.

The `handoff/` folder is the source of truth for the current state. This file is
deliberately short; the detail lives in `handoff/`, and it is not duplicated here.

| Handoff file | Read it for |
|---|---|
| `handoff/START_HERE.md` | Orientation, rules for future agents |
| `handoff/CURRENT_STATE.md` | Exact Git, artifact, test and verification state |
| `handoff/ARCHITECTURE_MAP.md` | How the pieces fit; data flow |
| `handoff/IMPORTANT_FILES.md` | Which file to open for a given task |
| `handoff/BUILD_AND_TEST.md` | Commands, environment, PowerShell caveats |
| `handoff/KNOWN_ISSUES.md` | Limits, deferred work, fixed traps |
| `handoff/NEXT_TASKS.md` | Ordered future work |

## Project

- Solution: `TrafficLens.sln`
- **UI: WinUI 3** (unpackaged, no MSIX) via Windows App SDK — **not WPF**
- Stack: C#, WinUI 3, MVVM, `Microsoft.Extensions.DependencyInjection`, SQLite, structured file logging
- Runtime: .NET 8
- Platforms: Windows 10/11, **x64 only**

### Current state

| | |
|---|---|
| Current UI | **WinUI 3** |
| Entry point executable | **`TrafficLens.WinUI.exe`** |
| WinUI project | `src/TrafficLens.WinUI/` |
| Tray project | `src/TrafficLens.WinUI.Tray/` |
| Branch | `feature/winui3-migration` |
| Local release | **v0.1.6** — FINAL LOCAL RELEASE, human UI verification PASS |
| Application source commit | `3a2bdce477f1b688a53d08b5897b7b00572d6978` (v0.1.6 fixes) |
| Tests | 895/895 pass (App 270 / Network 266 / Infrastructure 141 / WinUI 218) |
| Release build | 0 warnings, 0 errors |
| GitHub publication | **DEFERRED** — no remote exists |
| Code signing | **DEFERRED** — build is unsigned |
| GitHub updater | **DEFERRED UNTIL GITHUB PUBLICATION** |

## Project Layout

```
TrafficLens/
- src/
  - TrafficLens.WinUI/        CURRENT APP: WinUI 3 shell, pages, widget  (entry point)
  - TrafficLens.WinUI.Tray/   Standalone tray icon host (Windows Forms, x64)
  - TrafficLens.App/          LEGACY WPF shell - rollback/reference only, never published
  - TrafficLens.Core/         Domain models, interfaces, shared abstractions
  - TrafficLens.Network/      Windows network collectors, connections, process mapping
  - TrafficLens.Infrastructure/  SQLite, repositories, settings, logging
- tests/                      5 xunit projects + TrafficLens.Network.Verification (console harness)
- docs/
- handoff/
- scripts/                    build-release.ps1 and probe/lifecycle scripts
- packaging/                  Inno Setup 6
- assets/branding/
```

### The legacy WPF project

`src/TrafficLens.App/` is the **old WPF shell**. It is **not** the current app and
must **not** be treated as the entry point:

- **Rollback / reference only.** The current app is `TrafficLens.WinUI`.
- The release pipeline **fails** if a WPF `TrafficLens.exe` ever appears in publish
  output, because the WinUI application is the only release entry point.
- Do not "fix" the WinUI app by editing WPF views.
- **But it is not dead code.** The WinUI project *links* these files from it:
  `Services/AlertService.cs`, `AlertSettings.cs`, `AlertNotification.cs`,
  `IAlertService.cs`, and the `Resources/Strings.resx` + `Strings.fa-IR.resx`
  resources. Edit them where they live; do not fork a copy into the WinUI project.

### Reference rules

- `Core` references nothing internal.
- `Network` and `Infrastructure` each reference only `Core`.
- `App` and `WinUI` reference the layers they need. `WinUI.Tray` is standalone.
- No circular references.

### Target frameworks

| Project | Target |
|---|---|
| `TrafficLens.Core`, `Network`, `Infrastructure` | `net8.0` |
| `TrafficLens.WinUI.Tray`, `TrafficLens.App` | `net8.0-windows` |
| `TrafficLens.WinUI` | `net8.0-windows10.0.19041.0` |

## Build and Test

**`-p:Platform=x64` is mandatory, not cosmetic.** `TrafficLens.WinUI` declares
`<Platforms>x64</Platforms>`, `WindowsAppSDKSelfContained=true` and
`RuntimeIdentifiers=win-x64`. Without `-p:Platform=x64` the WinUI project does not
build. This is the most common build failure here.

### The dotnet on PATH cannot build this project

`C:\Program Files\dotnet\dotnet.exe` (what PATH resolves) has **zero SDKs
installed**. The working SDK is `C:\Users\ali\.dotnet\dotnet.exe` (SDK 8.0.425).
Setting `$env:DOTNET_ROOT` alone does **not** fix it — the PATH muxer still wins.

```powershell
# Option A - full path (most explicit)
$dn = "C:\Users\ali\.dotnet\dotnet.exe"
& $dn build "C:\Users\ali\Documents\New folder\TrafficLens\TrafficLens.sln" -c Release -p:Platform=x64

# Option B - prepend to PATH for the session
$env:PATH = "C:\Users\ali\.dotnet;$env:PATH"
dotnet build "C:\Users\ali\Documents\New folder\TrafficLens\TrafficLens.sln" -c Release -p:Platform=x64
```

```powershell
# Tests
& $dn test "C:\Users\ali\Documents\New folder\TrafficLens\TrafficLens.sln" -c Release -p:Platform=x64 --no-build

# Release pipeline (also needs Inno Setup 6; rebuilds and overwrites artifacts)
& "C:\Users\ali\Documents\New folder\TrafficLens\scripts\build-release.ps1" -Version 0.1.6
```

Do not run the release pipeline just to test a change: it overwrites the recorded
v0.1.6 artifact hashes. See `handoff/BUILD_AND_TEST.md` for restore, focused test
filters, launch paths, and PowerShell 5.1 caveats (no heredocs, no `&&`, never
`Set-Content` on repo files).

**Close a running app before rebuilding Release** — it locks the output DLLs.
Per-process (Applications) data needs an **Administrator** process.

## Conventions

- MVVM: Views in `Pages/`/`Views/`, ViewModels in `ViewModels/`, no logic in code-behind.
- Dependency injection: the composition root is `src/TrafficLens.WinUI/App.xaml.cs`.
  It is the only place that knows the full service list — register and start new
  services there, not from a page.
- User-facing strings come from `TrafficLens.App/Resources/Strings*.resx` via
  `ILocalizationService`. Never hard-code UI strings.
- Add a key to **both** `Strings.resx` and `Strings.fa-IR.resx`; a missing key
  renders as `[KeyName]`.
- Logging via `ILogger<T>` (structured JSON file logger in Infrastructure).
- Do not add comments unless necessary; keep code self-documenting.
- Unit conversion, aggregation, settings, history, and localization logic must be testable.
- After a milestone, update `docs/PROJECT_STATUS.md`, `TASKS.md`, and `CHANGELOG.md`.

## Architecture Rules — still binding

1. **No duplicate network polling pipelines.** One collector, one poll loop, one
   owner per signal. Never add a second timer, poller, or parallel collection path
   for the widget, tray, or any page.
2. **Avoid unnecessary timers, background workers, and positioning loops.** A
   self-removing event handler is preferred over a repeating timer.
3. **Preserve low CPU / RAM / battery usage.** TrafficLens is expected to idle cheaply.
4. **Reuse existing services and events** instead of adding parallel mechanisms.
5. **Never capture packet payloads.**
6. **ETW per-process data requires an Administrator process.** Without elevation it
   logs a warning and reports a degraded status — that is expected, non-fatal, and
   must not be "fixed" by making the app self-elevate.
7. **History uses real byte deltas between samples**, never a cumulative counter,
   so a counter reset cannot create a bogus spike.
8. **Technical values stay LTR in the Persian UI** (numbers, units, rates, IPs)
   even though the surrounding layout is RTL.
9. In RTL, window insets are reported in **flow** order — mapping a caption inset
   as a physical side is a known bug class. See `handoff/KNOWN_ISSUES.md`.

## Golden Rules

- LOCAL-FIRST, WINDOWS-FIRST, LIGHTWEIGHT, MODULAR, AGENT-FRIENDLY, LOCALIZATION-READY.
- **Never fabricate measurements or test results. Never hide a failure.**
- **Root-cause fixes only.** Do not paper over a symptom or add a special case that
  hides a deeper bug.
- **Preserve unfinished valid work.** Never `git reset --hard`, `git checkout --`,
  `git clean`, or `git revert` casually to "get back to a clean state".
- **Inspect `git status` and `git diff` before modifying anything.** If uncommitted
  work exists, understand it first.
- Do not remove working code unless required.
- Do not start a new task ID until the current one is verified.
- **The UI is locked** — it passed human visual verification for v0.1.6. Any UI
  change requires new human visual verification before it can be called done.
- **Never expose secrets.** No key material, certificates, or credentials in the repo.
- Leave the working tree clean when you finish.

## Task IDs

All significant work uses `TL-XXX` / `WUI-0xx` IDs (see `TASKS.md`). Never
renumber existing IDs.
