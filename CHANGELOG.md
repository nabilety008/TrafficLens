# TrafficLens — CHANGELOG

All notable changes are documented here in reverse chronological order.

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