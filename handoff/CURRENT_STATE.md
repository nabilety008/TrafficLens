# CURRENT_STATE — TrafficLens v0.1.6

**Version:** `0.1.6` (**Product/Informational 0.1.6**, **File/Assembly 0.1.6.0**)
**Scope:** post-v0.1.5 fix release, no new features
**Human UI:** **PASS** — Applications "Sort By" default visibly selected; selection
survives the fa-IR ↔ en-US culture switch; selection survives navigate-away/back
**Tests:** **895/895 PASS** (App 270 / Network 266 / Infrastructure 141 / WinUI 218)
**Release build:** **0 warnings / 0 errors**
**Portable smoke:** **PASS** — clean extraction of the final ZIP, all 7 pages rendered, About = `0.1.6`, 0 log errors
**History CSV export:** automated + runtime verified — real file created, exact
raw-byte schema preserved, success message shown, false error absent, Cancel silent
**Signing:** **UNSIGNED** (no certificate created; reported honestly)
**GitHub:** **DEFERRED / NOT CONFIGURED** (no git remote exists)
**Code signing / GitHub publication / updater:** **DEFERRED**

### v0.1.6 contents

Two defects reported against v0.1.5, both fixed at the root in `3a2bdce`:

- **Applications "Sort By" blank default** — the last ComboBox still binding
  `SelectedValue` into a collection cleared/refilled on every localized rebuild.
  Now `SelectedIndex` over a canonical fixed-order key list, re-announced after the
  rebuild, rejecting `-1`/out-of-range indices. Sort order, the `TotalRate`
  default and raw numeric comparison are unchanged.
- **History CSV false error after success** — the success-state update shared a
  `try` with the failure-reporting `catch`, and the status methods raised
  `PropertyChanged` after `Dispose`. Split into picker/start, write and post-write
  phases; real write failures still log + surface; Cancel still silent.

Also carried from earlier post-v0.1.5 work already in HEAD: the Applications
data-size display precision improvement (adaptive 3-significant-digit sizing).

### Known non-blocking note (pre-existing)

The History CSV export writes **no UTF-8 BOM** although the writer requests one.
This predates the false-error fix, the CSV content is valid and parses correctly,
and it is deliberately **not** changed in v0.1.6.

### Release artifact metadata (FINAL v0.1.6)

| | |
|---|---|
| Installer | `artifacts/installer/TrafficLens-Setup-0.1.6-win-x64.exe` |
| Installer size | 89,963,560 bytes |
| Installer SHA-256 | `3332235ABB7B7ADA857EA3D0D41273F5E51D74A1B6DAE96B98ED83F4740CAB8E` |
| Portable ZIP | `artifacts/portable/TrafficLens-Portable-0.1.6-win-x64.zip` |
| Portable size | 129,632,776 bytes |
| Portable SHA-256 | `9F9375B505961CA85778F09187C3D5EB2674AAE6204B49A5E198628590555795` |
| Entry point | `TrafficLens.WinUI.exe` (WinUI 3, unpackaged, self-contained) |
| Shipped FileVersion | `0.1.6.0` |
| Shipped ProductVersion | `0.1.6` |
| About page (verified live) | `0.1.6` |
| ZIP entries | 815 — no PDB, no `.cs`/`.xaml`, no test assemblies, no MSIX, forward-slash paths only |
| fa-IR payload | present (3 satellite/resource entries) |
| WPF shell in payload | absent (correct — WinUI is the only entry point) |
| Signature | **UNSIGNED** (`NotSigned`) for both installer and app |

These hashes describe the **unsigned** release candidate.

### Clean portable smoke (FINAL v0.1.6) — PASS

Performed against a fresh extraction of the final ZIP in a new temp directory,
launching **only** the extracted `TrafficLens.WinUI.exe`:

- App started clean; window `TrafficLens - Network Monitor`; ~199 MB working set.
- All seven pages rendered via UI Automation: Dashboard, Applications,
  Connections, History, Alerts, Settings, About.
- **About reports version `0.1.6`.**
- Applications **Sort By shows `Total rate`** (not blank) in the packaged build,
  and the selection survives navigating away to Dashboard and back.
- **0 Error-level log entries** for the whole session. The single Warning is the
  expected ETW-requires-Administrator notice (the smoke ran non-elevated).
- Temp extraction removed afterwards; no `TrafficLens` processes left running.
- Note (not a defect): closing the window does **not** terminate the process —
  `CloseToTray` defaults to true, so `MainWindow` hides to the tray and the real
  exit is the tray **Exit** command. The tray-exit path was not exercised in this
  smoke.

### v0.1.5 remains immutable

Re-verified after v0.1.6 packaging — both unchanged:

| | |
|---|---|
| `TrafficLens-Setup-0.1.5-win-x64.exe` | 89,964,207 bytes, SHA-256 `545468B7547FD13C5A1CA181057BAFFF67715F90F5B4EBA3D241D86403D1D4BC` |
| `TrafficLens-Portable-0.1.5-win-x64.zip` | 129,633,006 bytes, SHA-256 `01836AF597F12C38C3E48DD3CD6B2A88CE89BF3B00CAA71E2640C630B962CC9B` |

---

> **HISTORICAL RECORD — v0.1.4 and the post-v0.1.4 batches.** Everything below
> this line is the earlier record, kept for provenance.
>
> **ADDENDUM (post-v0.1.4 audit, 2026-09-30) — read this first.** The v0.1.4
> release state below is still accurate for the shipped artifacts, but the
> branch has moved on. Four post-v0.1.4 batches are committed on
> `feature/winui3-migration`, current HEAD **`513568a`**
> (`fix(winui): hold feature updates without disabling security updates`):
>
> | Batch | Scope | Commit(s) |
> |---|---|---|
> | 1 | Connections UI + History CSV repair | `4703881` |
> | 2 | Graph scale + live/history hover | `79bcb43` |
> | 3 | Caption-button palette + Alerts polish/regroup | `65a5185`, `319722b` |
> | 4 | Windows feature-update hold (replaces NoAutoUpdate kill switch) | `513568a` |
>
> Latest quality gate: full suite **832/832 PASS** (App 270 / Network 237 /
> Infrastructure 136 / WinUI 189); Release x64 build **0 warnings / 0 errors**.
> Tests, docs (`TASKS.md`, `CHANGELOG.md`, `docs/PROJECT_STATUS.md`,
> `docs/DECISIONS.md` ADR-026) and the current source all live at `513568a`.
> **Current source is NOT identical to the v0.1.4 binaries** — the next release
> needs a new version number and a fresh human visual pass (the v0.1.4 UI lock
> has been superseded by batch 1–4 UI changes).
> Still pending human checks: caption glyphs light theme/hover, hover callouts,
> the regrouped Alerts layout, Connections/CSV hands-on pass, and the real
> elevated Windows Update hold/release registry round-trip (never performed —
> UAC approval was not granted; the machine registry is untouched).
> Everything below the addendum is the untouched v0.1.4 release record.

**Version:** `0.1.4`
**Status:** **FINAL LOCAL RELEASE**
**Human UI:** **PASS**
**Tests:** **731/731 PASS** (App 270 / Network 212 / Infrastructure 84 / WinUI 165)
**Release build:** **0 warnings / 0 errors**
**Portable smoke:** **PASS**
**Installer:** generated and statically validated
**Installer install / upgrade / uninstall:** **NOT TESTED**
**Signing:** **UNSIGNED / DEFERRED**
**GitHub:** **DEFERRED / NOT CONFIGURED** (no git remote exists)
**GitHub updater:** **DEFERRED UNTIL GITHUB PUBLICATION**

## Git state

| | |
|---|---|
| Branch | `feature/winui3-migration` |
| HEAD at closeout | `67c334e06aa0d567f50a75992bb042feec292a41` |
| HEAD subject | `docs(release): finalize TrafficLens v0.1.4 release metadata` |
| Final application source | `fd7c1762361d93c8dff5adb44d091c9dd07c3ec9` |
| Working tree | clean |
| Remotes | none |
| Existing tags | `v0.1.0`, `v0.1.1`, `v0.1.2` |
| `v0.1.4` tag | **not created** (deferred) |
| `master` | `afb1f6be87da9b4d999f65685d267e41b83a70d6` ("release: TrafficLens 0.1.2") |

The `handoff/` folder is added by a **documentation-only** commit on top of
`67c334e`. It changes no application code, no tests, no packaging, and no
artifacts. The released application source remains `fd7c176`.

### Merge status (not performed)

`master` is a direct ancestor of the release branch, so the pending release work
merges as a **fast-forward** with no conflicts (58 commits ahead, 0 behind;
`git merge-tree` exits 0). No merge, no tag, no push was performed — all
intentionally deferred.

## Final artifacts

Both files exist under `artifacts/`, which is gitignored (`.gitignore:10`) and is
therefore **not** preserved by git. Copy them somewhere durable before this
directory is cleaned.

### Installer

```
artifacts\installer\TrafficLens-Setup-0.1.4-win-x64.exe
artifacts\installer\TrafficLens-Setup-0.1.4-win-x64.exe.sha256
```

- Size: **89,953,854 bytes** (85.79 MB)
- SHA-256: `A982E268E13AB03DD36BBEA335947CC886FF50170E255391D1D935F00E52D70E`
- Sidecar hash matches the recomputed file hash
- `Get-AuthenticodeSignature` → **`NotSigned`**
- AppId `{8F0E8A8F-7B1D-4A5E-9C2D-3E5F6A7B8C9D}`, entry point `TrafficLens.WinUI.exe`

### Portable

```
artifacts\portable\TrafficLens-Portable-0.1.4-win-x64.zip
artifacts\portable\TrafficLens-Portable-0.1.4-win-x64.zip.sha256
```

- Size: **129,618,364 bytes** (123.61 MB)
- SHA-256: `03A41E32F5A76837C8E58A1AC6D1EC557E4471A9D0F979669C511AC2BFA61D24`
- Sidecar hash matches the recomputed file hash
- 815 entries, all forward-slash entry names

### Publish payload

```
artifacts\publish\win-x64\
```

- 815 files
- `TrafficLens.WinUI.exe` 297,984 bytes, `TrafficLens.WinUI.dll` 499,200 bytes
- `TrafficLens.WinUI.pri` 2,231,888 bytes, header `mrm_pri2`
  (`6d 72 6d 5f 70 72 69 32`), SHA-256
  `5F286940DDC6AE04C8904DB78597A0664164B344CB8AF9308728EBCDC95756C6`
- 0 PDBs, 0 test assemblies, no `.cs`/`.xaml` sources, no `obj`/`bin` leftovers
- No superseded WPF `TrafficLens.exe` payload
- `fa-IR\TrafficLens.WinUI.resources.dll` present; English is the embedded
  neutral/default culture
- Branding assets byte-identical to `assets\branding\`

## Version metadata

| Field | Value |
|---|---|
| `Version` (Directory.Build.props) | `0.1.4` |
| `InformationalVersion` / `ProductVersion` | `0.1.4` |
| `AssemblyVersion` / `FileVersion` | `0.1.4.0` |
| About page | reads the version from assembly metadata (no hard-coded string) |

## Verification performed at closeout

- Full solution suite: 731/731 pass, Release x64, 0 warnings / 0 errors
- Artifact existence and SHA-256 recomputation — both match sidecars
- PRI size, `mrm_pri2` header, and hash verified
- Portable ZIP structure: 815 entries, forward-slash names, no forbidden content
- Installer configuration verified statically in `packaging\TrafficLens.iss`
- **Runtime smoke test against the extracted final portable ZIP** (not a
  `bin\Release` build), `fa-IR`, one launch:
  - window opened, `WinUI MainWindow activated` — proving the PRI loaded and
    `InitializeComponent()` succeeded
  - culture `fa-IR`, NavigationView rendered all 7 items
  - shell widget quick action held 29 physical px relLeft at 150% DPI
  - title-to-caption-button gap **+35 px** (clear of the native controls)
  - Floating Widget opened 510x210 at its persisted position, values verifiably
    live (11.14 KB/s, then 2.82 KB/s ten seconds later), then disabled cleanly
  - 3 pages navigated, process alive at the end, `settings.json` unchanged
  - application log: 0 Error; only the documented non-elevated ETW warning
- Human visual verification of the widget title bar, window/widget icon state and
  Persian RTL layout: **PASS** (2026-09-28)

## What was deliberately NOT done

- No GitHub repository, remote, push, GitHub Release, or publication
- No code signing, no certificate, no Windows security change
- No merge to `master`, no `v0.1.4` tag
- No updater implementation
- No rebuild or modification of the artifacts above

See `handoff/NEXT_TASKS.md` for what is queued and in what order.

---

# ADDENDUM 2 — v0.1.5 release candidate (2026-09-30)

Recorded from actual repository and artifact state at v0.1.5 RC closeout.

**Version:** `0.1.5` (Directory.Build.props + TrafficLens.iss)
**Status:** **RELEASE CANDIDATE PREPARED** — final tag not yet created
**Tests:** **837/837 PASS** (App 270 / Network 237 / Infrastructure 141 / WinUI 189)
**Release build:** **0 warnings / 0 errors**
**Signing:** **UNSIGNED / DEFERRED**
**GitHub:** **DEFERRED / NOT CONFIGURED** (no git remote exists)

## What v0.1.5 contains (vs v0.1.4 at fd7c176)

1. Batch 1 (4703881): Connections defaults/filter polish, Hide Listeners
   cleanup, reverse-DNS removal, History CSV diagnosability.
2. Batch 2 (79bcb43): live-graph Y-axis scale labels + live/history hover
   callouts with forced-LTR graph rendering.
3. Batch 3 (65a5185, 319722b): caption-button palette for the custom title
   bar; Alerts page regrouped into one configuration container + separate
   triggered-alerts container.
4. Batch 4 (513568a, 23284fe, f958c42, 9c7a291): Windows Update redesigned as
   a feature-update hold (Target Release Version policy only; never disables
   security/quality updates, services, BITS or Defender; external policy never
   overwritten; ownership record with exact rollback and legacy migration).
   Live-debug fixes proven on the real machine: single-op elevated reg.exe
   commands with exit-code verification, and previously-absent owned values
   deleted through the elevated runner.

## Human verification recorded (2026-09-30)

- Windows Update LIVE Hold → Release round-trip: **HUMAN PASS** — hold wrote
  exactly the three target-release values; release restored the exact original
  baseline (0 values / 0 subkeys, AU absent, ownership record removed,
  services unchanged).
- Alerts final regrouped layout: **HUMAN PASS**.
- Live graph real-mouse hover callouts: **HUMAN PASS**.
- History real-mouse hover callouts: **HUMAN PASS**.
- v0.1.4 human PASS (2026-09-28) covers the earlier locked surfaces.

## Still pending (accurate, not invented)

- Caption-button glyphs in LIGHT theme and hover/pressed states: not yet
  independently human-confirmed (dark theme verified).
- Connections page + CSV export hands-on human pass: agent-verified only.
- Code signing (blocks public distribution, not the local build).
- GitHub publication and updater (no remote configured).
- v0.1.5 tag: awaiting explicit authorization.

## Temporary diagnostics decision

The WUI-014 per-command diagnostic file logging was REDUCED to failure-only
entries during release preparation: `windowsupdate-diag.log` now grows only
when an elevated operation fails (non-zero exit, canceled verification, launch
exception). Success writes nothing. Rollback verification and safety behavior
are unchanged.

See `CHANGELOG.md` ([0.1.5]) for the full change list and
`docs/PROJECT_STATUS.md` for the current milestone.

---

# ADDENDUM 3 — Applications display precision fix + HUMAN PASS (2026-10-01)

Post-RC production fix before tagging, from the user-reported cross-unit
ordering report (reported as "Connections"; the traffic-usage surface is
Applications).

## Investigation result

- Raw byte accounting: **correct, unchanged** (canonical raw `long` counters
  end to end).
- Raw-byte sorting/ranking: **correct, unchanged** — `ProcessSampleSort` and
  `ProcessSampleSelection` compare raw numeric values only; no string-based
  comparison exists anywhere in the pipeline.
- Root cause of the visual ambiguity: the shared `DataSizeFormatter` rounded
  `2047 B` to `"2 KB"`, colliding with `2048 B` = `"2 KB"`, so an in-Bytes row
  could look equal to (or bigger than) a genuinely larger KB row.
- Connections page has **no** traffic/usage display at all (verified in code
  and at runtime); no changes were made to Connections.

## Fix (0fe6775)

- Shared `DataSizeFormatter` now uses **adaptive truncated 3-significant-digit
  precision**: <10 → 2 decimals, <100 → 1 decimal, ≥100 → integer; truncation
  (never round-up) so a display can never imply ≥ the next boundary. Binary
  1024 convention and culture decimal separators unchanged. CSV exports raw
  byte values (verified untouched).
- Verified conversions: 900 B → `900 B`, 1023 B → `1023 B`, 1024 B → `1 KB`,
  1536 B → `1.5 KB`, **2047 B → `1.99 KB`**, 2048 B → `2 KB`,
  1023 KB → `1023 KB`, 1 MB → `1 MB` (KB→MB and MB→GB boundaries tested).
- Test gate at 0fe6775: focused 54/54, full **866/866 PASS**, Release x64
  build 0 warnings / 0 errors. 16 new regression tests lock in raw-byte
  ordering + formatter precision boundaries.

## Human verification recorded (2026-10-01)

- Applications traffic display precision: **HUMAN PASS** — visually verified
  the boundary set above on the fresh Release build; sorting/accounting
  confirmed raw-byte based.

Source fix commit: `5a60d30` (raw-byte ordering regression tests) +
`0fe6775` (adaptive formatter precision).

---

# ADDENDUM 4 — FINAL v0.1.5 release (2026-10-01)

Supersedes the RC artifact metadata in Addendum 2 (those artifacts are
STALE — built before `5a60d30`/`0fe6775`; do not reuse their hashes).

**Final source/release HEAD:** `605d798` — final tag target.
Production source identical to `0fe6775`; `605d798` adds documentation only.

## Test gate (source at 0fe6775)

- Focused (formatter + selection): **54/54 PASS**
- Full suite: **866/866 PASS** (Network 266 / Infrastructure 141 / App 270 /
  WinUI 189)
- Release x64 build (`-p:Platform=x64`): **0 warnings / 0 errors**
- Packaging inputs (`packaging/TrafficLens.iss`, `scripts/build-release.ps1`)
  unchanged since the verified RC prep; tests not rerun for the docs-only
  commit `605d798`.

## FINAL artifacts (regenerated at `605d798`, unsigned)

- Installer: `artifacts/installer/TrafficLens-Setup-0.1.5-win-x64.exe`
  — 89,964,207 bytes
  — SHA-256 `545468B7547FD13C5A1CA181057BAFFF67715F90F5B4EBA3D241D86403D1D4BC`
  — VersionInfo ProductVersion/FileVersion `0.1.5`; AppId
    `8F0E8A8F-7B1D-4A5E-9C2D-3E5F6A7B8C9D` and icon carried from the
    unchanged, previously validated `TrafficLens.iss`
- Portable: `artifacts/portable/TrafficLens-Portable-0.1.5-win-x64.zip`
  — 129,633,006 bytes
  — SHA-256 `01836AF597F12C38C3E48DD3CD6B2A88CE89BF3B00CAA71E2640C630B962CC9B`
  — 815 entries, forward-slash paths, `TrafficLens.WinUI.exe` + 1
    `TrafficLens.Core.dll` + 5 `.pri` + fa-IR resources + Windows App SDK
    self-contained layout + ico; no PDB/tests/.cs/.xaml-source/legacy WPF
    exe/MSIX junk
  — Fresh `TrafficLens.Core.dll` proven: hash inside extracted ZIP
    `77e45d8dddf07eb9ea7353892d074cdfda25f604e60a5d5b1a78942cba63659d`
    == freshly rebuilt `TrafficLens.Core` Release x64 output (stale
    `win-x64/` publish copy from the RC not used)
- Sidecar `.sha256` files written next to both artifacts.
- Signing: **UNSIGNED / DEFERRED**.

## FINAL portable smoke — from shipped ZIP (2026-10-01): PASS

Extracted to a clean temp dir; launched ONLY the extracted
`TrafficLens.WinUI.exe` (process path verified = extraction dir).
Dashboard rendered, Applications/Alerts/Settings opened via UIA, fa-IR
switch applied with RTL layout, About page reached, no startup error
logs in the extraction dir, fresh formatter fix present (DLL hash
above). Instance closed cleanly; temp extraction removed.

## Human QA recorded for FINAL v0.1.5

- Windows Update LIVE Hold → Release round-trip: **HUMAN PASS** (2026-09-30)
- Alerts regrouped layout: **HUMAN PASS** (2026-09-30)
- Live graph hover callouts: **HUMAN PASS** (2026-09-30)
- History hover callouts: **HUMAN PASS** (2026-09-30)
- Applications display precision (adaptive formatter, 2047 B → `1.99 KB`):
  **HUMAN PASS** (2026-10-01)

## Still deferred (accurate)

- Caption-button glyphs LIGHT theme hover/pressed: not independently
  human-tested (dark verified).
- Connections page hands-on QA: page carries no traffic usage display
  (verified); independent human pass still pending.
- CSV export hands-on QA: agent-verified only; raw byte values confirmed
  by code audit + tests.
- Code signing, GitHub publication, updater: deferred (no remote).
