# TrafficLens — Architecture

## Overview

TrafficLens is a modular .NET 8 WPF desktop application. The layout separates UI,
domain contracts, collection logic, and infrastructure so each can evolve independently.

```
┌────────────────────────────────────────────────────────────────┐
│                       TrafficLens.App (WPF)                     │
│  Views · ViewModels · Commands · Tray · Widget · Resources       │
│  Composition root: App.xaml.cs (builds IServiceProvider)        │
└───────────┬──────────────────────────────┬───────────────────┬───┘
            │                              │                   │
            ▼                              ▼                   ▼
┌────────────────────┐  ┌────────────────────────┐  ┌───────────────────────┐
│  TrafficLens.Core  │  │   TrafficLens.Network  │  │ TrafficLens.Infrastructure │
│  models, interfaces│  │  collectors (TL-002+)  │  │  SQLite, settings, log    │
└────────────────────┘  └────────────────────────┘  └───────────────────────┘
```

## Projects

### TrafficLens.App
- WPF UI and composition root.
- Views bind to ViewModels via `DataContext`; no business logic in code-behind.
- Localization resources live here (`Resources/Strings*.resx`).
- Everything public is resolved through DI.

### TrafficLens.Core
- Contains domain records and interface contracts (`INetworkTrafficCollector`,
  `IProcessTrafficCollector`, `IConnectionProvider`, `INetworkAdapterProvider`,
  `ILocalizationService`, `ISettingsService`).
- No WPF, no OS-specific networking code.
- Consumers depend on abstractions so implementations are replaceable.

### TrafficLens.Network
- Owns Windows collection implementations (upcoming milestones).
- Consumes Core contracts only.

### TrafficLens.Infrastructure
- SQLite persistence (TL-009 history), JSON settings, structured file logging.
- Depends only on Core.

## Dependency Injection

- `Microsoft.Extensions.DependencyInjection`.
- Registration happens in `App.OnStartup` via `ConfigureServices`.
- `MainWindow`, `MainViewModel`, `ILocalizationService`, `ISettingsService`,
  and `ILogger<T>` are resolved from the container.
- App data lives under `%LOCALAPPDATA%\TrafficLens` (`AppPaths`).

## MVVM

- Every ViewModel derives from `ViewModelBase` (`INotifyPropertyChanged`).
- Commands use `RelayCommand`.
- Data flows: collector -> service -> ViewModel -> View binding.
- `MainViewModel` is window chrome (title, status, language switch) plus
  top-level navigation; the content sections are injected ViewModels —
  `DashboardViewModel` (`MainViewModel.Dashboard`),
  `ApplicationsViewModel` (`MainViewModel.Applications`), and
  `ConnectionsViewModel` (`MainViewModel.Connections`), switched by
  `ShowDashboardCommand` / `ShowApplicationsCommand` / `ShowConnectionsCommand`
  and hosted in a `MainWindow` `ContentControl`.
- `DashboardViewModel` consumes `INetworkTrafficCollector` and
  `INetworkAdapterProvider` through DI, reacts to `SpeedSampleReady` and the
  adapter-changed events, and marshals every update to the WPF Dispatcher with
  `InvokeAsync`. It unsubscribes in `Dispose()` (no leaked handlers).
- Rates are formatted with `DataRateFormatter` (Core). No data is polled by the
  ViewModel and no network APIs are touched in the view layer, keeping ADR-011's
  thin-dashboard contract testable without the UI.

## Live graph (TL-006)

- `TrafficLens.Core/Graph` holds the no-WPF graph layer (pure, unit-testable):
  - `TrafficSampleBuffer` — bounded, thread-safe ring buffer (5.5 min retention,
    1320 samples) fed by the ViewModel with the existing `AggregateRates` aggregate.
    Dedupes same-poll duplicate timestamps so one poll appends one sample.
    `Slice(window, now)` is non-mutating, so switching 30 s / 1 m / 5 m never clears
    history. Real timestamps are preserved (dropouts stay visible as gaps).
  - `AdaptiveGraphScale` — shared Y max: immediate growth on spikes, hysteretic shrink,
    2 KB/s floor (ADR-012).
- `TrafficGraphControl` (App, `Controls/`) is a lightweight `FrameworkElement` that
  draws both series into a `DrawingContext` (`OnRender`, `StreamGeometry`), reads the
  theme brushes, formats axis labels via `DataRateFormatter`, and forces LTR so the
  timeline stays oldest-left → newest-right under fa-IR.
- Range selection is a `SelectGraphRangeCommand` on the dashboard (CommandParameter
  30/60/300); selection state re-slices the buffer and re-renders in place.

## History layer (TL-009)

- `TrafficLens.Core/History` holds the no-OS domain:
  - `HistoryRangeCalculator` — half-open local-date ranges
    (`ToLocalDateRange`, `LocalDateOf`) over `TimeZoneInfo`, and
    `BuildDailySeries`/`SumDaily` helpers; DST + local-midnight correct.
  - `TrafficHistoryAccumulator` — turns the collector's `NetworkCounterSample`
    DELTAS into per-UTC-minute buckets: first observation per adapter is a
    baseline, only non-negative deltas count, resets/reconnects/reboots
    re-baseline (never fabricate usage). `DrainCompleted` emits full minutes;
    `DrainAll` also emits the open minute (clamped 1..60 s) for shutdown flush.
  - `TrafficUsage`, `DailyUsagePoint`, `HistorySnapshot` (immutable, cached) and
    the contracts `ITrafficHistoryRepository` / `ITrafficHistoryService`.
- `TrafficLens.Infrastructure/History` owns persistence and the pipeline:
  - `SqliteTrafficHistoryRepository` (Microsoft.Data.Sqlite) — schema v1 tables
    `traffic_samples` + `daily_usage`, WAL, `busy_timeout`, `Pooling=false`;
    single-transaction appends using `INSERT OR IGNORE` + `changes()==1` so
    restarts/crashes cannot duplicate totals; `daily_usage` kept forever; raw
    samples pruned after 90 days on startup.
  - `TrafficHistoryService` — subscribes the existing
    `INetworkTrafficCollector.CounterSampleReady` (it **never starts its own
    NIC polling loop**), excludes tunnels by default (ADR-009/010 policy; on a
    VPN-only host history honestly records ~0), flushes completed minutes on the
    background loop every 30 s, drains + flushes on `StopAsync`, and publishes a
    cached immutable `HistorySnapshot` (`HistoryChanged`). SQL runs only on
    service threads; the UI never queries the DB.
  - `HistoryServiceCollectionExtensions.AddHistoryServices(dbPath)` — DI.
- Data flow: collector counters → accumulator (live) → repository (SQLite) →
  `HistorySnapshot` (cached) → `HistoryViewModel` (dispatcher) →
  `HistoryView` + `HistoryBarChartControl` (native `FrameworkElement`,
  oldest-left → newest-right even under RTL).
- App: `HistoryViewModel` (five ranges, summary cards, no-data/unavailable
  states) and the History host wired in `MainViewModel`/`MainWindow`;
  `App.xaml.cs` starts the service after the network collector.

## Logging

- `AddFileLogging(logDirectory)` registers a `FileLoggerProvider`.
- Writes structured JSON lines to `%LOCALAPPDATA%\TrafficLens\logs\trafficlens-{date}.log`.
- Logging failures are swallowed and never crash the app.

## Localization

- Core defines `ILocalizationService` (culture, `GetString`, fallback, RTL flag).
- App implements it over embedded resx resources.
  - Neutral: `Strings.resx` (English), satellite: `Strings.fa-IR.resx`.
- Views bind localized labels through the ViewModel; window `FlowDirection` follows
  culture so RTL is supported later without redesign.
- Technical values (IPs, ports, paths, protocol names) stay untranslated and LTR.

## Async & UI Threading

- Collectors run in background (later milestones). UI updates must be marshalled to
  the dispatcher. Never block the UI thread (graph requirement).

## Per-process traffic layer (TL-007)

- `TrafficLens.Core` defines the process contracts: `IProcessTrafficCollector`
  (Start/Stop/Samples/Status/LastError), `ProcessTrafficSample` (immutable snapshot
  with pid, start-time identity, process name, path, icon availability, byte
  totals, monotonic-window rates), and `ProcessTrafficCollectorStatus` (Stopped /
  Starting / Running / PermissionDenied / Failed).
- `TrafficLens.Network/Process/` owns the implementation:
  - `WindowsEtwProcessTrafficCollector` — opens a real-time ETW kernel session
    (`TraceEventSession`), subscribes the eight TCP/UDP IPv4/IPv6 handlers, maps
    decoded PID+size events into `NetworkTransferEvent` on the ETW thread, and
    publishes bounded snapshots on a ~1 s loop; reports `PermissionDenied` when not
    elevated and never crashes.
  - `ProcessTrafficAccountingEngine` — dictionary keyed by `ProcessInstanceId`,
    monotonically-windowed rates, metadata-resolve/rekey, idle prune, cap/eviction;
    hot path (Record) is allocation-free.
  - `WindowsProcessMetadataProvider` — guarded `System.Diagnostics.Process` reads;
    detects PID reuse by mismatched start time; never throws.
  - Supporting models: `NetworkTransferEvent`, `ProcessInstanceId`,
    `ProcessMetadata` / `ProcessMetadataResult`, `ProcessProtocolTotals`.
- DI: registered as a singleton `IProcessTrafficCollector` in
  `NetworkServiceCollectionExtensions`. The provider/session is owned entirely by
  the Network layer; the App layer (once the Applications page exists) will consume
  only the Core abstraction.

## Active connections layer (TL-008)

- `TrafficLens.Core` defines the connection contracts and pure presentation logic:
  `IConnectionProvider` (current snapshot + `ConnectionsChanged` + `LastError` +
  lifecycle), `ConnectionInfo` (protocol, state, address family, nullable remote
  endpoint, process identity), `ConnectionKey`, `EndpointFormatter` (always-LTR,
  culture-safe; suppresses unspecified peers), and `Selection/ConnectionSelection`
  (`ConnectionFilter` / `ConnectionFiltering` / `ConnectionSort`) — all non-mutating
  and unit-testable without the OS.
- `TrafficLens.Network/Connections/` owns the Windows implementation:
  - `NativeConnectionTableReader` — IP Helper `GetExtendedTcpTable` /
    `GetExtendedUdpTable` (owner-PID tables, IPv4 + IPv6), buffer-grow retry.
  - `ConnectionTableParser` — pure parsing of the native row layouts.
  - `ConnectionProcessResolver` — bounded `IProcessMetadataProvider` cache keyed by
    `ProcessInstanceId`.
  - `WindowsConnectionProvider` — ~1 s off-UI poll loop publishing snapshots;
    partial failures degrade to warnings, total failures keep the last good snapshot
    and surface `LastError`.
- App: `ConnectionsViewModel` + `ConnectionRowViewModel` consume only
  `IConnectionProvider`, marshal updates to the Dispatcher, and update rows in place;
  `ConnectionsView` is hosted via `MainViewModel.Connections` and
  `MainWindow`'s `ContentControl` (Dashboard / Applications / Connections).
- Non-elevated by design; TCP/UDP only (no ICMP); endpoints/privacy and state
  semantics are recorded in `docs/NETWORK_COLLECTION.md` and ADR-016.

## Floating widget layer (TL-010)

- `FloatingWidgetViewModel` (App) — a thin event-driven ViewModel over the
  existing live pipeline. It subscribes `SpeedSampleReady`/
  `NetworkChanged`/`AdaptersChanged` (same events as the dashboard) and **never
  starts its own poll loop, timer, SQLite query, or process metadata call**.
  Rates come from the existing ADR-009/010 aggregate
  (`NetworkTrafficAggregator.AggregateRates`), formatted through
  `DataRateFormatter` (technical units stay LTR under RTL). Exposes localized
  title/labels, Download/Upload/Total strings, `TogglePinCommand` (raises
  `PinStateChanged`) and `CloseWidgetCommand` (raises `CloseRequested`).
- `FloatingWidgetWindow` (App) — 280×110 frameless window
  (`WindowStyle=None`, `ResizeMode=NoResize`, `ShowInTaskbar=False`), dark theme
  from the shared `DarkTheme.xaml` brushes, drag by empty area (`DragMove`,
  ignoring clicks on buttons), pin toggle + hide buttons bound to commands, rate
  values forced `FlowDirection=LeftToRight`.
- `FloatingWidgetService` (App, singleton) — owns the single widget instance:
  `Show`/`Hide`/`Toggle`/`RestoreIfEnabled`. Re-show only activates the existing
  window (never duplicates). Closing the widget window is cancelled → hide only;
  `Dispose` detaches the cancel handler and really closes the window so the app
  shuts down normally (`OnLastWindowClose`) without `Environment.Exit` or a
  hidden window keeping the process alive. `MainWindow.Closing` calls
  `Hide()` (persists position) before normal shutdown proceeds (ADR-015).
- `WidgetPositionHelper.Clamp` (App) — pure, unit-testable multi-monitor position
  recovery: clamps the window rectangle into the union of the monitor work areas,
  preserving legitimate negative virtual-screen coordinates (secondary monitor
  left of primary) and recovering from off-screen / monitor-disconnected
  positions. Real work areas come from `SystemParameters.VirtualScreen*`.
- Settings — persisted through the existing `ISettingsService`
  (single `settings.json`, no second settings file):
  `FloatingWidgetEnabled` (startup restore), `FloatingWidgetAlwaysOnTop`
  (default on), `FloatingWidgetLeft`, `FloatingWidgetTop`.
- Lifecycle: registered as a singleton in `App.xaml.cs`; `RestoreIfEnabled()`
  runs after the collectors start; main-window close hides it; DI disposal closes
  it for real.
- Intentional scope boundary: the widget has **no tray behavior** (technique,
  minimize-to-tray, and startup-with-Windows belong to TL-011).

## System tray layer (TL-011)

- `ISystemTrayService` + `SystemTrayService` (App) — a single WinForms
  `NotifyIcon` (`System.Windows.Forms` via a plain
  `<FrameworkReference Include="Microsoft.WindowsDesktop.App.WindowsForms" />`;
  `UseWPF` stays on, `UseWindowsForms` is off, so no WinForms global usings pollute
  the WPF codebase). One icon created once at startup, disposed only on real exit;
  tooltip `TrafficLens`; no ghost. Exposes `OpenRequested` (double-click /
  menu Open) and `ExitRequested` (menu Exit) events, `Show()` (startup), and
  `ShowFirstCloseToTrayNotice()`.
- Tray menu (in-place relabeled on culture change, never recreated): Open
  TrafficLens / Show-Hide Floating Widget / Always on Top (checkable) /
  separator / Exit. Always on Top flips the same widget pin state saved by the
  widget's own pin button (single source of truth). The widget pin's
  tooltip/accessibility text binds `AlwaysOnTopLabel` (TL-010 polish).
- Runtime-drawn icon — 32×32 dark rounded square with accent chevrons
  (`#4FC3F7` / `#26A69A` on `#1E1E2E`), readable at 16–32 px; `GetHicon()` +
  `Icon.FromHandle`, `DestroyIcon` on dispose.
- `TrayBehavior` (App, pure) — settings keys `MinimizeToTray` / `CloseToTray`
  (defaults true) + `TrayCloseNoticeShown`; `ResolveCloseAction(isExitRequested,
  settings)` → `Exit | HideToTray`; once-only notice decision.
- `ApplicationExitCoordinator` (App) — single idempotent
  `RequestApplicationExit()` pipeline: latch `IsExitRequested`, dispose tray +
  widget, `Application.Current?.Shutdown()` (Dispatcher-safe). Collectors /
  connection provider / history flush / file logger / DI are disposed by
  container disposal. No `Environment.Exit` / `Process.Kill`.
- `App.xaml`: `ShutdownMode="OnExplicitShutdown"` — tray-hide is a real
  background run (no hidden main window keeps it alive incorrectly; a hidden
  window alone does not keep the process up).
- `MainWindow` (App): `Closing` routes through `TrayBehavior.ResolveCloseAction`
  — Exit path calls the coordinator (real close), HideToTray path cancels +
  `Hide()` + first-close balloon; `StateChanged` minimize→`Normal`+`Hide()` when
  `MinimizeToTray`; `OpenRequested` re-shows and activates the **same** window
  (singleton, never a second instance).
- `MainViewModel` (App) — `MinimizeToTray`/`CloseToTray` settings-backed
  properties with localized labels; header `…` options popup with the two
  checkboxes (minimal surface; TL-013 owns the full settings page).
- Localization — `OpenTrafficLensLabel`, `ExitLabel`, `MinimizeToTrayLabel`,
  `CloseToTrayLabel`, `TrayCloseNoticeBalloon` in en + fa-IR; tray technical name
  stays LTR.
- Lifecycle: registered in `App.xaml.cs`; startup subscribes
  `trayService.ExitRequested → coordinator.RequestApplicationExit()` then
  `trayService.Show()`; widget restore runs via `IFloatingWidgetService`.

## Future Plans

- Full settings page (TL-013) — currently just the minimal tray options popup.
- Hourly view of the current day (raw minute samples are already retained 90 days).
- See `docs/ROADMAP.md`.