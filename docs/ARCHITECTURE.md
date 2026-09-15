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
- SQLite persistence (upcoming), JSON settings, structured file logging.
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

## Future Plans

- History: aggregated SQLite samples; retention policies.
- Tray + floating widget.
- See `docs/ROADMAP.md`.