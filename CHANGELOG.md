# TrafficLens — CHANGELOG

All notable changes are documented here in reverse chronological order.

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