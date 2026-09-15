# TrafficLens — TASKS.md

## How to use

- Optional tasks are marked `[optional]`.
- A task is DONE only when implemented, verified (built/run/tested), and documented.
- Task IDs are stable. Do not renumber existing IDs.

## Backlog

### TL-011 System Tray — **DONE**
- [x] Single WinForms `NotifyIcon` via `<FrameworkReference Include=
      "Microsoft.WindowsDesktop.App.WindowsForms" />` (no `UseWindowsForms`,
      no global-using ambiguity)
- [x] One icon created once; disposed only on real exit; tooltip `TrafficLens`;
      runtime-drawn 32×32 icon (dark rounded square + accent chevrons, readable
      16–32 px; `GetHicon`/`FromHandle`/`DestroyIcon`)
- [x] Tray menu: Open TrafficLens / Show-Hide Floating Widget / Always on Top
      (checkable) / separator / Exit; culture-change relabel in place (no icon
      recreation); Always on Top reuses the widget pin state
- [x] Double-click / Open restores the **same** singleton MainWindow (never a
      second instance)
- [x] Minimize-to-tray (`StateChanged` → Normal+Hide) and Close-to-tray
      (`Closing` → HideToTray) with settings defaults true;
      CloseToTray=false ⇒ X real graceful exit via the coordinator
- [x] Single idempotent `ApplicationExitCoordinator.RequestApplicationExit()`
      (latch → dispose tray+widget → `Shutdown()`); no `Environment.Exit`;
      `ShutdownMode="OnExplicitShutdown"`; collectors/history/DI disposed by
      the container
- [x] First close-to-tray balloon once-ever, persisted `TrayCloseNoticeShown`
- [x] Collectors keep running and history keeps accumulating while hidden
- [x] Floating widget pin tooltip bound to `AlwaysOnTopLabel` (TL-010 polish)
- [x] Dispatcher-safe marshaling; thread-safe tray usage
- [x] Localization en + fa-IR for all tray strings (technical name stays LTR)
- [x] 317 tests passing (10 `TrayBehaviorTests`, 4
      `ApplicationExitCoordinatorTests`, localization keys, widget always-on-top
      label); Debug+Release 0 warnings/0 errors
- [x] Real Windows GUI verification (`scripts/tl011-verify.ps1`): tray icon
      present/hidden-tray, minimize→hidden+alive+collectors running, history
      accumulates while hidden, close→hidden+alive+notice once, singleton HWND,
      CloseToTray=false→prompt clean exit+no ETW, 3 lifecycle cycles clean;
      tray-click restore/menu Exit covered by unit tests + same handlers
- [x] Docs updated (PROJECT_STATUS, TASKS, CHANGELOG, ARCHITECTURE, DECISIONS
      ADR-019)
- **Status: done**
- **Commit:** see `docs/PROJECT_STATUS.md` Git Commit section

### TL-001 Project Bootstrap — **DONE**
- [x] Inspect .NET environment (installed .NET 8 SDK 8.0.425)
- [x] Create `TrafficLens.sln` and solution folder structure
- [x] Create `TrafficLens.App` (WPF), `TrafficLens.Core`, `TrafficLens.Network`, `TrafficLens.Infrastructure`
- [x] Configure project references (no circular dependencies)
- [x] Establish DI composition root (`App.xaml.cs`) and MVVM base classes
- [x] Configure structured file logging (Infrastructure `FileLogger`)
- [x] Create required documentation
- [x] Initialize Git repository
- [x] Minimal dark main window with dashboard placeholders
- [x] Localization foundation: `ILocalizationService`, `Strings.resx` (en), `Strings.fa-IR.resx`
- [x] RTL-ready architecture (`FlowDirection` switching, LTR-safe values)
- [x] Build solution: success, 0 warnings
- [x] Verify startup: GUI smoke test passed (start, dark theme, en/fa localization,
      RTL culture, logs, clean shutdown)
- [x] Update `PROJECT_STATUS.md` and `TASKS.md`

### TL-002 Global Network Collector
- [x] Research and select collection mechanism (see `docs/NETWORK_COLLECTION.md`)
- [x] Implement `INetworkTrafficCollector` (`WindowsNetworkTrafficCollector`)
  - [x] Poll cumulative per-adapter counters (received/sent bytes)
  - [x] `CounterSampleReady` event fires per adapter per poll
  - [x] `NetworkChanged` on adapter set change (init + `NetworkChange.NetworkAddressChanged`)
  - [x] Counter reset/wrap detection with re-baseline logging
- [x] Implement `INetworkAdapterProvider` (`WindowsNetworkAdapterProvider`)
- [x] Adapter enumeration, kind mapping, filtering (Ethernet/Wireless/Tunnel/Virtual/Unknown)
- [x] Default adapter detection (up + gateway preferred, never "first adapter")
- [x] Aggregation policy avoiding double-counting (`NetworkTrafficAggregator`)
- [x] Handle adapter connect/disconnect
- [x] Unit tests: 36 tests passing (mapper, filter, default selector, aggregator, collector)
- [x] Real verification: console collector cross-checked vs `Get-NetAdapterStatistics`
- [x] Document accuracy and limitations
- **Status: done**
- **Commit:** `e3bef48`

### TL-003 Download/Upload Calculation
- [x] Compute rate samples from cumulative counter deltas / real monotonic elapsed
- [x] Per-adapter independent baselines (`SpeedRateTracker`) with re-baseline rules
- [x] First sample / counter reset / wrap / reconnect / replacement: no fake spikes
- [x] Raise `SpeedSampleReady`; `GetCurrentSamples()` returns current rate samples
- [x] System aggregate rate (tunnel-excluding policy) via `AggregateRates`
- [x] Per-adapter views keep tunnel/VPN traffic (nothing discarded)
- [x] Unit conversions for UI: B/s, KB/s, MB/s, Kbps, Mbps, Gbps (`DataRateConverter`)
- [x] No UI-thread dependencies, no busy loops, bounded baselines
- [x] Tests: 65 passing (calculator, tracker, conversions, aggregate, collector)
- [x] Real verification: rates plausibly aligned with `Get-NetAdapterStatistics`
- **Status: done**
- **Commit:** `6bfa9d6`

### TL-004 Network Adapter Detection
- [x] Enumerate Ethernet, Wi-Fi, VPN (OpenVPN TAP/DCO, WireGuard), virtual adapters
      (Hyper-V, VMware, Wi-Fi Direct) — all visible via `INetworkAdapterProvider`
- [x] Default adapter detection (up + gateway preferred, never "first adapter"; tunnel/virtual excluded from default)
- [x] Audit passed: ID/name/description/type/status, up/down, gateway awareness,
      all-adapters mode (down adapters kept), per-adapter rates, connect/disconnect,
      no duplicate logical entries
- [x] Gap fixed: OpenVPN TAP/DCO (interface type `HighPerformanceSerialBus`=53)
      were classified `Unknown` → description-aware classification (Tunnel/Virtual)
- [x] Tests: 80 passing (type + description-aware kind mapping, phantom-Unknown
      filter relaxation, TAP default-selection guard)
- **Status: done**
- **Commit:** `2bf03c9` (code + tests), `docs/PROJECT_STATUS.md` hash pointer

### TL-005 Dashboard
- [x] Live Download/Upload/Total cards from real collector rates (`SpeedSampleReady` + `AggregateRates`)
- [x] Adaptive rate formatting (B/s, KB/s, MB/s) + Mbps cards (`DataRateFormatter` in Core)
- [x] Active/preferred adapter card (name, kind, connected/disconnected) via default-selector
- [x] Per-adapter list (name, kind, up/down, current up/down rates); tunnels stay visible
- [x] System cards use the ADR-009/ADR-010 aggregate policy (tunnels excluded, no double count)
- [x] MVVM: DashboardViewModel consumes abstractions via DI, no networking in code-behind,
      UI updates marshalled to Dispatcher, events fully unsubscribed (IDisposable)
- [x] Localization: en + fa-IR resources for Dashboard/Download/Upload/Total/Active Adapter/
      Connected/Disconnected/Network Adapters/No active connection + kind names; RTL-safe layout
- [x] Connection state: no-network, disconnect, reconnect, VPN-only host, no-sample window — no crash
- [x] No polling in VM, no unbounded history, scalar-only rate updates per second
- [x] Tests: 15 formatter cases (Network.Tests) + 8 App.Tests (aggregate→VM, adapter→VM,
      no-network, reconnect, VPN-only honesty, culture switch, resource keys)
- [x] Real Windows GUI verification: dark dashboard renders live rates under real traffic
      (download 0 B/s → 844 KB/s / 6.92 Mbps → decaying), en + fa-IR both render and exit cleanly
- **Status: done**
- **Commit:** `bb6deef` (see `docs/PROJECT_STATUS.md`)

### TL-006 Live Traffic Graph
- [x] Real-time download/upload graph (30s / 1m / 5m ranges) with adaptive Y scale
- [x] Never block UI thread (collection thread writes, ViewModel marshals to
      Dispatcher, `OnRender` drawing only)
- [x] Bounded ring-buffer history (5.5 min retention, 1320 samples max, thread-safe;
      dedupe rejects same-poll duplicates; capacity/time retention enforced)
- [x] Time ranges with localized controls; switching never clears history
  (`TrafficSampleBuffer.Slice` is non-mutating)
- [x] Adaptive shared scale: immediate spike growth, hysteretic shrink (sustained
      2-update low), 2 KB/s floor (no divide-by-zero at zero traffic)
- [x] Native WPF rendering (`TrafficGraphControl` FrameworkElement + `OnRender`),
      two `StreamGeometry` series, no chart library, no per-sample UI elements
- [x] Timeline always oldest-left → newest-right even in RTL (control forces LTR)
- [x] No-network / disconnect / reconnect: no crash, history survives, ages out by
      retention, reconnect resumes; never fabricates non-zero data
- [x] Raw bytes/sec in graph data (Core graph models); formatting only at render
      time via `DataRateFormatter`
- [x] Localization: en + fa-IR for head, range and legend strings
- [x] Tests: Core graph (buffer/scale) + VM graph (append/dedupe/slice/reset/zero/large)
      + localized graph keys
- [x] Real Windows GUI verification: graph renders under live traffic, range buttons
      30/60/300 switch, en + fa-IR switch, resize, clean shutdown — no exceptions
- **Status: done**
- **Commit:** (see `docs/PROJECT_STATUS.md`)

### TL-007 Per-Process Traffic
- [x] ETW research grounded in `KernelTraceEventParser` source: payload PID fixup,
      size field, event IDs; Tcp/Udp + IPv4/IPv6 handlers; retransmit (id 14) excluded
- [x] `WindowsEtwProcessTrafficCollector` — real-time kernel network session
      (TCP/UDP, IPv4/IPv6), PID+size mapped into `NetworkTransferEvent`, ~1 s
      snapshot loop, `SamplesReady`/`GetCurrentSamples`; no payload capture
- [x] `ProcessTrafficAccountingEngine` — per-instance buckets by `(pid, start)`,
      monotonic sliding-window rates, metadata resolve/rekey, PID-reuse isolation,
      unknown processes kept in their own `<unknown pid N>` bucket (never merged),
      120 s idle prune + 4096 cap, no per-event allocation/logging/UI work
- [x] `WindowsProcessMetadataProvider` — guarded `Process` reads, PID-reuse
      detection via start-time mismatch, never throws
- [x] Collector health states: Stopped/Starting/Running/PermissionDenied/Failed;
      non-elevated → `PermissionDenied` + `LastError`, no crash, no forced UAC
- [x] Core contracts: `IProcessTrafficCollector` (+Status/LastError),
      `ProcessTrafficSample` (pid/start/name/path/totals/rates),
      `ProcessTrafficCollectorStatus`
- [x] DI registration (`IProcessTrafficCollector` singleton, Network layer owns the
      provider/session); VPN semantics documented, ADR-009/010 policy unchanged
- [x] Tests: 18 new (13 accounting-engine synthetic-event suite incl. PID reuse,
      rates, process exit, eviction, protocol/IP classification; 4 metadata
      provider; 1 graph count) — engine tested without any real ETW session
- [x] Real Windows verification: non-elevated → `PermissionDenied` evidence;
      elevated live run with curl.exe + powershell.exe → two distinguishable apps,
      two curl instances as distinct buckets, bounded (11 samples, ~2.6 MB growth),
      clean stop; protocol totals invariant `Total = Tcp + Udp = IPv4 + IPv6`
- [x] Applications list UI (M4 UI milestone):
  - [x] `ApplicationsViewModel` + `ProcessRowViewModel` — per-instance rows keyed
        by `ProcessInstanceId` (same PID + new start time = distinct row), in-place
        update (no re-add/flicker), top-consumer cards (now/download/upload),
        localized process status (`Running`/`Exited`/unknown)
  - [x] Sort (7 keys via `ProcessSampleSelection`/`ProcessSortKey`, Core, pure) +
        search (name case-insensitive substring + PID prefix); never mutates
        collector state
  - [x] Permission UX: `PermissionDenied`/`Failed`/`Stopped` banners + monitoring
        actions; explicit Restart-as-Administrator only — no auto-elevation (ADR-014)
  - [x] `ProcessIconResolver` — shell32 `SHGetFileInfo` P/Invoke, frozen fallback,
        bounded FIFO cache, UI-thread only (no System.Drawing)
  - [x] `MainWindow` Dashboard/Applications navigation + `ContentControl` host;
        process collector started at startup (App.xaml.cs)
  - [x] `DataSizeFormatter` (Core binary totals, culture-aware, negatives clamped)
  - [x] Localization: en + fa-IR for all Applications keys
  - [x] Tests: 11 selection/sort + 12 formatter cases (Network) + 16
        ApplicationsViewModel (App) — 45 new tests
  - [x] Real Windows GUI verification: elevated live run (ETW Running, ~20 MB/s
        survived, session closed cleanly on graceful close) and non-elevated run
        (PermissionDenied banner path, no session, no auto-UAC); 192/192 tests;
        0 warnings/errors Debug + Release
- **Status: done (backend + Applications UI)**
- **Commit:** `8f080fb` (code + tests; see `docs/PROJECT_STATUS.md`)

### TL-007F Shutdown deadlock fix (lingering process after graceful close)
- [x] Root cause (live repro + `dotnet-dump`, non-elevated): `App.OnExit` → DI
      disposes `WindowsNetworkTrafficCollector` via `StopAsync().GetAwaiter().GetResult()`
      on the WPF dispatcher thread; `await loop;` captured the
      `DispatcherSynchronizationContext`, so the continuation was posted to the
      blocked dispatcher → `StopAsync` never resumed, `App.OnExit` never returned,
      process lingered (no window, low CPU, one foreground thread)
- [x] Fix: `await loop.ConfigureAwait(false)` — deterministic shutdown from any
      thread/context; no `Environment.Exit`, no forced kill (ADR-015)
- [x] Regression test `Dispose_FromNonPumpingSyncContext_DoesNotDeadlock`
      (non-pumping `SynchronizationContext` + 5 s deadline) — fails (timeout)
      pre-fix, passes post-fix
- [x] Build Debug + Release: 0 warnings / 0 errors; tests **193/193** (163 Network
      + 30 App)
- [x] Real Windows GUI verification (Release):
      - Non-elevated: 3× launch → graceful close, each exits promptly; no residual
        process; no ETW session; log ends `TrafficLens exiting` → `Process traffic
        collector stopped` → `Network traffic collector stopped` (previously
        missing line)
      - Elevated: ETW session `TrafficLensProcessTrace` Running + ~20 MB download →
        graceful close → process exits, session gone from `logman query -ets`
- **Status: done**
- **Commit:** `e09111e` (code + tests; docs `f425dca`)

### TL-008 Active Connections
- [x] Core: extended `ConnectionInfo` (nullable remote endpoint, `ConnectionAddressFamily`,
      process start-time identity, executable path, icon availability, timestamp),
      `ConnectionKey` (protocol + family + local + remote + pid), `ConnectionProtocol`/
      `ConnectionState`/`ConnectionAddressFamily` enums, `EndpointFormatter`
      (LTR, culture-safe; unspecified/absent peer rendered empty — ADR-016)
- [x] `IConnectionProvider` extended (`ConnectionsChanged`, `LastError`,
      `GetCurrentConnections`, `GetActiveConnectionsAsync(ct)`, `StartAsync`,
      `StopAsync`, `IDisposable`)
- [x] Native collection `NativeConnectionTableReader` — `GetExtendedTcpTable`
      (`TCP_TABLE_OWNER_PID_ALL`) + `GetExtendedUdpTable` (`UDP_TABLE_OWNER_PID`),
      IPv4 + IPv6, 4-byte little-endian entry-count header, per-row layouts (TCPv4 24 B,
      TCPv6 56 B, UDPv4 12 B, UDPv6 28 B), network→host port byte order, 64 KB initial
      buffer grown on `ERROR_INSUFFICIENT_BUFFER`; partial-table failure tolerated
- [x] `ConnectionTableParser` — pure static parsers over the native buffers (unit-tested
      with synthetic payloads, no live table needed)
- [x] `WindowsConnectionProvider` — ~1 s off-UI poll loop; partial-table failure is a
      warning (successful tables kept); total failure keeps the last good snapshot + sets
      `LastError`; any success clears `LastError`; `StopAsync` uses `ConfigureAwait(false)`
      (ADR-015)
- [x] `ConnectionProcessResolver` — bounded cache (TTL 3 s, capacity 512, FIFO eviction,
      negative caching) over `IProcessMetadataProvider`, keyed by full `ProcessInstanceId`;
      never throws
- [x] Filter/search/sort (Core, pure, non-mutating): `ConnectionFilter`
      (All/Established/Listening/Tcp/Udp/Ipv4/Ipv6), `ConnectionFiltering`,
      `ConnectionSort` (Default/Process/ProcessId/Protocol/State/Local/Remote) with
      deterministic tie-breaks; UDP remotes are never fabricated, LISTEN is not outbound
- [x] App: `ConnectionsViewModel` + `ConnectionRowViewModel` — dispatcher-marshalled
      `ConnectionsChanged`, in-place row updates (rebuild only when the key sequence
      changes), icon budget per refresh, error banner, empty state, Filter + FamilyFilter
      + Sort + search
- [x] `ConnectionsView` (XAML + code-behind DI); `MainWindow` Dashboard/Connections
      navigation; `App.xaml.cs` registers the VM/View and starts `IConnectionProvider`
- [x] Localization: en + fa-IR keys (column headers, filters, sort, TCP states, empty/
      error, unknown process); endpoints stay LTR under RTL
- [x] Tests: `ConnectionTableParserTests`, `ConnectionKeyTests`, `ConnectionSelectionTests`,
      `EndpointFormatterTests` (Network) + `ConnectionsViewModelTests` (App)
- [x] Real verification (`--connections`): 112 connections (78 TCP / 34 UDP, 99 IPv4 /
      13 IPv6), in-process listener observed as `Listen`, curl download attributed
      `Established` with correct PID/name/remote; cross-checked vs `netstat -ano`
      (TCP state histogram + UDP count match the MIB source — `Get-NetTCPConnection`'s
      `Bound` rows are cmdlet-synthesized, not in the owner-PID table)
- [x] GUI smoke: `Connection provider started (IP Helper tables, poll interval 00:00:01)`,
      no exceptions, clean teardown (no orphan ETW session / leftover process)
- **Status: done**
- **Commit:** `c29adf4` (code + tests; see `docs/PROJECT_STATUS.md`)

### TL-009 SQLite History
- [x] Core history domain (`TrafficLens.Core/History`): `HistoryRange`,
      `TrafficUsage`, `DailyUsagePoint`, `HistorySnapshot` (immutable, `.For(range)`),
      `TrafficHistoryBucket`, `HistoryRangeCalculator` (half-open local-date ranges;
      DST/local-midnight correct via `TimeZoneInfo`), `TrafficHistoryAccumulator`
      (baseline-only first observation, non-negative deltas, per-UTC-minute buckets,
      `DrainCompleted`/`DrainAll`), `ITrafficHistoryRepository`, `ITrafficHistoryService`
- [x] Infrastructure persistence (`TrafficLens.Infrastructure/History`):
      `SqliteTrafficHistoryRepository` (schema v1 via `PRAGMA user_version`; tables
      `traffic_samples` + `daily_usage`; WAL + busy_timeout; `Pooling=false`;
      semaphore-gated writes; `INSERT OR IGNORE` + `changes()==1` → restart can never
      duplicate history; `daily_usage` upsert; 90-day raw prune on startup),
      `TrafficHistoryService` (rides existing `CounterSampleReady` — never a second
      poll loop; adapter-kind map refreshed on `AdaptersChanged`; tunnel exclusion by
      default mirrors ADR-009/010; 30 s flush loop; `StopAsync` drains + flushes;
      cached immutable `HistorySnapshot` so SQL never runs on the UI thread),
      `HistoryServiceCollectionExtensions.AddHistoryServices(dbPath)`
- [x] `AppPaths` — database under `%LOCALAPPDATA%\TrafficLens\data\trafficlens.db`;
      `EnsureDirectories` creates the `data` folder
- [x] App History page: `HistoryViewModel` (five ranges, summary cards, banner when
      unavailable), `HistoryView`, native `HistoryBarChartControl` (FrameworkElement,
      always oldest-left → newest-right under RTL; Today/Yesterday = 1 bar, 7d = 7 bars,
      30d/Lifetime = 30 bars), `MainViewModel.ShowHistoryCommand` + nav label,
      `MainWindow` History host, DI + start in `App.xaml.cs`
- [x] Localization: en + fa-IR keys (HistoryLabel, HistoryDailyTrafficLabel,
      TodayLabel, YesterdayLabel, Last7DaysLabel, Last30DaysLabel, LifetimeLabel,
      HistoryNoDataLabel, HistoryUnavailableLabel); reused download/upload/total labels
- [x] Tests: `TrafficLens.Infrastructure.Tests` (new project, added to solution) —
      range calculator, accumulator, SQLite repository (temp DBs, idempotent append,
      daily/lifetime queries, prune, reopen/schema), service (delta→flush→shutdown,
      restart no-duplication, tunnel exclusion) — 27 tests
- [x] App tests: `HistoryViewModelTests` (6) + localization resource keys (en + fa)
- [x] Real Windows verification (`--history`): live collector + real DB; 20 MB curl
      download recorded as ~20.18 MB Today with peak 5.78 MB/s; second service
      instance against the same DB → Lifetime unchanged (restart idempotency)
- [x] Docs: `DATABASE.md` finalized, `ARCHITECTURE.md`, `PROJECT_STATUS.md`,
      `CHANGELOG.md`, ADR-017
- **Status: done**
- **Commit:** `861269c` (see `docs/PROJECT_STATUS.md`)

### TL-010 Floating Widget
- [x] `FloatingWidgetViewModel` — subscribes to the existing `SpeedSampleReady`/
      `NetworkChanged`/`AdaptersChanged` events (never its own poll loop or timer),
      computes the ADR-009/010 aggregate via `NetworkTrafficAggregator.AggregateRates`,
      formats Download/Upload/Total through `DataRateFormatter` (LTR units), localized
      labels (FloatingWidgetLabel, DownloadLabel, UploadLabel, TotalRateLabel),
      `TogglePinCommand` (raises `PinStateChanged`) + `CloseWidgetCommand` (raises
      `CloseRequested`), UI-thread marshalling like the dashboard, IDisposable
- [x] `FloatingWidgetWindow` (280×110, `WindowStyle=None`, `ResizeMode=NoResize`,
      `ShowInTaskbar=False`, `Topmost` from settings) — dark theme via existing
      DarkTheme.xaml brushes, drag by empty area (`DragMove`, ignores clicks on
      buttons), pin toggle button + hide (✕) button bound to commands, values forced
      `FlowDirection=LeftToRight` under RTL; only necessary code-behind is drag
- [x] `FloatingWidgetService` (singleton) — owns the single widget instance:
      `Show`/`Hide`/`Toggle`/`RestoreIfEnabled`, idempotent re-show (activates, never
      duplicates), widget-close = hide only (`Closing` cancelled), position
      persisted as `FloatingWidgetLeft`/`FloatingWidgetTop`, always-on-top persisted
      as `FloatingWidgetAlwaysOnTop` (default on), startup visibility persisted as
      `FloatingWidgetEnabled`, `Dispose` removes the cancel-handler and closes the
      window for real (shutdown cannot be pinned open)
- [x] `WidgetPositionHelper.Clamp` — pure multi-monitor clamping: union of monitor
      work areas, negative virtual-screen coordinates preserved (secondary monitor
      left of primary), off-screen / monitor-disconnected recovery, window larger
      than work area collapses to top-left; real areas from
      `SystemParameters.VirtualScreen*`
- [x] Main UI: toggle button in the `MainWindow` header
      (`Show Floating Widget` / `Hide Floating Widget` exchange based on
      `IsVisibleChanged`), `MainViewModel.ToggleFloatingWidgetCommand` +
      `FloatingWidgetToggleLabel`, `MainWindow.Closing` → `Hide()` the widget +
      dispose viewmodel; `App.xaml.cs` registers the service + `RestoreIfEnabled()`
      on startup
- [x] Shutdown safety (ADR-015/TL-007F): widget is a secondary window; main-window
      close hides it, then `OnLastWindowClose` shuts the app down normally — no
      `Environment.Exit`, no hidden window holding the process, no new foreground
      thread; no orphan ETW sessions
- [x] Localization: en + fa-IR keys (FloatingWidgetLabel, AlwaysOnTopLabel,
      ShowFloatingWidgetLabel, HideFloatingWidgetLabel); Download/Upload/Total reused
      (`DownloadLabel`/`UploadLabel`/`TotalRateLabel`)
- [x] Tests: `FloatingWidgetViewModelTests` (aggregate→VM mapping, tunnel/down
      exclusion, formatting, culture re-format + labels, pin icon, dispose) +
      `WidgetPositionHelperTests` (inside/off-screen right&bottom/negative
      coords/secondary-monitor-left/multi-monitor union/empty/larger-than-work-area)
      + localization resource keys — 18 new (App 51 → 69; total 284 → 302)
- [x] Build Debug + Release: 0 warnings / 0 errors
- [x] Real Windows GUI verification: show/hide, live rate changes, dashboard
      compatibility, drag, restart → position restored, topmost toggles + persists,
      repeat show no duplicate, main close → widget gone → process exits, no orphan
      ETW session; en + fa-IR render
- **Status: done**
- **Commit:** (see `docs/PROJECT_STATUS.md`)

### TL-011 System Tray
- [ ] Tray icon, show/hide, minimize to tray, exit
- **Status: not started**

### TL-012 Alerts
- [ ] Local alert architecture (usage thresholds, notifications)
- **Status: not started**

### TL-013 Settings
- [ ] Settings UI and persistence
- **Status: not started**

### TL-014 CSV Export
- [ ] Export app / daily / hourly usage to CSV
- **Status: not started**

### TL-015 Packaging / Installer
- [ ] x64 packaging and installer
- **Status: not started**

### TL-016 Localization / Persian UI
- [ ] Runtime language switcher, full fa-IR translation
- **Status: not started**

## Milestones

| Milestone | Title | Tasks | Status |
|---|---|---|---|
| M0 | Project bootstrap, docs, localization foundation | TL-001 | Done |
| M1 | Global network monitoring | TL-002, TL-003 | Done |
| M2 | Dashboard and live graph | TL-005, TL-006 | Done |
| M3 | Network interfaces | TL-004 | Done |
| M4 | Per-process traffic | TL-007 | Done |
| M5 | Active connections | TL-008 | Done |
| M6 | SQLite history | TL-009 | Done |
| M7 | Tray and widget | TL-010, TL-011 | In progress (TL-010 done; TL-011 owns tray) |
| M8 | Alerts and settings | TL-012, TL-013 | Not started |
| M9 | Stability, performance, tests, packaging | TL-015 | Not started |
| M10 | Full Persian localization | TL-016 | Not started |