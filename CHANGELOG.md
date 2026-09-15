# TrafficLens — CHANGELOG

All notable changes are documented here in reverse chronological order.

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