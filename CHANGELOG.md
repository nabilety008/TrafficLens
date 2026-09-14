# TrafficLens — CHANGELOG

All notable changes are documented here in reverse chronological order.

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