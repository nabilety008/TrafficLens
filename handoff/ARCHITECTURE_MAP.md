# ARCHITECTURE MAP — TrafficLens

A map of the shipped system. Read this to understand how data flows before
changing anything. Deeper narrative detail is in `docs/ARCHITECTURE.md`,
`docs/NETWORK_COLLECTION.md` and `docs/DATABASE.md`.

## Projects and dependency direction

```
TrafficLens.Core            no internal dependencies
        ^
        |            TrafficLens.Network  -> Core
        |
        |            TrafficLens.Infrastructure -> Core
        |
        +---- TrafficLens.WinUI  -> Core, Network, Infrastructure, WinUI.Tray
                                     (this is the release entry point)
                     TrafficLens.WinUI.Tray -> Core

TrafficLens.App            legacy WPF shell: ROLLBACK / REFERENCE ONLY, never published
                           (its alert services and Strings*.resx are *linked* into WinUI)
```

Reference rules that still hold:

- `Core` references nothing internal.
- `Network` and `Infrastructure` each reference only `Core`.
- `App` and `WinUI` reference the layers they need.
- No circular references.

`TrafficLens.App` (WPF) is retained deliberately as a rollback reference. The
release pipeline **fails** if a WPF `TrafficLens.exe` ever appears in publish
output. Do not "fix" the WinUI app by editing WPF views.

## Composition root

`src/TrafficLens.WinUI\App.xaml.cs` builds the DI container
(`ConfigureServices`), reads settings, sets the culture, creates `MainWindow`,
and starts each long-running service exactly once. It is the only place that
knows the full service list. If a new service needs to start, register and start
it here — not from a page.

Registered singletons include `ISettingsService`, `ILocalizationService`,
`IFloatingWidgetService`, `ISystemTrayService`, `IStartupRegistrationService`,
`IWindowsUpdateService`, `DnsResolverService`, `IAlertService`, the collectors,
and the connection provider.

## The shell

- `MainWindow.xaml` / `.cs` — custom title bar, NavigationView with 7 destinations,
  and the Floating Widget quick action in the physical top-left corner.
- `Pages/` — `DashboardPage`, `ApplicationsPage`, `ConnectionsPage`, `HistoryPage`,
  `AlertsPage`, `SettingsPage`, `AboutPage`, plus `PlaceholderView`.
- `ViewModels/` — one ViewModel per page; pages bind to them and hold no logic.
- `Infrastructure/` — `SingleInstanceGuard`, `WindowIcon`, `ProcessIconCache`,
  converters.

### Custom title bar

Two collaborating helpers, both with dedicated tests:

- `Infrastructure\TitleBarCaptionLayout.cs` — resolves the physical safe area for
  the title. Maps the shell's insets by **flow order** and applies the reserve on
  the **physical** caption side, so RTL works without per-language handling.
- `Infrastructure\NativeCaptionButtons.cs` — bounded `WM_NCHITTEST` probe that
  reads the real caption-button region back off the live window when the shell
  reports nothing usable (zero, or resize-frame-only).

## Monitoring pipeline

Two independent paths feed different screens. **Do not add a third.**

### A. Global / aggregate network throughput

```
WindowsNetworkAdapterProvider  ── adapter list, filtered
  AdapterFilter / AdapterSnapshotCache / DefaultAdapterSelector
        │
WindowsNetworkTrafficCollector ── polls per-adapter byte counters (~1 s)
        │  → NetworkCounterSample
NetworkSpeedCalculator + SpeedRateTracker ── counter deltas → bytes/sec
        │  → NetworkSpeedSample
NetworkTrafficAggregator ── sums the selected adapter(s)
        │
        ├── DashboardViewModel  → DashboardPage
        ├── FloatingWidgetViewModel → FloatingWidgetWindow
        └── TrafficHistoryAccumulator (see History)
```

Rates are always derived from **real byte deltas between samples**, never from a
cumulative counter, so a counter reset cannot produce a spike.

### B. Per-process / per-application traffic (ETW)

```
WindowsEtwProcessTrafficCollector ── ETW kernel network session
  NetworkTransferEvent                     (start/stop/completion events)
        │
ProcessTrafficAccountingEngine ── attributes bytes to PID + process instance
  ProcessInstanceId (handles PID reuse)    → ProcessProtocolTotals
        │
WindowsProcessMetadataProvider ── name/icon/metadata
        │  → ProcessTrafficSample (ProcessTrafficCollectorStatus when degraded)
ApplicationsViewModel → ApplicationsPage
```

**ETW requires an Administrator process.** Without elevation the collector logs
`permission denied` and reports a degraded status instead of failing. This is
expected and non-fatal — do not "fix" it by requesting elevation in the app.

### C. Connections

```
WindowsConnectionProvider ── IP Helper tables (~1 s poll)
  NativeConnectionTableReader → ConnectionTableParser → ConnectionInfo
  ConnectionProcessResolver ── maps a connection to a process
  DnsResolverService ── reverse DNS for display names
        │  → ConnectionsViewModel → ConnectionsPage
```

## History and SQLite

```
TrafficHistoryAccumulator ── aggregates real byte deltas into buckets
  TrafficHistoryBucket / TrafficUsage / HourlyUsagePoint
  HourlyHistoryBuilder  (current-day hourly series)
        │
TrafficHistoryService (ITrafficHistoryService)
        │
SqliteTrafficHistoryRepository (ITrafficHistoryRepository)  ── SQLite, schema v2
        │
HistoryViewModel → HistoryPage
  Controls\HistoryBarChart.xaml
```

Retention/pruning and bucket sizing live in `src\TrafficLens.Core\History\` and are unit-tested.
Schema details: `docs/DATABASE.md`. The repository is opened once and the schema
is created/migrated at startup (`Traffic history database ready (schema v2)`).

## Alerts

```
AlertEngine (Core)  ── evaluates AlertConfig thresholds against the current rate
  AlertSignal → AlertEvent → AlertHistoryBuffer (cooldown)
        │
AlertService (IAlertService)  ── notification surface
  AlertSettings  (persisted config)
        │
AlertsViewModel → AlertsPage
```

`AlertService`, `AlertSettings`, `AlertNotification` and `IAlertService` live in
the WPF project and are **linked into WinUI** by the `.csproj`. They are shared
on purpose — edit them in place, do not fork a WinUI copy.

## Settings and persistence

- `ISettingsService` (`src\TrafficLens.Core\Abstractions\`) implemented by
  `src\TrafficLens.Infrastructure\Services\JsonSettingsService.cs` — JSON at
  `%LOCALAPPDATA%\TrafficLens\settings.json`.
- `AppPaths` centralises every user-data path.
- Settings own: language, theme, close/minimize-to-tray, start-with-Windows,
  start-minimized, onboarding state, and the Floating Widget state
  (`FloatingWidgetEnabled`, `FloatingWidgetAlwaysOnTop`, `FloatingWidgetLeft`,
  `FloatingWidgetTop`).

## Floating Widget

- `IFloatingWidgetService` / `FloatingWidgetService` — owns creation, lifetime,
  drag and clamping to the monitor work area.
- `WidgetEnabledState` — the **single** source of truth for enabled state. The
  shell quick action and the Settings switch are views of this one value; writes
  go only through `IFloatingWidgetService.SetEnabled`.
- `WidgetToggleSync.Resolve` — shared resolution helper (the old `Apply` that fed
  a *pair* of switches was removed).
- `Views\FloatingWidgetWindow.xaml` — the widget window (510x210 in the final
  build), with its own title bar and a close button that routes through the
  service so the state actually persists.

## Tray and exit

- `ISystemTrayService` / `SystemTrayService` + `TrayBehavior` — tray icon,
  context menu, balloon notice, activate-on-click.
- `TrafficLens.WinUI.Tray\TrayIconHost.cs` — a **separate small project** that
  hosts the notify icon, so the tray can outlive the main window.
- `ApplicationExitCoordinator` — single owner of process exit. Closing to the
  tray is **not** exiting; the tray has an explicit Exit item. Do not add a
  second exit path.

## Localization

- `src/TrafficLens.WinUI\LocalizationService.cs` implements `ILocalizationService`
  over a `ResourceManager` with base name `TrafficLens.WinUI.Resources.Strings`.
- Resources: `src/TrafficLens.App\Resources\Strings.resx` (English, neutral) and
  `Strings.fa-IR.resx` (Persian), both **linked** into the WinUI project.
- `IsRightToLeft` is derived from a culture list (`fa`, `fa-IR`, `ar`, `ar-SA`,
  `he`, `he-IL`, `ur`, `ur-PK`); `CultureChanged` drives re-layout.
- User-facing strings come from the `.resx` via `ILocalizationService`. Never
  hard-code UI strings in a page or ViewModel.
- **Technical values stay LTR inside the RTL layout** (rates, byte counts, IPs).

## Packaging and release

- `Directory.Build.props` — the single source of version metadata.
- `scripts/build-release.ps1` — the release pipeline. Resolves a `dotnet` that
  actually has an SDK, then restore → build (x64) → test → publish → **validate
  PRI** → ZIP → Inno installer → SHA-256 sidecars. It fails the run before
  packaging if the PRI is missing/empty/too small/not a PRI container.
- `packaging\TrafficLens.iss` — Inno Setup script (AppId, shortcuts, uninstall,
  upgrade-time deletion of the stale WPF `TrafficLens.exe`).
- `packaging\Persian.isl` — Persian installer language file.
- `assets\branding\` — the one replaceable brand asset set, generated by
  `scripts\generate-icons.ps1`.

Full release detail: `docs/PACKAGING.md` and `handoff/BUILD_AND_TEST.md`.

## Logging

`FileLoggerProvider` / `FileLogger` write structured JSON lines to
`%LOCALAPPDATA%\TrafficLens\logs\`. Log via `ILogger<T>`. Every record carries an
`"exception":null` field, so a naive grep for "exception" matches every line —
filter on `"level":"Error"` instead.
