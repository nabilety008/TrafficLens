# TrafficLens — CHANGELOG

All notable changes are documented here in reverse chronological order.

## [Unreleased] — TL-018 (current-day hourly history, M13)

Branch `feature/tl018-hourly-history` off master; ready to merge on approval.

### Added (TL-018)

- **Today renders an hourly bar series.** `HistoryViewModel` now maps a
  per-hour series sourced from `HistorySnapshot.TodayHourly` when Today is
  selected; the chart title switches between a new localized `HourlyTrafficLabel`
  ("Hourly Traffic" / "ترافیک ساعتی") on Today and the existing
  `HistoryDailyTrafficLabel` otherwise.
- **`HourlyUsagePoint` + `HourlyHistoryBuilder` (Core).** Pure DST-safe UTC-slot
  aggregation: slots are counted from local-midnight-UTC, zero-filled, clamped
  to "now", and labelled by local hour. Spring-forward days yield 23 slots,
  fall-back days 25 (duplicated local label over distinct UTC slots), half-hour
  offset zones (e.g. +05:30) 12 slots.
- **`QuerySamplesAsync` repository query.** One bounded half-open UTC range over
  `traffic_samples` (built-in PK seek, no new index, verified by an
  `EXPLAIN QUERY PLAN` test); the snapshot build issues exactly one such query.
- **Tests (+16 → 432/432).** `HourlyHistoryBuilderTests` (8), repo range/empty/
  query-plan (3), service hourly-snapshot integrity (1), `HistoryViewModelTests`
  rewritten to 10.

### Changed (TL-018)

- `HistoryBarChartControl.Points` now carries `HistoryChartPoint` (label string +
  bytes) so the same native control draws both daily (`MM-dd`) and hourly
  (`HH:00`) labels; alerts fakes/tests updated for the extended snapshot.

### Scope (TL-018)

- Purely additive and read-time: no new timers (the flush-driven
  `HistoryChanged` refresh is reused), no schema change, no migration, no new
  collectors/write-path changes, `lifetime_totals` semantics untouched.

### Verified (2026-09-19, branch source)

- `dotnet build TrafficLens.sln` Debug + Release: **0 warnings / 0 errors**.
- **432/432 tests pass** (App 167 / Network 212 / Infrastructure 53).
- `scripts/tl018-verify.ps1` PASS (en-US + fa-IR chart titles and range
  buttons, resize, real-traffic data chain with exact daily/sample
  reconciliation, graceful exit, no orphans).

## [Unreleased] — TL-017 (performance audit, M12)

Branch `feature/tl017-performance` off master v0.1.1; **merged into `master` at
`21dabb9`.** Verbatim changelog of the audit.

### Changed (TL-017)

- **Connections page optimization (Phase 1).** In-place
  `ConnectionRowViewModel` updates keyed by the stable `ConnectionKey`, delta
  diffing over the native table snapshots, and a per-refresh icon budget — the
  row collection is no longer rebuilt every poll when only counters change.
  Dedicated 3-minute comparable probe: CPU avg **15.31% → 6.65%** (~57% lower),
  CPU max **41.41% → 16.81%** (~59% lower), WS drift **+24.29 → +9.48
  MB/3min** (~61% lower), Private drift **+23.26 → +8.00 MB/3min** (~66%
  lower).
- **Per-tick allocation reduction (Phase 2).** ~550 avoidable allocations per
  poll tick removed (~450 short-lived per-row strings, ~101 `ConnectionKey`
  records created twice per row update). GC capture: Gen0 0.20/s, Gen1/Gen2 0,
  ~1.53 MB/s allocation, ~0.8% time in GC, bounded heap. No CPU improvement
  claimed (within noise).
- **Lazy page instantiation (Phase 3).** The six page Views are created on
  first navigation instead of eagerly in `MainViewModel`/`MainWindow`; the
  internal mainVM + 6-view block dropped ~170 ms → ~70 ms. Wall-clock launch is
  **not** claimed faster (environment-dominated on this host).
- **SQLite history optimization (Phase 4).** Persistent background writer with
  prepared commands and a dedicated flush queue plus `lifetime_totals` schema
  v2: flush avg **7.4 → 1.3 ms**, p95 **9 → 2 ms** (~5.7x), and the Lifetime
  query is now O(1). `docs/DATABASE.md` updated for schema v2 + migration.
- **Idle tray optimization (Phase 5).** `SetActive` gating so hidden
  widget/tray surfaces stop re-rendering, and a widget dispose/recreate path.
  Idle allocation ~2.2 → ~0.5–1.1 MB/s, WS drift +4.7 → +2.4 MB/5min, threads
  16 → 15. Idle CPU avg 1.08% → 1.07% is explicitly within noise (not claimed).
- **Soak harness (Phase 6)** — `scripts/tl017-soak.ps1`. 60-min idle tray:
  CPU avg 0.97%, WS ~240 MB flat (**+0.2 MB** drift after warm-up), Private
  ~120 MB, handles 468–496. 30-min Connections: WS +14.5 first 15min → +2.5
  second 15min, Private +12.7 → +5.0 (deceleration). "No leak was observed
  during the measured soak window" — 60/30-minute soak, not a 24h soak.

### Added

- `docs/PERFORMANCE_AFTER.md` — authoritative TL-017 BEFORE vs AFTER report
  (measurement rules, per-phase results, summary table, correctness regression,
  known limitations, remaining manual validation). Linked from
  `docs/PERFORMANCE.md`.

### Fixed (found during Phase 7 final verification)

- **Schema v1→v2 migration lost the pre-upgrade Lifetime total (production
  data-correctness).** `Migrate()` created `lifetime_totals` but never seeded
  it, so upgrading a real v0.1.1 database (schema v1) reported a Lifetime
  history of 0 until new buckets arrived. The migration now back-fills
  `lifetime_totals` from the existing `daily_usage` sum (`INSERT OR IGNORE …
  SELECT SUM(…) FROM daily_usage`). New regression test
  `InitializeAsync_MigratesV1History_BackFillsLifetimeTotal`; `DATABASE.md`
  describes the migration.
- **`scripts/tl017-db-benchmark.ps1` (harness).** The generated benchmark
  console project was never built before `dotnet run --no-build`, and the same
  source was written to two files (`DbBenchmark.cs` + `Program.cs`), so a clean
  run could not start `DbBenchmark.exe`. Removed the duplicate source write and
  let `dotnet run` build the project; verified end-to-end (flush/query/prune).

### Known limitations (TL-017)

- **Elevated ETW long-run profiling was NOT measured** (non-elevated
  environment; UAC not automatable). Only the `PermissionDenied` fallback,
  stability, no-crash and no-orphan-session paths were verified. Documented as
  remaining manual validation — ETW profiling is not marked complete.
- Wall-clock startup and per-phase idle-CPU deltas are within environment/
  run-to-run noise and are explicitly not claimed.

### Verified (2026-09-19, branch source)

- `dotnet build TrafficLens.sln` Debug + Release: **0 warnings / 0 errors**.
- **416/416 tests pass** (App 163 / Network 212 / Infrastructure 41).
- `tl015-smoke.ps1` PASS, `tl016-verify.ps1` PASS, `tl017-connprobe.ps1` PASS
  (on retry — first attempt was a transient UIA timing flake, diagnosed and
  re-run clean).
- `build-release.ps1` full packaging pipeline succeeds at version 0.1.1
  (no tag created, no version bump).
- All TL-017 benchmark harnesses remain runnable.

## [0.1.1] — 2026-09-17 (first release after TL-016)

### Changed
- **Release version 0.1.0 → 0.1.1** — first released build that ships the
  TL-016 branding/About/Diagnostics work end-to-end. Version stays centralized in
  `Directory.Build.props` (`Version`/`AssemblyVersion`/`FileVersion`/
  `InformationalVersion`); the About page reads it at runtime, and
  `scripts/build-release.ps1` feeds it to the installer (`/DAppVersion`/
  `/DAppVersionShort`; the `.iss` fallback defines match).
- Installer `TrafficLens-Setup-0.1.1-win-x64.exe` built (Inno Setup 6.7.3):
  `SetupIconFile` brand icon + `VersionInfo*` metadata (ProductVersion 0.1.1,
  FileVersion 0.1.1).

### Verified
- `dotnet build TrafficLens.sln` Debug + Release: **0 warnings / 0 errors**.
- 410/410 tests pass (App 158 / Network 212 / Infrastructure 40).
- Published single-file win-x64 EXE metadata: ProductVersion 0.1.1, FileVersion
  0.1.1, FileDescription "TrafficLens Network Monitor".
- Installer SHA-256 `3FB5EE38238D497883755390C4659BFED5E3DDD8F9DD6F477FA776F29E68DE3B`.
- **Upgrade v0.1.0 → v0.1.1 in place:** `settings.json`, `data\trafficlens.db`
  and all logs preserved byte-identical; start-menu shortcut not duplicated;
  exactly one uninstall entry (DisplayVersion 0.1.1); HKCU Run untouched.
- Installed-app smoke (Dashboard, graph, Applications, Connections, History,
  Settings, widget, tray, en/fa switch, single-instance, graceful exit, no ETW
  orphan) PASS.
- About page shows **0.1.1**; brand icon on the EXE matches
  `assets/branding/TrafficLens.ico` at the 32 px frame (100% pixel match).
- `tl016-verify.ps1` PASS on the published 0.1.1 build (en-US labels, fa-IR
  content scan, History page, About page en + fa-IR, no English/Persian leaks,
  graceful exit, no orphan process/ETW).

## [Unreleased] — TL-016 (full Persian localization / M11, + product polish / M11a)

### Changed
- **No hard-coded user-facing strings remain.** The dashboard graph "now" label is
  now data-bound via `TrafficGraphControl.NowLabel` (a `DependencyProperty`,
  default `"now"`) to the localized `Dashboard.GraphNowLabel`, so it renders
  «اکنون» under fa-IR.
- **Status/error detail surfaces are localized.** Applications
  (`PermissionDeniedDetailLabel`, `MonitoringFailedDetailLabel`), Connections
  (`ConnectionsErrorDetailLabel`) and History (`HistoryErrorDetailLabel`)
  banners show localized Persian/English detail text instead of raw English
  provider messages. Raw detail (e.g. `_collector.LastError`) is no longer
  rendered; it stays in the application log. (ADR-024.)
- **Culture-aware History chart dates.** `ToString("MM-dd",
  CultureInfo.CurrentCulture)` — fa-IR renders Persian-calendar dates.
- Language endonyms (`English` / «فارسی») and the TrafficLens brand remain
  intentionally untranslated.
- **Navigation bar is now a `WrapPanel`** so the 7th (About) item cannot overflow
  the 640px minimum window width.
- **Tray/window/installer share one generated brand icon** (`assets/branding/`);
  the tray icon prefers the embedded 32px frame with the TL-011 runtime-drawn
  glyph as a fallback.

### Added
- **Brand icon pipeline (TL-016 continuation).**
  `scripts/generate-icons.ps1` renders the brand glyph (rounded dark square, cyan
  down / teal up arrow — the tray art) at 16–256 px and writes a multi-size ICO +
  PNGs under `assets/branding/`. Wired once in `TrafficLens.App.csproj`
  (`<ApplicationIcon>` + `<Resource>` pack URI) and consumed by the main window,
  floating-widget title glyph, tray icon, About page and installer
  (`SetupIconFile={#BrandIcon}` + `VersionInfo*` setup metadata). The old
  `packaging/placeholder.ico` is gone. See `docs/BRANDING.md` (ADR-025).
- **About page.** Localized 7th nav page: brand identity, version, runtime, OS,
  display language, data/settings/logs paths.
- **Diagnostics support.** `DiagnosticsInfo.Build` (pure, tested); Copy
  diagnostics to clipboard (never throws); Open logs folder. Version is read from
  `AssemblyInformationalVersionAttribute` so it stays correct under single-file
  publish (no IL3000).
- `scripts/tl016-verify.ps1` — GUI verification of the published build: en-US
  label render, fa-IR content scan (nav + dashboard + History page in Persian,
  no English leak), graceful exit, no orphan process/ETW.
- ADR-024 (localized detail surfaces) and ADR-025 (branding foundation / About /
  Diagnostics).

### Verified
- en + fa-IR resx both 161 keys, identical key sets (`RequiredKeys` test).
- 410 tests (App 158 / Network 212 / Infrastructure 40); Debug + Release 0
  warnings / 0 errors.
- `tl016-verify.ps1` PASS on the published single-file win-x64 build (en-US,
  fa-IR, About page, no English leak); TL-015 smoke regression PASS.
- Localization landed in `master` via merge `9dc8335`; the branding/About
  continuation is committed directly on `master`.

## [0.1.0] — 2026-09-17 (TL-015 complete — packaging / installer)

Versioning note: 0.0.1–0.0.17 are in-repo development milestones. **0.1.0 is the
first installer-driveable release** and the version baked into the produced
artifacts; it is centralised in `Directory.Build.props`.

### Added
- **Release pipeline** `scripts/build-release.ps1` — one command does restore →
  Release build (0 Warnings/0 Errors) → all tests → single-file self-contained
  win-x64 publish → artifact validation (size, PE x64, product/file version) →
  portable ZIP → Inno Setup compile → SHA-256 checksum. Artifacts land in
  `artifacts\` (gitignored); nothing binary is committed.
- **Single-file publish** — `PublishSingleFile=true`, native-libs self-extract +
  compression, `InvariantGlobalization=false` (the `fa-IR` satellite is bundled
  inside the exe), `AssemblyName=TrafficLens` so the process/exe/reg-entry is
  `TrafficLens`.
- **Per-user Inno Setup installer** (`packaging/TrafficLens.iss`, Inno Setup
  6.7.3) — `PrivilegesRequired=lowest`, installs to
  `{localappdata}\Programs\TrafficLens`, stable AppId, Start-Menu shortcut
  (desktop icon off by task), `Excludes: "*.pdb"` (no PDBs shipped),
  post-install launch, `[UninstallDelete]` only `dirifempty`. Running-app prompt
  (`[Code]`, WMI `Win32_Process` + `WbemObjectSet.Count`) shown in `wpReady` —
  the installer never force-kills; Inno's file-in-use dialog is the second
  safety net.
- **Clean product metadata** — `Directory.Build.props`:
  `IncludeSourceRevisionInInformationalVersion=false` (ProductVersion/FileVersion
  = 0.1.0, no git hash) and `AssemblyTitle=TrafficLens Network Monitor`
  (FileDescription); `app.manifest`: asInvoker, Windows 10/11, PerMonitorV2,
  longPathAware.
- `docs/PACKAGING.md` — full packaging documentation (decisions, per-user
  scope, release command, artifact naming, upgrade/uninstall user-data policy,
  checksum, security/untrusted-build caveats).
- ADR-023 (packaging: pipeline, single-file vs MSIX/WiX, per-user Inno, no PDBs,
  never-kill policy, user-data preservation).

### Verified
- Published-exe smoke (`scripts/tl015-smoke.ps1`) and installed-app
  verification (`scripts/tl015-installed-app.ps1`) on real Windows — all PASS:
  window render, dashboard + live rate, graph section, Applications,
  Connections, History, Settings persistence, floating widget, tray, fa-IR
  Persian + en-US fallback, single-instance, graceful exit, no orphan process,
  no orphan ETW session (`TrafficLens.SingleInstance` / up-again event naming).
- Install 0.1.0 → Start-Menu shortcut + HKCU uninstall entry; uninstall removes
  install dir + shortcuts + uninstall entry; reinstall and upgrade (0.1.0 →
  0.1.1 → 0.1.0) all exit 0 with **user data
  (`%LOCALAPPDATA%\TrafficLens\settings.json` + `data\` + `logs\`) preserved
  hash-identically**; single uninstall entry / single .lnk throughout; no PDBs
  in the install dir.
- Final 0.1.0 installer SHA-256
  `0E9F11865700342E4F49EBF6B77C18C9FCAD640E56E9773FD0D826BC803D6D0B`
  (`artifacts\installer\TrafficLens-Setup-0.1.0-win-x64.exe`, checksum sidecar
  `.exe.sha256`).
- Regression: **397 tests** (App 145 / Network 212 / Infrastructure 40), Debug +
  Release 0 warnings / 0 errors.

## [0.0.17] — 2026-09-16 (TL-014 stability & performance audit complete)

### Fixed
- **Periodic idle-CPU spikes eliminated (TL-014).** The 30-min soak harness
  (max CPU ≤15% single-core in 30 s windows) was failing with ~20%+ periodic
  peaks. Three independent sources were found (via dotnet-trace Speedscope)
  and fixed, with no new user-facing features and no weakened thresholds:
  - **DashboardViewModel dispatch storm:** `SpeedSampleReady` fires once per
    adapter per second; with 5 adapters, `OnSpeedSample` called `RefreshRates`
    5×/s, producing N layout passes, N `INotifyPropertyChanged` storms, and N
    graph-buffer appends per second. Added a `_refreshPending` flag +
    `CoalesceRefresh()` (single `Dispatcher.BeginInvoke` per second) so all
    per-adapter events merge into one `RefreshRates` call.
  - **ConnectionsViewModel always-on dispatch:** every ~1 s poll dispatched
    `OnConnectionsChanged` to the UI thread and rebuilt rows even while the
    Dashboard page was active. Added `_isActive` + `SetActive(bool)`, called
    from `MainViewModel.SelectPage`; while inactive the VM buffers the data
    without dispatching and refreshes immediately on activation.
  - **WindowsConnectionProvider native polling pause:** `RunLoopAsync` called
    `EnumerateOnce()` every second unconditionally (four `GetExtendedTcpTable`
    / `GetExtendedUdpTable` P/Invokes + process resolution for ~159 connections).
    Added `IConnectionProvider.SetPollingEnabled(bool)` +
    `WindowsConnectionProvider.SetPollingEnabled` so the loop is skipped
    entirely when the Connections page is hidden; `GetCurrentConnections`
    still returns the last good snapshot.
  - **Default deactivation:** the `MainViewModel` constructor calls
    `Connections.SetActive(false)` since Dashboard is the default page.
- **Result:** harness F2 soak **avg 3.43%, max 9.88%** (well under 15%);
  main thread idle on-CPU dropped from ~50% (22,449 samples/45 s) to ~1.9%
  (113 samples/60 s) — the only remaining work is one dashboard Arrange per
  second. F1/F3/F4 phases also pass; full harness log:
  `%TEMP%\opencode\tl014-v2.log`.

### Added
- `IConnectionProvider.SetPollingEnabled(bool)` (+ `FakeConnectionProvider`
  no-op stub in tests).
- Regression tests: SingleInstanceGuard (6), FileLoggerProvider retention (3),
  SpeedRateTracker sleep-gap.
- `docs/PERFORMANCE.md` — CPU-optimization findings, measurement methodology,
  and evidence.
- ADR-022 (idle CPU optimization: coalescing, page-visibility gating, native
  polling pause, default deactivation).

### Tests
- Total **397 tests** (App 145 / Network 212 / Infrastructure 40), Debug +
  Release 0 warnings / 0 errors.

### Verified
- `scripts/tl014-stability.ps1` (Release, real host): F2 30-min soak passes
  (avg 3.43%, max 9.88%); F1 idle, F3 memory (WS +5.8 MB, Private +15.5 MB),
  F4 navigation/widget/window/restart stress all green; no exceptions in the
  structured log; graceful exit with no orphan ETW sessions.

## [0.0.16] — 2026-09-16 (TL-013 complete — settings)

### Added
- Full Settings page (`SettingsView.xaml` + `SettingsViewModel` + DI
  code-behind), replacing the minimal TL-011 tray-options popup: General
  (language, start with Windows, start minimized, minimize/close to tray),
  Floating Widget (enable, always-on-top, show/hide buttons), and Alerts (all
  5 rule rows with enable checkbox, threshold field, unit combo, and a bounded
  cooldown 1–1440 minutes with inline validation).
- Staged-save model: numeric fields and dropdowns apply on **Save** (validate →
  one logical persist → runtime apply → `RefreshFromSettings`), tray toggles
  apply **immediately**, Reset stages defaults behind a Yes/No confirmation,
  with `SavedNotice` feedback and dirty tracking.
- `AlertRuleViewModel` per rule — threshold is typed in the displayed unit and
  persisted as invariant bytes; unit-switch breaks (KB/s, MB/s, GB/s, TB/s for
  speed; MB, GB, TB for daily).
- `IFloatingWidgetService.SetAlwaysOnTop(bool)` added; `SettingsViewModel.Save`
  now sets (not toggles) the widget topmost state so unchecking it actually
  persists and applies, while the tray menu keeps `ToggleAlwaysOnTop`.
- `IStartupRegistrationService`/`StartupRegistrationService` — HKCU Run value
  `TrafficLens` = quoted exe path (+ optional ` --minimized`), removes only its
  own value; `--minimized` CLI starts hidden to tray with a dedicated log
  marker.
- `JsonSettingsService` hardening: partial settings files merge with defaults,
  unknown keys are preserved on Save, malformed JSON falls back to defaults
  without crashing.
- Localization en + fa-IR for the entire Settings surface (nav/title/sections/
  labels/units/save/reset/confirm/change-saved); required-keys test extended.
- `scripts/tl013-verify.ps1` — real-Windows Settings GUI verification (A–I):
  partial-merge/unknown-key/malformed recovery, en→fa→en switch with restart,
  tray immediate-apply + close-to-tray, widget enable/topmost-off/hide/show +
  restart persistence, page-configured 256 KB/s threshold → real alert → Reset
  with native confirmation → defaults + history DB intact, HKCU Run create/
  quoted/`--minimized`/own-value-only removal, hidden `--minimized` start,
  restart persistence, graceful exit with no orphan ETW sessions.
- ADR-021 (settings architecture: staged-save vs immediate-apply, unit-of-
  entry storage, widget always-on-top set-vs-toggle semantics).

### Tests
- `SettingsViewModelTests` — Save staging, validation (cooldown bounds,
  threshold > 0), language apply, widget state via `SetAlwaysOnTop`, alert
  config persistence, reset-to-defaults. `JsonSettingsServiceTests` —
  default-merge, unknown-key preservation, malformed fallback.
  `Fakes.cs` gained `SetAlwaysOnTop` on the fake widget service; localization
  resource keys extended.
- Total **382 tests** (App 138 / Network 206 / Infrastructure 38), Debug +
  Release 0 warnings / 0 errors.

### Verified
- Real Windows GUI verification (`scripts/tl013-verify.ps1`, Release) green
  end-to-end (blocks A–I), including a native `Reset to Defaults` MessageBox
  discovered, answered via Win32 `WM_COMMAND`, and dismissed, with the traffic
  history SQLite DB untouched (same mtime) and defaults restored on Save.

## [0.0.15] — 2026-09-15 (TL-012 complete — alerts)

### Added
- Core alert domain (`TrafficLens.Core/Alerts`, no WPF/OS dependencies):
  `AlertType` (HighDownloadSpeed / HighUploadSpeed / DailyDownloadLimit /
  DailyUploadLimit / DailyTotalLimit), `AlertConfig` (immutable record;
  all 5 rules **disabled by default** with suggested thresholds 50 MB/s /
  20 MB/s / 50 GB / 20 GB / 100 GB and a 5 min cooldown), `AlertEvent`,
  `AlertSignal`, `AlertEngine` (pure, gate-locked, clock + `TimeZoneInfo`
  injected), `AlertHistoryBuffer` (session-only, capacity 100, newest-first).
- Speed rule semantics: triggers on **upward crossing only**; remaining above
  the threshold never repeats; dropping below re-arms but does not clear the
  cooldown, so flapping yields ≤ 1 alert per cooldown window (no spam).
- Daily usage rule semantics: at most once per **local calendar day**
  (DST-safe), with the last-triggered local date persisted and restored on
  startup so a same-day restart (or crash) never re-fires; the next day
  re-arms.
- `AlertService`/`IAlertService` (App, singleton) — subscribes the existing
  `SpeedSampleReady` (evaluated via `NetworkTrafficAggregator.AggregateRates`,
  ADR-009/010 policy) and the cached history snapshot on `HistoryChanged`
  (never touches SQL); raises `AlertRaised`, logs every trigger, clean
  `Dispose`. `AlertSettings` (flat settings keys + Load/Save plus persisted
  `alerts.lastTriggered.*` dates), `AlertNotification` +
  `AlertMessageFormatter`.
- Tray balloons: `ISystemTrayService.ShowAlert` / `SystemTrayService.ShowAlert`
  (Warning balloon, 8 s; dropped+logged only when the tray is unavailable);
  `BalloonTipClicked → OpenRequested` reuses the TL-011 singleton-restore
  handler.
- Alerts page: `AlertsViewModel` (count + TimeText + newest-first rows),
  `Views/AlertsView.xaml` (+ DI code-behind), Alerts nav button + host in
  `MainViewModel`/`MainWindow`, DI registrations and `AlertRaised → ShowAlert`
  wiring in `App.xaml.cs`.
- Localization en + fa-IR: `AlertsNavLabel`, `AlertsTitleLabel`,
  `AlertsNoAlertsLabel`, `AlertsCountFormat`, `AlertTitle`, `AlertType*` ×6,
  `AlertMsg*` ×5.
- `scripts/tl012-verify.ps1` — real-Windows alert verification (speed
  crossing/no-spam/re-arm, daily once-per-day + same-day-restart persistence,
  notification-while-hidden, regression + graceful exit).
- ADR-020 (local alert architecture: no SQL in the alert path, crossing-gated
  speed rules, once-per-local-day usage rules, session buffer).

### Tests
- `AlertEngineTests` (17 engine cases + 3 buffer tests) — upward-crossing-
  only, cooldown window, no-repeat-while-above, re-arm, once-per-day,
  DST-safe UTC+14 local-day identity, next-day re-arm, restore semantics.
  `AlertServiceTests` (10) — pipeline wiring, daily suspension when history is
  unavailable, persistence, dispose. `AlertsViewModelTests` (4). `Fakes.cs`
  gained `FakeHistoryService`/`FakeAlertService`/`FakeTrayService.ShowAlert`;
  localization resource keys extended.
- Total 349 tests (App 116 / Network 206 / Infrastructure 27), Debug + Release
  0 warnings / 0 errors.

### Verified
- Real Windows GUI verification (`scripts/tl012-verify.ps1`, Release): speed
  alert threshold 256 KB/s + controlled download → exactly one notification,
  no spam while above, re-arm after drop; daily total 10 MB → once per local
  day, persisted `alerts.lastTriggered.dailyTotal`, same-day restart → no
  repeat; notification delivered while hidden with the tray alive; default
  (alerts disabled) regression + graceful exit with no ETW orphans.

## [0.0.14] — 2026-09-15 (TL-011 complete — system tray)

### Added
- `ISystemTrayService` + `SystemTrayService` (App) — one WinForms `NotifyIcon`
  via `<FrameworkReference Include="Microsoft.WindowsDesktop.App.WindowsForms" />`
  (`UseWPF` kept; no `UseWindowsForms` global usings). Single icon created once,
  disposed only on real exit; tooltip `TrafficLens`; hidden
  `WindowsForms10..._ad1` message window as the in-process verification proxy.
- Runtime-drawn 32×32 tray icon — dark rounded square with `#4FC3F7`/`#26A69A`
  chevrons on `#1E1E2E`, readable 16–32 px (`GetHicon`+`Icon.FromHandle`,
  `DestroyIcon` on dispose).
- Tray menu: Open TrafficLens / Show-Hide Floating Widget / Always on Top
  (checkable) / separator / Exit. Relabeled in place on culture change (never
  recreated). Always on Top drives the same widget pin state; widget pin
  tooltip/accessibility binds the shared `AlwaysOnTopLabel`.
- `TrayBehavior` (pure) + settings `MinimizeToTray` / `CloseToTray` (defaults
  true) and persisted once-only `TrayCloseNoticeShown`; first close-to-tray
  balloon "TrafficLens is still running in the system tray."
- `ApplicationExitCoordinator` — single idempotent `RequestApplicationExit()`
  (latch → dispose tray+widget → `Application.Current.Shutdown()`); no
  `Environment.Exit`/`Process.Kill`; collectors/history/DI disposed via the
  container. `App.xaml` `ShutdownMode="OnExplicitShutdown"`.
- `MainWindow` — `Closing` routed via `TrayBehavior` (Exit path calls the
  coordinator; HideToTray cancels + hides + balloon), `StateChanged`
  minimize→hidden when `MinimizeToTray`, `OpenRequested` restores the same
  singleton window. Minimal `…` options popup with the two tray checkboxes in
  `MainViewModel` (TL-013 owns the full settings page).
- Localization en + fa-IR: `OpenTrafficLensLabel`, `ExitLabel`,
  `MinimizeToTrayLabel`, `CloseToTrayLabel`, `TrayCloseNoticeBalloon`; tray
  technical name stays LTR.
- `scripts/tl011-verify.ps1` (final), `tl011-debug.ps1`, `tl011-shelldump.ps1`,
  `tl011-uiaprobe.ps1`, `tl011-dblclk.ps1`, `tl011-keynav.ps1` (diagnostics).

### Fixes
- `CloseToTray=false` was closing the window without exiting (process stayed
  alive under `OnExplicitShutdown`); the `MainWindow.Closing` exit branch now
  calls `RequestApplicationExit()` (caught by real-Windows verification, see
  ADR-019).

### Tests
- `TrayBehaviorTests` (10) — close-action resolution, minimize/close flags,
  once-only notice, idempotent coordination; `ApplicationExitCoordinatorTests`
  (4) — exit latching + disposal ordering with fakes. `Fakes.cs` gained
  `FakeSettingsService`/`FakeTrayService`/`FakeFloatingWidgetService`;
  FloatingWidgetViewModel acquires `AlwaysOnTopLabel` localization test;
  localization resource keys extended.
- Total 317 tests (App 84 / Network 206 / Infrastructure 27), Debug + Release
  0 warnings / 0 errors.

## [0.0.13] — 2026-09-15 (TL-010 complete — floating widget)

### Added
- `FloatingWidgetViewModel` (App) — rides the existing live rate pipeline
  (`SpeedSampleReady`/`NetworkChanged`/`AdaptersChanged`; no second poll loop or
  timer), computes the ADR-009/010 aggregate via `NetworkTrafficAggregator`,
  formats Download/Upload/Total through `DataRateFormatter`, localized
  title + labels, `TogglePinCommand`/`CloseWidgetCommand`, dispatcher-marshalled
  updates, IDisposable (same pattern as `DashboardViewModel`).
- `FloatingWidgetWindow` (App) — 280×110 frameless always-on-top widget
  (`WindowStyle=None`, `ResizeMode=NoResize`, `ShowInTaskbar=False`), dark theme
  from shared `DarkTheme.xaml`, drag by empty area, pin toggle (📌/📍) + hide (✕)
  buttons, rate values forced LTR under RTL.
- `FloatingWidgetService` (App, singleton) — single widget instance;
  `Show`/`Hide`/`Toggle`/`RestoreIfEnabled`; repeat show activates (no
  duplicates); widget close hides only; `Dispose` really closes the window on
  shutdown (never keeps the app alive).
- `WidgetPositionHelper.Clamp` (App) — pure multi-monitor position recovery:
  union of monitor work areas, negative virtual-screen coords preserved
  (secondary monitor left of primary), off-screen/disconnected-monitor clamping;
  real areas from `SystemParameters.VirtualScreen*`.
- Settings via existing `ISettingsService` (`settings.json`):
  `FloatingWidgetEnabled`, `FloatingWidgetAlwaysOnTop` (default on),
  `FloatingWidgetLeft`, `FloatingWidgetTop`.
- Main UI: header toggle button with `Show`/`Hide Floating Widget` labels,
  `MainViewModel.ToggleFloatingWidgetCommand` + `FloatingWidgetToggleLabel`;
  `MainWindow.Closing` hides the widget so normal shutdown is unaffected
  (ADR-015/TL-007F); `App.xaml.cs` registers the service and calls
  `RestoreIfEnabled()` at startup.
- Localization: `FloatingWidgetLabel`, `AlwaysOnTopLabel`,
  `ShowFloatingWidgetLabel`, `HideFloatingWidgetLabel` in en + fa-IR.

### Tests
- `FloatingWidgetViewModelTests` (8) — aggregate→VM mapping, tunnel/down-adapter
  exclusion, rate formatting, culture re-format + localized labels, pin icon,
  dispose. `WidgetPositionHelperTests` (10) — in-bounds, off-screen right/bottom,
  negative coords, secondary-monitor-left (kept vs clamped), multi-monitor union,
  empty areas, oversized window. Localization resource keys extended.
- Total 302 tests (App 69 / Network 206 / Infrastructure 27), Debug + Release
  0 warnings / 0 errors.

## [0.0.12] — 2026-09-15 (TL-009 complete — SQLite history)

### Added
- Core history domain (`TrafficLens.Core/History`, no WPF/OS dependencies):
  - `HistoryRange` (Today / Yesterday / Last 7 Days / Last 30 Days / Lifetime),
    `TrafficUsage`, `DailyUsagePoint`, and immutable `HistorySnapshot`
    (`For(range)` derives per-range totals; `Unavailable(lastError)`).
  - `HistoryRangeCalculator` — half-open local-date ranges via
    `TimeZoneInfo.ConvertTimeFromUtc` → `DateOnly` (DST / local-midnight correct;
    Last7Days `(today-6, today+1)`, Last30Days `(today-29, today+1)`).
  - `TrafficHistoryAccumulator` — counter-sample DELTAS → per-UTC-minute buckets;
    first observation per adapter is baseline-only; non-negative deltas only;
    `DrainCompleted` (full minutes) vs `DrainAll` (open minute clamped 1..60).
  - Contracts `ITrafficHistoryRepository` + `ITrafficHistoryService`.
- Infrastructure (`TrafficLens.Infrastructure/History`):
  - `SqliteTrafficHistoryRepository` — schema v1 (`PRAGMA user_version`), WAL,
    `busy_timeout`, `Pooling=false`; tables `traffic_samples` +
    `daily_usage`; appends are single transactions guarded by
    `INSERT OR IGNORE` + `changes()==1` so **restarts/crashes can never
    duplicate history**; `daily_usage` kept forever, raw samples pruned after
    90 days on startup.
  - `TrafficHistoryService` — rides the existing `CounterSampleReady` events
    (never a second NIC polling loop), tunnel-excluding like the ADR-009/010
    aggregate, re-baselines on resets/reconnects/reboots (nothing fabricated),
    30 s background flush of completed minutes, drains + flushes on stop, and
    exposes a cached immutable `HistorySnapshot` (SQL never on the UI thread).
  - `HistoryServiceCollectionExtensions.AddHistoryServices(dbPath)`.
- `AppPaths` — DB at `%LOCALAPPDATA%\TrafficLens\data\trafficlens.db`;
  `EnsureDirectories` creates the `data` folder.
- App History page:
  - `HistoryViewModel` — five-range selector button row, Download/Upload/Total
    summary cards, "No history yet" overlay when Lifetime is zero, storage
    failure banner (`HistoryUnavailableLabel` + `LastError`).
  - `HistoryView` + native `HistoryBarChartControl` (`FrameworkElement`,
    `OnRender`, no chart library) — bars always oldest-left → newest-right under
    RTL; Today/Yesterday = 1 bar, 7d = 7 bars, 30d/Lifetime = 30 daily bars;
    tooltips show date → `DataSizeFormatter` totals.
  - `MainViewModel.ShowHistoryCommand` + localized nav label; `MainWindow`
    History host (Dashboard / Applications / Connections / History);
    `App.xaml.cs` registers the ViewModel/View and starts the history service.
- Localization: `HistoryLabel`, `HistoryDailyTrafficLabel`, `TodayLabel`,
  `YesterdayLabel`, `Last7DaysLabel`, `Last30DaysLabel`, `LifetimeLabel`,
  `HistoryNoDataLabel`, `HistoryUnavailableLabel` added to `Strings.resx` (en)
  and `Strings.fa-IR.resx`.
- Tests: new `TrafficLens.Infrastructure.Tests` project (27 tests: range
  calculator, accumulator, SQLite repository incl. restart-idempotent append,
  service) added to the solution; `HistoryViewModelTests` (6) and localization
  resource-key tests in `TrafficLens.App.Tests`.
- Verification: `--history` mode added to `TrafficLens.Network.Verification`
  (real collector + throwaway DB + real downloads + restart idempotency check).
- ADR-017 (durable aggregated history; minute buckets + daily rollup; restart
  idempotency; 90-day raw retention).

### Verified
- Build Debug + Release: **0 warnings, 0 errors**.
- Tests: **284/284 passing** (206 Network + 51 App + 27 Infrastructure).
- Real Windows verification (`--history`, live host, temp DB): a real 20 MB
  `speed.cloudflare.com` download recorded as Today = 20,182,568 B down /
  79,655 B up (peak ~5.78 MB/s observed); 30-day daily series correct; a second
  service instance over the same DB reported the identical Lifetime
  (`unchanged: true`) — restart cannot double-count. DB = 16,384 bytes after the
  runtime; `daily_usage` aggregates keep steady-state bounded even as raw minute
  samples archive.

## [0.0.11] — 2026-09-15 (TL-008 complete — active connections)

### Added
- Core connection model and selection (presentation-safe, non-mutating):
  - `ConnectionInfo` extended — nullable remote endpoint, `ConnectionAddressFamily`,
    process start-time identity (`ProcessStartTimeUtcTicks`), `ExecutablePath`,
    `IconAvailable`, `Timestamp`; new `ConnectionProtocol`, `ConnectionState`, and
    `ConnectionAddressFamily` enums.
  - `ConnectionKey` — stable identity `(Protocol, AddressFamily, LocalAddress,
    LocalPort, RemoteAddress?, RemotePort?, ProcessId)` for in-place row updates.
  - `EndpointFormatter` — culture-safe, always-LTR `address:port` formatting; the
    remote endpoint renders empty for a listening/unconnected socket (unspecified
    `0.0.0.0`/`::` + port 0) instead of a misleading peer (ADR-016).
  - `ConnectionFilter` (All/Established/Listening/Tcp/Udp/Ipv4/Ipv6),
    `ConnectionFiltering` (match + search), `ConnectionSort` (Default/Process/
    ProcessId/Protocol/State/Local/Remote) with deterministic tie-breaks.
  - `IConnectionProvider` extended — `ConnectionsChanged`, `LastError`,
    `GetCurrentConnections`, `GetActiveConnectionsAsync(ct)`, `StartAsync`,
    `StopAsync`, `IDisposable`.
- Network collection (`TrafficLens.Network/Connections`):
  - `NativeConnectionTableReader` — `GetExtendedTcpTable` (`TCP_TABLE_OWNER_PID_ALL`)
    + `GetExtendedUdpTable` (`UDP_TABLE_OWNER_PID`), IPv4 and IPv6; 4-byte
    little-endian entry-count header, per-row layouts (TCPv4 24 B, TCPv6 56 B,
    UDPv4 12 B, UDPv6 28 B), network→host port byte order, 64 KB buffer grown on
    `ERROR_INSUFFICIENT_BUFFER`.
  - `ConnectionTableParser` — pure static parsers over the native buffers
    (unit-tested with synthetic payloads; no live table required).
  - `ConnectionProcessResolver` — bounded cache (TTL 3 s, capacity 512, FIFO,
    negative caching) over `IProcessMetadataProvider`, keyed by full
    `ProcessInstanceId`; never throws.
  - `WindowsConnectionProvider` — ~1 s off-UI poll loop; partial-table failure is a
    warning (successful tables kept), total failure keeps the last good snapshot and
    sets `LastError`, any success clears it; `StopAsync` uses `ConfigureAwait(false)`
    (ADR-015).
- App Connections page and navigation:
  - `ConnectionsViewModel` + `ConnectionRowViewModel` — dispatcher-marshalled
    `ConnectionsChanged`, in-place row updates (rebuild only when the key sequence
    changes), per-refresh icon budget, error banner, empty state, Filter +
    Address-Family + Sort combo boxes and a search box.
  - `ConnectionSortOption` / `ConnectionFilterOption`; `ConnectionsView` (XAML +
    code-behind DI); `MainWindow` Dashboard/Connections navigation; `App.xaml.cs`
    registers the ViewModel/View and starts `IConnectionProvider`.
- Localization: en + fa-IR keys for column headers, filters, sort keys, TCP states,
  empty/error, and unknown process; endpoints stay LTR under RTL.
- Verification: `--connections` mode added to `TrafficLens.Network.Verification`
  (non-elevated; optional `TL_VERIFY_PORT` fixed listener port).
- Tests: `ConnectionTableParserTests`, `ConnectionKeyTests`,
  `ConnectionSelectionTests`, `EndpointFormatterTests` (Network) and
  `ConnectionsViewModelTests` (App).

### Verified
- Build Debug: **0 warnings, 0 errors**.
- Tests: **251/251 passing** (206 Network + 45 App).
- Real Windows verification (`--connections`, live host): 112 connections (78 TCP /
  34 UDP, 99 IPv4 / 13 IPv6); in-process listener observed as `Listen`; curl download
  attributed `Established` with correct PID/process name; cross-checked vs
  `netstat -ano` (TCP state histogram + UDP count match the MIB source;
  `Get-NetTCPConnection`'s `Bound` rows are cmdlet-synthesized, not in the table).
- GUI smoke (non-elevated): provider started (1 s poll), no exceptions, clean
  teardown (no orphan ETW session / leftover process).

## [0.0.10] — 2026-09-15 (TL-007F complete — shutdown deadlock fix)

### Fixed
- **Lingering `TrafficLens.App` after a graceful window close (TL-007F).** Root
  cause (proven via live repro + `dotnet-dump`): `WindowsNetworkTrafficCollector`
  is disposed on the WPF dispatcher thread during `App.OnExit` →
  `StopAsync().GetAwaiter().GetResult()`; `StopAsync`'s `await loop;` captured the
  `DispatcherSynchronizationContext`, so the continuation was posted to a
  dispatcher blocked in `GetResult()` — `StopAsync` never resumed, `Dispose`
  never returned, and the process stayed alive (no window, low CPU, single
  foreground thread). Fix: `await loop.ConfigureAwait(false)` so shutdown is
  deterministic from any thread/context (ADR-015); no `Environment.Exit`, no
  forced kill.
- Regression test `Dispose_FromNonPumpingSyncContext_DoesNotDeadlock` added
  (non-pumping `SynchronizationContext` + 5 s deadline) — fails (timeout) on the
  pre-fix code, passes with the fix.

### Verified
- Build Debug + Release: **0 warnings, 0 errors**.
- Tests: **193/193 passing** (163 Network + 30 App).
- Real Windows GUI (Release), after the fix:
  - **Non-elevated:** 3 consecutive launch → graceful close cycles; every cycle the
    process exited promptly (no residual process), no ETW session; log ends with
    `TrafficLens exiting` → `Process traffic collector stopped` → `Network traffic
    collector stopped` (this last line was the previously-missing/blocked one).
  - **Elevated:** ETW session `TrafficLensProcessTrace` Running → ~20 MB download
    while monitoring → graceful close → process exited, session gone from
    `logman query -ets`, clean shutdown log.
- The lingering-process known issue from 0.0.9 is resolved.

## [0.0.9] — 2026-09-15 (TL-007 complete — Applications-list UI)

### Added
- Core selection/formatting (presentation, no collector mutation):
  - `ProcessSampleSelection` + `ProcessSortKey` — filtering + seven sort keys
    (total/download/upload rate, downloaded/uploaded/total bytes, name) with
    deterministic tie-breaking (name, start time, PID); top-consumer helpers.
  - `DataSizeFormatter` — binary-unit byte totals (B/KB/MB/GB), culture-aware
    decimal separator, negatives clamped to 0 B (ADR-011 technical notation).
- App Applications page:
  - `ApplicationsViewModel` + `ProcessRowViewModel` — per-instance rows keyed by
    `ProcessInstanceId` (reused PID with a new start time = distinct row), in-place
    updates (no re-add/flicker), top-consumer cards, sort + search (name
    case-insensitive substring + PID prefix), localized status text for
    `Running`/`Exited`/unknown.
  - `ApplicationSortOption` + per-row state from the sample's `IsRunning` flag
    (supported by the engine's `Running-until-exit` revalidation semantics).
  - `ProcessIconResolver` — shell32 `SHGetFileInfo` P/Invoke
    (`SHGFI_ICON | SHGFI_LARGEICON`) + `CreateBitmapSourceFromHIcon` +
    `DestroyIcon`, frozen fallback, bounded FIFO cache (128), max 8 extractions
    per refresh; UI-thread only; no `System.Drawing` dependency.
  - `ApplicationsView` (XAML + code-behind DI): status banner (permission-deny +
    start-monitoring actions), top-consumer cards, sort ComboBox + search box,
    list header + `ItemsControl` rows (icon, name, PID, status, rates, totals).
- Navigation: `MainWindow` Dashboard / Applications nav buttons + `ContentControl`
  host switched by `MainViewModel.ShowDashboardCommand`/`ShowApplicationsCommand`.
- Startup: `App.xaml.cs` registers `ProcessIconResolver`, `ApplicationsViewModel`,
  `ApplicationsView`; starts `IProcessTrafficCollector` after the network
  collector (fire-and-forget with try/catch).
- Localization: Applications keys added to `Strings.resx` (en) and
  `Strings.fa-IR.resx` (fa, valid UTF-8) — per-process labels, sort/search,
  status texts, permission banner, and explicit Restart-as-Administrator action.
- Tests: 45 new (`ProcessSampleSelectionTests` 11, `DataSizeFormatterTests` 12
  cases, 4 new engine liveness tests — resolved-runs, exit-after-revalidation,
  PID-reuse, unknown-PID; `ApplicationsViewModelTests` 16; localized resource
  keys extended).
- ADR-014 (no automatic elevation; explicit restart-as-administrator).

### Verified
- Build Debug + Release: **0 warnings, 0 errors**.
- Tests: **192/192 passing** (162 Network + 30 App).
- Real Windows GUI (Release):
  - Elevated: ETW session `TrafficLensProcessTrace` Running with buffers; app
    survived ~20 MB/s-scale transfers; graceful `CloseMainWindow` →
    "TrafficLens exiting" → "Process traffic collector stopped" → ETW session
    closed cleanly (no orphan).
  - Non-elevated: permission-denied path logged; app usable; dashboard works; no
    ETW session; no crash; no auto-UAC.

### Known Issue
- After a graceful window close the `TrafficLens.App` process can linger (no
  window handle, low CPU) even though shutdown logs and the ETW session shutdown
  are clean. Reproduced elevated and non-elevated; investigation queued for the
  next milestone.

## [0.0.8] — 2026-09-14 (TL-007 Per-Process Traffic — collector milestone)

### Added
- `TrafficLens.Core` per-process contracts:
  - `ProcessTrafficCollectorStatus` — Stopped / Starting / Running / PermissionDenied / Failed.
  - `IProcessTrafficCollector` extended with `Status` and `LastError`.
  - `ProcessTrafficSample` — pid, process-start-time identity, name, executable path,
    icon-availability, cumulative byte totals, monotonic-window rates, timestamp.
- `TrafficLens.Network/Process/` — real ETW-backed collector:
  - `WindowsEtwProcessTrafficCollector` — real-time kernel session
    (`TraceEventSession` + `NetworkTCPIP`); eight Tcp/Udp IPv4/IPv6 handlers map the
    **payload PID** + size into `NetworkTransferEvent`; ~1 s snapshot loop raises
    `SamplesReady`; non-elevated run reports `PermissionDenied` + `LastError`
    without crashing or forcing UAC; clean `StopAsync` (session + consume + loop).
  - `ProcessTrafficAccountingEngine` — per-instance buckets
    (`ProcessInstanceId = pid + start time`), monotonic sliding-window rates,
    metadata resolve/rekey, PID-reuse isolation, `<unknown pid N>` bucket for
    unresolvable processes (never merged), idle-prune 120 s, 4096 cap, next-check
    throttling (metadata revalidation 15 s / unresolved retry 10 s);
    allocation-free hot path.
  - `WindowsProcessMetadataProvider` — guarded `Process` reads; PID-reuse detection
    via start-time mismatch (2 s tolerance); never throws.
  - Supporting models: `NetworkTransferEvent`, `ProcessInstanceId`,
    `ProcessMetadata`/`ProcessMetadataResult`, `ProcessProtocolTotals`
    (Tcp/Udp × Received/Sent, IPv4/IPv6 × Received/Sent).
- DI: `IProcessTrafficCollector` registered as a singleton in
  `NetworkServiceCollectionExtensions`.
- Verification console `--process` mode (per-process live check).
- ADR-013 (ETW mechanism, privilege behavior, PID-reuse, VPN semantics) and
  `NETWORK_COLLECTION.md` TL-007 section.
- Tests: 18 new (`ProcessTrafficAccountingEngineTests` + `WindowsProcessMetadataProviderTests`).

### Verified
- Build Debug + Release: **0 warnings, 0 errors**.
- Tests: **147/147 passing** (133 Network + 14 App).
- Real elevated verification (curl.exe + powershell.exe): two distinguishable
  apps; two curl instances as distinct (pid, start) buckets; protocol-totals
  invariant holds; bounded (11 samples, ~2.6 MB growth); clean stop.
- Non-elevated verification: `PermissionDenied` + `LastError`, no crash.

## [0.0.7] — 2026-09-14 (TL-006 Live Traffic Graph)

### Added
- `TrafficLens.Core/Graph/` — no-WPF graph layer over raw bytes/second:
  - `TrafficGraphPoint` — timestamped raw download/upload rates (no formatted strings
    in graph data; formatting is render-only).
  - `TrafficSampleBuffer` — bounded (5.5 min retention / 1320 samples) thread-safe ring
    buffer; duplicate same-poll timestamps rejected (one sample per poll regardless of
    per-adapter events); wall-clock age-out; non-mutating `Slice(window, now)` so range
    switching never clears history; real timestamps preserved (gaps drawn honestly).
  - `AdaptiveGraphScale` — single shared Y max for both series; immediate spike growth;
    hysteretic shrink (consecutive sustained lows < 35% only) to prevent flicker; 2 KB/s
    floor prevents divide-by-zero at zero traffic (ADR-012).
  - `GraphTimeRange` — 30 s / 60 s / 300 s.
- `TrafficGraphControl` (App `Controls/`) — lightweight native WPF `FrameworkElement`;
  renders both series as `StreamGeometry` in `OnRender` over a 4-line grid with adaptive
  axis labels (`DataRateFormatter`); forces LTR so the timeline is always oldest-left →
  newest-right even under fa-IR; no chart library, no per-sample UI elements.
- `DashboardViewModel` graph support: feeds the existing ADR-009/010 `AggregateRates`
  aggregate into the buffer once per poll (dedupe absorbs multi-adapter burst events);
  `SelectGraphRangeCommand`; localized graph labels; culture-change re-render.
- `MainWindow` "Live Traffic" section: range buttons (30 s / 1 m / 5 m), download/upload
  legend swatches, graph control (height 190, scrollable with the dashboard).
- en + fa-IR resources: `GraphLiveTrafficLabel`, `GraphLast30SecondsLabel`,
  `GraphLast1MinuteLabel`, `GraphLast5MinutesLabel`, `GraphNowLabel`,
  `GraphDownloadSeriesLabel`, `GraphUploadSeriesLabel`.
- Tests: `TrafficSampleBufferTests` + `AdaptiveGraphScaleTests` (20) in
  `TrafficLens.Network.Tests`; dashboard graph tests (append/dedupe/range-slice/
  no-network/zero/large, 6) in `TrafficLens.App.Tests`; graph keys added to the
  localization resource test.
- ADR-012 (native WPF rendering + documented scale hysteresis).

### Verified
- Build Debug + Release: **0 warnings, 0 errors**.
- Tests: **129/129 passing** (115 Network + 14 App).
- Real Windows GUI (Release, live traffic): graph section renders; ranges 30 s / 1 m / 5 m
  switch without clearing history; rates tracked live traffic (→ 10.68 Mbps download);
  en ↔ fa-IR switch re-localized all graph strings without crashing; resize stays
  responsive; clean close logged "TrafficLens exiting". No exceptions in the log.

## [0.0.6] — 2026-09-14 (TL-005 Dashboard)

### Added
- `TrafficLens.Core/Conversion/DataRateFormatter` — presentation formatting for
  live rates: adaptive B/s / KB/s / MB/s and Mbps; culture-aware decimal
  separator; unit symbols kept as technical notation (untranslated).
- `DashboardViewModel` + `AdapterListItemViewModel` (App) — MVVM dashboard over
  the existing collector/provider abstractions, bound to `MainViewModel.Dashboard`.
- `MainWindow` dashboard layout: Download / Upload / Total cards, Active Adapter
  card, Network Adapters list; scrollable, stays intact at smaller window sizes.
- English (en) and Persian (fa-IR) resources for dashboard labels, adapter kinds
  and connection states.
- `tests/TrafficLens.App.Tests` (xUnit, net8.0-windows) for ViewModel mapping,
  no-network/reconnect/VPN-only states, culture switch, and resource existence.
- Formatter tests (15 cases) in `tests/TrafficLens.Network.Tests`.
- ADR-011 (thin event-driven dashboard; Core formatter; aggregate policy reuse).

### Changed
- `App.xaml.cs` — registers `AddNetworkServices()`, `DashboardViewModel`; starts
  the collector on startup; logs "TrafficLens exiting" on clean shutdown; DI now
  also disposes collector (stops the poll loop).
- `MainViewModel` — reduced to window chrome (title, status, language switch);
  dashboard state moved into the injected `Dashboard` property.
- VM updates are marshalled to the WPF Dispatcher (`InvokeAsync`); all collector/
  provider/localization event subscriptions are released via `IDisposable`.
- Adapter-list automation names via `ToString()` so screen readers see adapter
  names, not ViewModel type names.

### Verified
- Build Debug + Release: **0 warnings, 0 errors**.
- Tests: **103/103 passing** (95 Network + 8 App).
- Real Windows GUI (Release, live traffic): Download card moved
  0 B/s → 844.31 KB/s (6.92 Mbps) → decaying to 32.74 KB/s as traffic flowed;
  adapter list showed Wi-Fi + OpenVPN TAP/DCO + Wi-Fi Direct virtuals + Bluetooth;
  en and fa-IR dashboards both rendered without crashing; clean close logged
  "TrafficLens exiting".
- ADR-009/010 policy holds: peak ~6.92 Mbps on the system cards matched the
  non-tunnel aggregate while tunnel rows keep their own rates; no double counting.

## [0.0.5] — 2026-09-14 (TL-004 audit + gap fix)

### Changed
- `NetworkAdapterKindMapper.Map(type, description)`: description-aware classification
  so OpenVPN TAP/DCO (`HighPerformanceSerialBus`/53) are now `Tunnel`, and
  virtual nics (Hyper-V/VMware/Wi-Fi Direct) are correctly `Virtual`.
- `AdapterFilter.IsMonitored` relaxed: Unknown-type adapters with recognized
  tunnel/virtual driver descriptions remain visible (prevents WireGuard hidden
  on machines where it reports Unknown type).
- `DefaultAdapterSelector` uses the description-aware overload so TAP stays
  non-default; no change to live default (Wi-Fi) on this machine.

### Added
- Tests: description-aware kind mapping (VPN drivers, virtual nics),
  Unknown+description filter guard, TAP default-selection regression test.

### Verified
- Build (Debug + Release): 0 warnings, 0 errors.
- Tests: 80/80 passed.
- Live enumeration confirmed: OpenVPN TAP/DCO → Tunnel; Wi-Fi Direct → Virtual;
  Wi-Fi → Wireless (default); Bluetooth PAN → Ethernet; all adapters visible.

### Notes
- No new classes; all changes are small refinements in existing TL-002 files.
- No rewrite performed; only the identified gap (TAP/DCO misclassification) was
  fixed per the TL-004 audit instruction.

## [0.0.4] — 2026-09-14 (M1, TL-003)

### Added
- Rate calculation (`NetworkSpeedCalculator`): cumulative-counter deltas divided by
  actual monotonic elapsed time (QPC via `Stopwatch`), no 1 s assumption.
- `SpeedRateTracker`: per-adapter independent baselines with re-baseline rules
  (first sample, counter reset/wrap/decrease, zero/invalid elapsed, adapter
  disappearance/replacement) — never emits a fake spike.
- `SpeedSampleReady` is now raised with real per-adapter rates; `GetCurrentSamples()`
  returns current rate samples (`NetworkSpeedSample`).
- `NetworkTrafficAggregator.AggregateRates` — system "Internet Total" using the
  tunnel-excluding non-overlapping policy; per-adapter views keep tunnel/VPN traffic.
- `DataRateConverter` (Core): B/s, KB/s, MB/s, Kbps, Mbps, Gbps numeric conversions
  for the UI; raw values remain bytes/second (ADR-010).
- Tests: `NetworkSpeedCalculatorTests`, `SpeedRateTrackerTests`, `DataRateConverterTests`,
  aggregate-rate and collector rate tests.

### Changed
- `WindowsNetworkTrafficCollector.RunLoopAsync` computes and raises rate samples each
  poll (bounded one-baseline-per-adapter state; background thread only).

### Verified
- Build (Debug + Release): 0 warnings, 0 errors.
- Tests: 65/65 passed.
- Real Windows: window-mean Wi-Fi rate 803,647 B/s down / 18,423 B/s up vs native
  `Get-NetAdapterStatistics` 1,120,590 / 26,086 over a longer overlapping window —
  plausibly aligned (see `docs/NETWORK_COLLECTION.md`).

### Notes
- String formatting of rates is deferred to UI (TL-005).
- Dashboard/graph/per-process/connections not touched (scope restriction).

## [0.0.3] — 2026-09-14 (M1, TL-002)

### Added
- `TrafficLens.Network`: global network collector
  - `WindowsNetworkTrafficCollector` (implements `INetworkTrafficCollector`):
    poll loop, per-adapter cumulative counters, `CounterSampleReady`, `NetworkChanged`,
    `NetworkChange` address/availability hooks, counter reset/wrap re-baseline.
  - `WindowsNetworkAdapterProvider` (implements `INetworkAdapterProvider`).
  - `NetworkInterfaceSource`, `RawAdapterSnapshot`, `NetworkAdapterKindMapper`,
    `AdapterFilter`, `DefaultAdapterSelector` (very thorough adapter detection).
  - `NetworkTrafficAggregator` (totals + tunnel-exclusion policy, ADR-009).
  - `NetworkServiceCollectionExtensions.AddNetworkServices()` DI registration.
- Core: `NetworkCounterSample` model; `CounterSampleReady` +
  `GetCurrentCounterSamples()` on `INetworkTrafficCollector` (ADR-007).
- Tests: `TrafficLens.Network.Tests` — 36 xUnit tests, all passing.
- Verification console: `TrafficLens.Network.Verification` (real collector + JSON dump).
- Docs: `docs/NETWORK_COLLECTION.md` decision + verification evidence; ADR-007/008/009
  in `docs/DECISIONS.md`.

### Changed
- `TrafficLens.Network.csproj`: added `Microsoft.Extensions.DependencyInjection.Abstractions`.
- Build now includes tests + verification projects.

### Verified
- Build (Debug + Release): 0 warnings, 0 errors.
- Tests: 36/36 passed.
- Real Windows cross-check vs `Get-NetAdapterStatistics` — counters match
  (Wi-Fi 487.0 MB / 80.8 MB at capture), see `docs/NETWORK_COLLECTION.md`.

### Notes
- Rates (`NetworkSpeedSample`, `SpeedSampleReady`) intentionally left unimplemented;
  produced by TL-003.

## [0.0.2] — 2026-09-14 (M0 verification)

### Added
- Startup logging in `App.xaml.cs` (startup, culture, MainWindow shown) to confirm
  boot sequence in structured logs.

### Fixed
- Nothing required at runtime; all TL-001 startup/localization checks passed.

### Verified
- GUI smoke test passed on Windows: app starts cleanly, dark theme loads,
  English and Persian (fa-IR) resources resolve, RTL culture applies without crash,
  structured logs written, clean shutdown.

## [0.0.1] — 2026-09-14 (M0)

### Added
- Solution `TrafficLens.sln` with four projects:
  - `TrafficLens.App` (WPF UI, `net8.0-windows`)
  - `TrafficLens.Core` (models, interfaces)
  - `TrafficLens.Network` (empty placeholder project)
  - `TrafficLens.Infrastructure` (settings, logging)
- Project references configured (App -> Core/Network/Infrastructure; Network/Infrastructure -> Core).
- MVVM base: `ViewModelBase`, `RelayCommand`, `MainViewModel`.
- DI composition root in `App.xaml.cs` (Microsoft.Extensions.DependencyInjection).
- Structured JSON file logging (`Infrastructure/Logging/FileLogger`), logs under
  `%LOCALAPPDATA%\TrafficLens\logs`.
- JSON settings store (`JsonSettingsService`), persists language.
- Localization foundation:
  - Core `ILocalizationService`
  - App `LocalizationService` with resource-based lookup and English fallback
  - `Resources/Strings.resx` (English, neutral)
  - `Resources/Strings.fa-IR.resx` (Persian)
  - RTL support via `FlowDirection` tied to culture
- Minimal dark theme (`Themes/DarkTheme.xaml`) and dark main window with dashboard placeholders.
- Core domain contracts for later milestones:
  - `INetworkTrafficCollector`, `IProcessTrafficCollector`, `IConnectionProvider`,
    `INetworkAdapterProvider`, `ISettingsService`, `ILocalizationService`
  - Models: `NetworkSpeedSample`, `NetworkAdapterInfo`, `ProcessTrafficSample`,
    `ConnectionInfo`
- Repository documentation (`AGENTS.md`, `README.md`, `TASKS.md`, docs/...).

### Notes
- .NET 8 SDK 8.0.425 installed locally at `C:\dotnet` (system had no SDK).
- No network monitoring implemented in this milestone (by design).