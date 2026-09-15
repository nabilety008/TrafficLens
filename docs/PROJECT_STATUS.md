# TrafficLens — Project Status

Updated: 2026-09-15

## Current Milestone

M7 (system tray) is **complete**: TL-010 (floating widget) and TL-011 (system
tray) are both done and verified — one tray icon with Open / Show-Hide Widget /
Always-on-Top / Exit, minimize-to-tray and close-to-tray (defaults true), a
single idempotent exit coordinator feeding a clean graceful shutdown, a
once-ever close-to-tray balloon, runtime-drawn localized tray menu, and the
floating widget pin tooltip bound to the shared label. M1, M2, M3, M4
(per-process traffic), M5, and M6 (SQLite history) are complete. M8 (settings /
next milestone) is the scheduled follow-up.

## Task IDs

- TL-001 Project Bootstrap — **DONE**
- TL-002 Global Network Collector — **DONE**
- TL-003 Download/Upload Calculation — **DONE**
- TL-004 Network Adapter Detection — **DONE** (audit + gap fix)
- TL-005 Dashboard — **DONE**
- TL-006 Live Traffic Graph — **DONE**
- TL-007 Per-Process Traffic — **DONE** (collector + engine + verification +
  Applications-list UI)
- TL-007F Shutdown deadlock fix — **DONE**
- TL-008 Active Connections — **DONE**
- TL-009 SQLite History — **DONE**
- TL-010 Floating Widget — **DONE**
- TL-011 System Tray — **DONE**
- TL-012 and later — not started

## Completed

- TL-001: Solution and four projects; DI/MVVM; structured JSON logging; dark main
  window; localization (en + fa-IR, RTL-ready); required docs; git repo.
- TL-002: Global network collector implemented end-to-end:
  - `WindowsNetworkTrafficCollector` (background poll loop, per-adapter cumulative
    counters, `CounterSampleReady`/`NetworkChanged` events, `NetworkChange` hooks,
    counter reset/wrap re-baseline, non-blocking started/stopped).
  - `WindowsNetworkAdapterProvider` (implements `INetworkAdapterProvider`).
  - Adapter source, kind mapping, filtering, and default-adapter selection
    (`NetworkInterfaceSource`, `NetworkAdapterKindMapper`, `AdapterFilter`,
    `DefaultAdapterSelector`) plus `RawAdapterSnapshot`.
  - `NetworkTrafficAggregator` with a double-counting-avoidance policy.
  - Core contract extended: `NetworkCounterSample` model + counter members on
    `INetworkTrafficCollector` (see ADR-007).
  - xUnit test project `TrafficLens.Network.Tests`; verification console
    `TrafficLens.Network.Verification`.

- TL-003: Download/upload rate calculation:
  - `NetworkSpeedCalculator` — cumulative-counter deltas / real monotonic elapsed
    (QPC), never an assumed 1 s interval.
  - `SpeedRateTracker` — per-adapter baselines with re-baseline rules (first
    sample, reset/wrap/decrease, zero elapsed, disappearance/replacement); no
    fake spikes.
  - `SpeedSampleReady` raised with real rates; `GetCurrentSamples()` returns them.
  - `NetworkTrafficAggregator.AggregateRates` — system "Internet Total",
    tunnel-excluding by default; per-adapter views keep tunnel/VPN traffic.
  - `DataRateConverter` (Core) — B/s, KB/s, MB/s, Kbps, Mbps, Gbps for the UI.
  - Extension of ADR-009 policy to rates; ADR-010 (monotonic clock).
  - Tests added (calculator, tracker, conversions, aggregate, collector rates).
- TL-004 (audit + targeted fix):
  - Verified all existing TL-002/TL-003 adapter detection satisfies the checklist:
    Ethernet/Wi-Fi/OpenVPN TAP/DCO/Hyper-V/VMware/all-virtual visible;
    ID/name/description/type/status, up/down, gateway awareness, default preferred
    selection, all-adapters mode (down adapters kept), per-adapter rates,
    connect/disconnect events, GUID-unique identity — all covered.
  - Gap found and fixed: OpenVPN TAP/DCO drivers register as
    `HighPerformanceSerialBus` (type 53) and were classified `Unknown`.
    Added description-aware `NetworkAdapterKindMapper.Map(type, description)`
    (tap-windows/openvpn/wintun/wireguard → Tunnel;
    virtual/vmware/hyper-v/vethernet/virtualbox → Virtual).
    `AdapterFilter.IsMonitored` relaxed for Unknown-type adapters with
    recognized tunnel/virtual descriptions so WireGuard (or similar) remains
    visible if its O/S type is Unknown. `DefaultAdapterSelector` uses the
    description-aware overload so TAP stays non-default.
- TL-005: Dashboard connected to real TL-002/TL-003 data:
  - `DataRateFormatter` (Core) — adaptive B/s/KB/s/MB/s and Mbps, culture-aware
    decimal separator; unit symbols intentionally untranslated (ADR-011).
  - `DashboardViewModel` + `AdapterListItemViewModel` (MVVM, DI, no code-behind
    networking). Consumes `INetworkTrafficCollector` + `INetworkAdapterProvider`;
    subscribes to `SpeedSampleReady` and adapter-change events; all updates
    marshalled to the WPF Dispatcher; `IDisposable` unsubscribes all handlers.
  - Prominent Download/Upload/Total cards with **real live rates** from
    `NetworkTrafficAggregator.AggregateRates` (ADR-009/ADR-010 policy — tunnels
    excluded from the system total, never double-counted).
  - Active/preferred adapter card (name, kind, Connected/Disconnected) via the
    existing default-selector — never the first adapter.
  - Compact adapter list (friendly name, kind, up/down, current ↓/↑ rates);
    VPN/TAP/DCO/Wi-Fi-Direct virtuals stay visible with their own per-adapter
    rates, distinct from the system aggregate.
  - Connection states handled without crashing: no network, disconnect,
    reconnect, VPN-only host (honest zero/absent totals + per-adapter tunnel
    rates), collector temporarily without a sample.
  - Localization: en + fa-IR strings for Dashboard labels, adapter kinds, and
    connection states; RTL-safe layout (rate texts forced LTR, FlowDirection
    follows culture).
  - `TrafficLens.App.Tests` (net8.0-windows, WPF-ready): aggregate→VM mapping,
    adapter→VM mapping, no-network, reconnect-after-disconnect, VPN-only honesty,
    culture-switch reformatting, localization resource existence.
- TL-006: Live traffic graph (completes M2):
  - Core graph layer (`TrafficLens.Core/Graph`) — no WPF dependencies, raw
    bytes/second only:
    - `TrafficGraphPoint` — timestamped raw down/up rates (no formatted strings).
    - `TrafficSampleBuffer` — bounded (5.5 min retention / 1320 samples) thread-safe
      ring buffer; dedupe rejects same-poll duplicate timestamps; age-out by
      wall-clock timestamp; `Slice(window, now)` is non-mutating so range
      switching never clears history; preserves real timestamps (no assumed 1 s
      interval), so dropouts are rendered as honest gaps.
    - `AdaptiveGraphScale` — single shared Y max for both series; immediate
      growth on spikes; hysteretic shrink (sustained low < 35% for 2 consecutive
      updates) to avoid flicker; 2 KB/s floor prevents divide-by-zero at zero
      traffic (ADR-012).
    - `GraphTimeRange` — 30 s / 60 s / 300 s enumeration.
  - `TrafficGraphControl` (App, `Controls/`) — light native WPF `FrameworkElement`
    drawing both series as `StreamGeometry` in `OnRender`; grid + axis labels via
    `DataRateFormatter`; forces `FlowDirection=LeftToRight` so the timeline is
    always oldest-left → newest-right even under fa-IR.
  - `DashboardViewModel` — feeds the existing `AggregateRates` aggregate into the
    buffer once per poll (dedupe handles multi-adapter `SpeedSampleReady` bursts);
    `SelectGraphRangeCommand` + per-range selection flags; localized graph labels.
  - `MainWindow.xaml` — "Live Traffic" section with range buttons (30 s / 1 m / 5 m),
    download/upload legend, and the graph control.
  - Localization: en + fa-IR for live-traffic heading, range and series labels.
  - Tests: graph buffer/scale suites in `TrafficLens.Network.Tests`; VM graph
    appends/dedupe/range-slice/no-network/zero/large tests in
    `TrafficLens.App.Tests`; localized graph keys in resource tests.

- TL-007 (per-process traffic, collector milestone — M4 backend):
  - `ProcessTrafficCollectorStatus` (Stopped/Starting/Running/PermissionDenied/Failed),
    extended `IProcessTrafficCollector` (Status/LastError), richer
    `ProcessTrafficSample` (pid + start-time identity, name, path, icon-available,
    byte totals, monotonic-window rates, Timestamp) — all in Core.
  - `WindowsEtwProcessTrafficCollector` (Network/Process) — real-time ETW kernel
    session (`TraceEventSession`, `NetworkTCPIP`); eight Tcp/Udp IPv4/IPv6 event
    handlers mapping payload PID + size into `NetworkTransferEvent` (never the raw
    header PID — DPC-computed receive completions would mis-attribute to
    System/Idle); ~1 s snapshot loop raising `SamplesReady`; non-elevated hosts
    report `PermissionDenied` + `LastError` without crashing and without forcing
    UAC.
  - `ProcessTrafficAccountingEngine` — per-instance buckets keyed by
    `ProcessInstanceId = (pid, process start time)`; monotonic sliding-window rates
    (never assumed 1 s); metadata resolve/rekey once the true start time is known;
    **PID-reuse isolation** (mismatched start time stops attribution to the old
    bucket and starts a fresh instance); unknown/unresolvable processes kept in
    their own `<unknown pid N>` bucket, never merged into another process; idle
    prune 120 s, hard cap 4096 buckets (oldest-LastSeen eviction); hot path is
    allocation-free (`CollectionsMarshal.GetValueRefOrAddDefault`); no per-event
    UI work, no logging, no SQLite.
  - `WindowsProcessMetadataProvider` — guarded `System.Diagnostics.Process` reads
    (name/path/icon/start time), PID-reuse detection by start-time mismatch
    (>2 s tolerance), never throws.
  - `ProcessProtocolTotals` — per instance Tcp/Udp × Received/Sent and IPv4/IPv6 ×
    Received/Sent with the tested invariant `Total = Tcp + Udp = IPv4 + IPv6`.
  - DI: `IProcessTrafficCollector` singleton registered in
    `NetworkServiceCollectionExtensions`; provider/session owned by the Network
    layer; document VPN/tunnel semantics (owner-attributed app bytes, not
    interface bytes; ADR-009/010 aggregate policy unchanged).
  - Real-time kernel provider requires elevation; detail + rationale in
    `docs/NETWORK_COLLECTION.md` (TL-007 section) and ADR-013.

- TL-007 (per-process traffic, Applications-list UI milestone — M4 UI):
  - `ProcessSampleSelection`/`ProcessSortKey` (Core) — pure sample filtering and
    seven sort keys (total/download/upload rate, downloaded/uploaded/total bytes,
    name) with deterministic tie-breaking (name, start time, PID); presentation
    concern, never mutates collector state.
  - `DataSizeFormatter` (Core) — binary unit totals (B/KB/MB/GB, culture-aware,
    negatives clamped); totals are technical notation, matching ADR-011.
  - `ApplicationsViewModel` + `ProcessRowViewModel` (App) — consumes
    `IProcessTrafficCollector.SamplesReady` once per second, keeps
    per-instance rows keyed by `ProcessInstanceId` (same PID + different start
    time = distinct rows), updates rows in place (no flicker/re-add), exposes
    top-consumer (now/download/upload) cards, sort + search (name substring
    case-insensitive + PID prefix), localized status text, and per-row state from
    the sample's `IsRunning` flag (`Running`/`Exited`/unknown).
  - Privilege UX: non-elevated hosts show a permission banner with
    `StartMonitoringCommand`, `Failed`/`Stopped` show monitoring controls; the
    only elevation path is an explicit `Restart-as-Administrator` command
    (`Process.Start` `runas` + shutdown) — **the app never auto-elevates**
    (ADR-014).
  - `ProcessIconResolver` (App) — shell32 `SHGetFileInfo` P/Invoke
    (`SHGFI_ICON | SHGFI_LARGEICON`) + `Imaging.CreateBitmapSourceFromHIcon` +
    `DestroyIcon`, frozen fallback, bounded FIFO cache (128) with max 8
    extractions per refresh — icons resolved on the UI thread only; no
    `System.Drawing` dependency.
  - `MainWindow` — Dashboard / Applications navigation buttons (nav row), content
    hosted in a `ContentControl` switched by `MainViewModel.ShowDashboardCommand`
    / `ShowApplicationsCommand`; `IProcessTrafficCollector` started at startup in
    `App.xaml.cs` (network collector + process collector).
  - Localization: all Applications keys added to `Strings.resx` (en) and
    `Strings.fa-IR.resx` (fa, valid UTF-8).

- TL-009 (SQLite history, M6):
  - Core history domain (`TrafficLens.Core/History`), no WPF/OS dependencies:
    - `HistoryRange` (Today/Yesterday/Last7Days/Last30Days/Lifetime),
      `TrafficUsage` (immutable record struct: download/upload/total),
      `DailyUsagePoint`, `HistorySnapshot` (immutable; `For(range)` derives
      per-range totals; `Unavailable(lastError)`).
    - `HistoryRangeCalculator` — half-open local-date ranges using
      `TimeZoneInfo.ConvertTimeFromUtc` → `DateOnly`, so DST + local midnight are
      correct; ranges verified by tests (Today `(today, today+1)`,
      Last7Days `(today-6, today+1)`, Last30Days `(today-29, today+1)`).
    - `TrafficHistoryAccumulator` — extends `NetworkCounterSample` DELTAS into
      per-UTC-minute buckets; first observation per adapter is baseline-only;
      non-negative deltas only; positive deltas from all eligible adapters summed
      per minute; `DrainCompleted` (full 60 s buckets) vs `DrainAll` (includes the
      open minute, duration clamped 1..60) for shutdown flush.
    - Contracts `ITrafficHistoryRepository` + `ITrafficHistoryService` (cached
      immutable `HistorySnapshot`; `HistoryChanged`; SQL never runs on the UI thread).
  - Infrastructure (`TrafficLens.Infrastructure/History`):
    - `SqliteTrafficHistoryRepository` — SQLite via `Microsoft.Data.Sqlite`;
      schema v1 (`PRAGMA user_version`), tables `traffic_samples`
      (`bucket_start_utc INTEGER PK, bucket_duration_seconds, download_bytes,
      upload_bytes`) and `daily_usage` (`local_date TEXT PK, download_bytes,
      upload_bytes`); `journal_mode=WAL`, `busy_timeout=5000`, `Pooling=false`
      (deterministic handles; keeps tests from holding file locks); every append
      is one transaction guarded by `INSERT OR IGNORE` + `changes()==1` before the
      `daily_usage` upsert so an app restart can never duplicate history;
      `daily_usage` kept forever, raw samples pruned older than 90 days on startup;
      `QueryDailyAsync` / `QueryLifetimeAsync` / `PruneRawSamplesBeforeAsync`.
    - `TrafficHistoryService` — subscribes the existing collector's
      `CounterSampleReady` (never starts a second NIC polling loop), keeps an
      adapter-kind map refreshed on `AdaptersChanged`, excludes tunnels by default
      (same rules as the ADR-009/010 aggregate), flushes *completed* minute buckets
      every 30 s in a background loop, re-baselines on resets/reconnects/reboots so
      measurements are never fabricated, and flushes everything on stop (graceful
      shutdown preserves at most the current open minute).
    - `HistoryServiceCollectionExtensions.AddHistoryServices(dbPath)`.
  - `AppPaths` — `DataDirectory = Root\data`, `DatabaseFile`,
    `EnsureDirectories` creates the `data` folder.
  - App History page:
    - `HistoryViewModel` — Five-range buttons (Today/Yesterday/Last 7 Days/Last 30
      Days/Lifetime) bound via `SelectRangeCommand`; Download/Upload/Total cards
      and a localized "No history yet" overlay when `Lifetime.TotalBytes == 0`;
      unavailable-banner when storage failed (`HistoryUnavailableLabel` +
      `LastError`), all from the cached immutable snapshot via `HistoryChanged`.
    - `HistoryView` (XAML + code-behind DI) hosting a native
      `HistoryBarChartControl` (`FrameworkElement`, `OnRender`, no chart library):
      bars always oldest-left → newest-right regardless of `FlowDirection`;
      Today/Yesterday render a single bar, 7 days → 7 bars, 30 days/Lifetime →
      30 daily bars; tooltips show `date → DataSizeFormatter` totals.
    - `MainViewModel.ShowHistoryCommand` + localized nav label; `MainWindow`
      History host alongside Dashboard/Applications/Connections;
      `App.xaml.cs` registers the ViewModel/View and starts the history service
      (fire-and-forget with error logging) after the network collector.
  - Localization: `HistoryLabel`, `HistoryDailyTrafficLabel`, `TodayLabel`,
    `YesterdayLabel`, `Last7DaysLabel`, `Last30DaysLabel`, `LifetimeLabel`,
    `HistoryNoDataLabel`, `HistoryUnavailableLabel` added to `Strings.resx` (en)
    and `Strings.fa-IR.resx`; download/upload/total label reuse.
- TL-010 (floating widget, M7):
  - `FloatingWidgetViewModel` — thin event-driven VM over the existing rate
    pipeline (`SpeedSampleReady`/`NetworkChanged`/`AdaptersChanged`, no own poll
    loop or timer; ADR-009/010 aggregate via `NetworkTrafficAggregator`), formats
    Download/Upload/Total via `DataRateFormatter` (units stay LTR under RTL),
    localized title/labels, `TogglePinCommand` + `CloseWidgetCommand`,
    dispatcher-marshalled updates, IDisposable (mirrors `DashboardViewModel`).
  - `FloatingWidgetWindow` — 280×110 frameless (`WindowStyle=None`,
    `ResizeMode=NoResize`), `ShowInTaskbar=False`, dark theme from the shared
    `DarkTheme.xaml` brushes, drag by empty area (`DragMove`), pin toggle + hide
    buttons bound to commands, values forced LTR; no second taskbar app.
  - `FloatingWidgetService` (singleton) — owns the single widget instance;
    `Show`/`Hide`/`Toggle`/`RestoreIfEnabled`; repeat show only activates (no
    duplicates); widget close is hide-only (`Closing` cancelled); `Dispose`
    detaches the cancel handler and really closes the window so shutdown is never
    pinned open.
  - `WidgetPositionHelper.Clamp` — pure multi-monitor-position recovery: union of
    monitor work areas, negative virtual-screen coordinates preserved (secondary
    monitor left of primary), off-screen/disconnected-monitor clamping, oversized
    window folded to top-left; real areas from `SystemParameters.VirtualScreen*`.
  - Settings via the existing `ISettingsService` (single `settings.json`):
    `FloatingWidgetEnabled` (startup restore), `FloatingWidgetAlwaysOnTop`
    (default on), `FloatingWidgetLeft`/`FloatingWidgetTop`.
  - Main UI: header toggle button (`Show`/`Hide Floating Widget` exchange on
    `IsVisibleChanged`), `MainViewModel.ToggleFloatingWidgetCommand` +
    `FloatingWidgetToggleLabel`, `MainWindow.Closing` hides the widget then normal
    shutdown proceeds (ADR-015/TL-007F — no `Environment.Exit`, no hidden window
    keeping the process alive); started via `App.xaml.cs`
    (`RestoreIfEnabled` after services start).
  - Localization: `FloatingWidgetLabel`, `AlwaysOnTopLabel`,
    `ShowFloatingWidgetLabel`, `HideFloatingWidgetLabel` in en + fa-IR;
    Download/Upload/Total reuse existing keys.
- TL-011 (system tray, M7):
  - `SystemTrayService`/`ISystemTrayService` (App) — a single WinForms
    `NotifyIcon` over a plain `<FrameworkReference
    Include="Microsoft.WindowsDesktop.App.WindowsForms" />` (`UseWPF` kept, no
    `UseWindowsForms` ⇒ no WinForms global usings / no CS0104). One icon created
    once, disposed only on real exit; tooltip `TrafficLens`; no ghost; hidden
    `WindowsForms10..._ad1` message window as the in-process proxy.
  - Runtime-drawn 32×32 icon (dark rounded square + `#4FC3F7`/`#26A69A`
    chevrons on `#1E1E2E`, readable 16–32 px; `GetHicon`+`FromHandle`,
    `DestroyIcon` on dispose).
  - Tray menu relabeled in place on culture change (never recreated): Open
    TrafficLens / Show-Hide Floating Widget / Always on Top (checkable) /
    separator / Exit. Always on Top drives the same widget pin state; the widget
    pin tooltip/accessibility binds the shared `AlwaysOnTopLabel` (TL-010
    polish).
  - `TrayBehavior` (pure) — `MinimizeToTray`/`CloseToTray` (default true),
    `TrayCloseNoticeShown`; `ResolveCloseAction` → Exit|HideToTray; once-only
    close notice.
  - `ApplicationExitCoordinator` — single idempotent `RequestApplicationExit()`
    (latch → dispose tray+widget → `Shutdown()`, Dispatcher-safe); no
    `Environment.Exit`/`Process.Kill`; collectors/history/DI disposed via
    container. `App.xaml` `ShutdownMode="OnExplicitShutdown"`.
  - `MainWindow`: `Closing` → `ResolveCloseAction` (Exit calls the coordinator;
    HideToTray cancels+Hides+balloon), `StateChanged` minimize→Hidden,
    `OpenRequested` restores the singleton window. `MainViewModel` exposes the
    two tray settings + localized labels in a minimal `…` options popup (TL-013
    owns the full settings page).
  - Localization en + fa-IR: `OpenTrafficLensLabel`, `ExitLabel`,
    `MinimizeToTrayLabel`, `CloseToTrayLabel`, `TrayCloseNoticeBalloon`; internal
    tray technical name stays LTR.
- TL-008 (active connections, M5):
  - Core (`TrafficLens.Core`):
    - `ConnectionInfo` extended — nullable remote endpoint, `ConnectionAddressFamily`,
      process start-time identity (`ProcessStartTimeUtcTicks`), `ExecutablePath`,
      `IconAvailable`, `Timestamp`; new `ConnectionProtocol`, `ConnectionState`,
      `ConnectionAddressFamily` enums.
    - `ConnectionKey` — stable identity `(Protocol, AddressFamily, LocalAddress,
      LocalPort, RemoteAddress?, RemotePort?, ProcessId)` used for in-place row updates.
    - `EndpointFormatter` — culture-safe, always-LTR `address:port` formatting; the
      remote endpoint renders **empty** for a listening/unconnected socket
      (unspecified `0.0.0.0`/`::` + port 0) instead of a misleading peer (ADR-016).
    - `Selection/ConnectionSelection` — pure, non-mutating `ConnectionFilter`
      (All/Established/Listening/Tcp/Udp/Ipv4/Ipv6), `ConnectionFiltering.Matches`/
      `MatchesSearch`, and `ConnectionSort` (Default/Process/ProcessId/Protocol/State/
      Local/Remote) with deterministic tie-breaks (name, PID, local, remote).
  - Native collection (`TrafficLens.Network/Connections`):
    - `NativeConnectionTableReader` — `GetExtendedTcpTable` (`TCP_TABLE_OWNER_PID_ALL`)
      + `GetExtendedUdpTable` (`UDP_TABLE_OWNER_PID`), IPv4 and IPv6; parses the 4-byte
      little-endian entry-count header and per-row layouts (TCPv4 24 B, TCPv6 56 B,
      UDPv4 12 B, UDPv6 28 B); network→host port byte order; 64 KB initial buffer grown
      on `ERROR_INSUFFICIENT_BUFFER`.
    - `ConnectionTableParser` — pure static parsers over the native buffers, unit-tested
      with synthetic payloads (no live table required).
    - `ConnectionProcessResolver` — bounded cache (TTL 3 s, capacity 512, FIFO eviction,
      negative caching) over `IProcessMetadataProvider`, keyed by full
      `ProcessInstanceId`; never throws.
    - `WindowsConnectionProvider` — ~1 s off-UI poll loop; a partial-table failure is a
      warning (successful tables are kept), a total failure keeps the last good snapshot
      and sets `LastError`, and any success clears it; `StopAsync` uses
      `ConfigureAwait(false)` (ADR-015).
  - App:
    - `ConnectionsViewModel` + `ConnectionRowViewModel` — dispatcher-marshalled
      `ConnectionsChanged` handler, in-place row updates (rebuild only when the key
      sequence changes), per-refresh icon budget, error banner, empty state, Filter +
      Address-Family + Sort combo boxes and a search box.
    - `ConnectionSortOption` / `ConnectionFilterOption` (localized option records);
      `ConnectionsView` (XAML + code-behind DI); `MainWindow` Dashboard/Connections
      navigation; `App.xaml.cs` registers the ViewModel/View and starts the provider.
    - Localization: en + fa-IR keys for headers, filters, sort, TCP states, empty/error,
      and unknown process; endpoints remain LTR under RTL.

## Verified

- `dotnet build TrafficLens.sln`: **Success, 0 warnings, 0 errors** (Debug and Release).
- **Automated tests:** 317/317 passed (`TrafficLens.Network.Tests` 206,
  `TrafficLens.App.Tests` 84, `TrafficLens.Infrastructure.Tests` 27).
- **TL-009 real Windows history verification** (`--history` mode, live host, throwaway
   temp DB — never the user's `%LOCALAPPDATA%` DB):
   - Pipeline ran end-to-end: live collector → accumulator → minute buckets →
     SQLite; a real 20 MB `speed.cloudflare.com` download was recorded as
     **Today = 20,182,568 B down / 79,655 B up** (20 MB requested + realistic
     counter-delta overhead), matching the observed peak rate of ~5.78 MB/s.
   - Yesterday / 7d / 30d / Lifetime consistent; `daily_usage` series spans
     exactly 30 local days (2026-08-17 → 2026-09-15) with today's row populated.
   - **Restart idempotency proven:** a second service instance started against the
     same database over a fresh collector reported the identical Lifetime
     (`unchanged: true`) — the `INSERT OR IGNORE` + `changes()==1` guard prevents
     double-counting across restarts/crashes.
   - DB footprint after two sessions + 20 MB of traffic: **16,384 bytes**.
- **TL-010 real Windows floating-widget verification** (Release build, Win32
  `EnumWindows`/`PostMessage`/`SetWindowPos` scripting, 3 launch-close cycles):
  - **Startup restore:** widget window appeared at launch when
    `FloatingWidgetEnabled=true` (3/3 cycles).
  - **Move + position persistence:** `SetWindowPos` moved the widget to
    (400, 300); after hiding the widget and restarting, the saved
    `FloatingWidgetLeft`/`FloatingWidgetTop` matched the moved position.
  - **Widget close = hide only:** WM_CLOSE to the widget hides only the widget
    window; the main window remains visible and responsive.
  - **Main window close exits cleanly:** WM_CLOSE to the main window exits the
    process promptly within 15 s (all 3 cycles); no shutdown hang.
  - **No orphan ETW sessions:** `logman query -ets` confirmed no TrafficLens
    sessions remain after each cycle.
  - **Restart restore:** relaunched with `enabled=true` → 2 windows appeared
    (main + widget).
  - **fa-IR coexistence:** widget launches under fa-IR culture with correct
    titles; culture-set log line confirmed.
- **TL-011 real Windows system-tray GUI verification** (Release build,
  `scripts/tl011-verify.ps1`, `scripts/tl011-debug.ps1` +
  `scripts/tl011-shelldump.ps1`/`tl011-uiaprobe.ps1`/`tl011-dblclk.ps1`/
  `tl011-keynav.ps1` diagnostics):
  - **Tray icon created:** WinForms NotifyIcon message window
    (`WindowsForms10.Window.0.app.<hash>_r3_ad1`, app-owned) present at startup
    and while hidden.
  - **Minimize-to-tray:** `WM_SYSCOMMAND SC_MINIMIZE` → main hidden, process
    alive, tray icon still present, network collector + history service running.
  - **History accumulates while hidden:** with the app hidden, a live download
    produced WAL/mtime activity in `data\trafficlens.db` (SQLite keeps a single
    16 KB page; WAL write confirmed via db-wal growth / mtime change ~25 s).
  - **Singleton restore model:** same main HWND survives minimize/hide (no
    second instance).
  - **Close-to-tray:** `WM_CLOSE` → main hidden, process alive,
    `TrayCloseNoticeShown=True` persisted once; a second close leaves it True and
    keeps the app alive.
  - **CloseToTray=false → real exit:** fresh launch with the flag false →
    `WM_CLOSE` exits promptly (<15 s) through the coordinator; no orphan ETW
    session after exit.
  - **3 lifecycle cycles:** launch → minimize → close-to-tray → teardown × 3 —
    no ghost processes, no ETW orphans, tray icon recreated each run.
  - **Honest coverage note:** tray double-click restore and the tray-menu Exit
    cannot be synthesized against the Windows 11 XAML tray from this session
    (no `SysPager`/`ToolbarWindow32` child, UI Automation finds no
    "TrafficLens" tray element, Win+B tray keyboard nav unreachable); both are
    unit-tested (`TrayBehavior`/`ApplicationExitCoordinator`) and wired through
    the same `OpenRequested`/`ExitRequested` handlers verified here via
    minimize/close and the `CloseToTray=false` pipeline.
  - **Verification caught a real bug:** the `CloseToTray=false` X path merely
    closed the window without calling the exit coordinator, leaving the process
    alive under `OnExplicitShutdown`; fixed by calling
    `RequestApplicationExit()` in the `MainWindow.Closing` exit branch (see
    ADR-019).
- **TL-008 real Windows verification** (`--connections` mode, non-elevated, live host):
  - **TrafficLens `--connections`**: 112 connections (78 TCP / 34 UDP, 99 IPv4 /
    13 IPv6), 26 established / 29 listening; `udpWithRemote = 0` and
    `unknownProcess = 0`. An in-process `TcpListener` on `127.0.0.1` was observed as a
    `Tcp / Ipv4 / Listen` row owned by the verification process; a `curl` download was
    attributed one `Established` `Tcp / Ipv4` row (`local → 162.159.140.220:443`) with
    the correct PID and process name.
  - **Cross-check vs native MIB source (`netstat -ano`)**: TrafficLens TCP state
    histogram (`TimeWait 32 / Listen 29 / Established 21 / CloseWait 5`) matched
    `netstat` (31 / 28 / 21 / 5) at the same moment; TrafficLens UDP count (34) matched
    `netstat` UDP (34) exactly.
  - **`Get-NetTCPConnection` difference explained**: that cmdlet reported ~22 extra
    TCP rows in a synthetic `Bound` state that do **not** appear in `netstat` nor in the
    `TCP_TABLE_OWNER_PID_ALL` MIB table our provider reads — i.e. a cmdlet-side
    convenience state, not data we drop. Targeted listener row matched the native view
    exactly (`127.0.0.1:18888`, remote `0.0.0.0:0`, `Listen`, correct owning PID).
  - **GUI smoke** (non-elevated): log shows
    `Connection provider started (IP Helper tables, poll interval 00:00:01)`, no
    connection-provider exceptions during polling, ETW session absent before/after, and
    no leftover process; the process collector's elevation warning is expected and
    non-fatal.
- **TL-007 Applications UI real Windows GUI verification** (Release build, live
  network traffic, elevated + non-elevated runs):
  - Elevated run (app pid 5048): ETW session `TrafficLensProcessTrace` reported
    **Running** with buffers written; app survived ~20 MB/s-scale transfers
    (curl + PowerShell WebClient artifacts ≈ 14.1 MB, 16.6 MB, 20 MB); log shows
    `Process traffic collector running (elevated ETW kernel network session)`;
    graceful `CloseMainWindow` → `TrafficLens exiting` → `Process traffic
    collector stopped` → ETW session gone from `logman query -ets` (no orphan
    kernel session after graceful shutdown).
  - Non-elevated run (pid 7812): app started, network (dashboard) collector ran,
    process collector logged `cannot start: permission denied (Enabling the ETW
    kernel network provider requires an elevated (Administrator) process.)` →
    `PermissionDenied` banner path — no crash, no auto-UAC, and no ETW session
    created (`logman`: "Data Collector Set was not found").
- **TL-007 per-process real verification** (elevated verification console, live
  traffic against `https://speed.cloudflare.com/__down`, `--process` mode):
  - Collector turned `Running` (elevated) and attributed real traffic to the two
    apps it launched — `curl.exe` (6,236,307 B down / 674 B up) and
    `powershell.exe` (4,013,430 B down / 388 B up), all TCP/IPv4, with the
    protocol totals invariant holding (`Total = Tcp + Udp = IPv4 + IPv6`).
  - Two sequential `curl` instances (distinct PIDs and start times) appeared as
    **two separate buckets** with independent totals (6,236,307 vs 1,555,415 B) —
    per-instance attribution of the same executable, not per-name.
  - Bounded: 11 samples observed (hard cap 4096), ~2.6 MB managed memory growth
    over the whole run; clean `StopAsync` with no orphaned ETW session.
  - Non-elevated path verified separately: `Status = PermissionDenied` with
    `LastError` "Enabling the ETW kernel network provider requires an elevated
    (Administrator) process." — no crash, no forced UAC.
- **TL-006 real Windows GUI verification** (Release build, live network activity against
  `https://speed.cloudflare.com/__down`, UIA snapshots + Win32 resize):
  - App launched cleanly (log: startup → culture en-US → MainWindow shown → collector
    started); no errors/exceptions anywhere in the structured log.
  - "Live Traffic" section rendered with three range buttons (Last 30 seconds / Last 1
    minute / Last 5 minutes), download + upload legend, and the graph control.
  - Under live traffic: rates updated continuously on the cards (0.01 Mbps → 10.68 Mbps
    download / 1.27 MB/s adaptive while downloading 10 MB); after the download finished,
    rates decayed back to idle — the graph follows the aggregate stream.
  - Range buttons invoked via UIA (Last 5 minutes → Last 30 seconds): no crash, no
    exception, history retained.
  - Perssian switch (فارسی) → all graph labels/range strings render in fa-IR, app stays
    responsive; switch back to English restored en strings; settings.json restored to
    `{"language":"en-US"}`.
  - Window resized 800×700 and 1200×500 via Win32 `MoveWindow`: control re-renders,
    app stays `Responding=True`, no exceptions.
  - Clean shutdown: `CloseMainWindow` → log line "TrafficLens exiting", process exited.
- **TL-005 real Windows GUI verification** (Release build, live network activity against
  `https://speed.cloudflare.com/__down`, snapshots via UI Automation):
  - Download card: **0 B/s → 844.31 KB/s (6.92 Mbps)** → decaying to 32.74 KB/s across
    three snapshots while traffic flowed; upload/total changed in lockstep
    (real, changing data — never placeholders).
  - Active adapter section rendered (W-Fi up + gateway selected as default).
  - Adapter list showed all monitored adapters: Wi-Fi, Bluetooth PAN (Ethernet),
    OpenVPN TAP, OpenVPN DCO, Wi-Fi Direct virtuals — VPN/TAP/DCO **visible**.
  - English dashboard: labels "Dashboard / Download / Upload / Total / Active
    Adapter / Network Adapters / Connected / Disconnected" rendered correctly.
  - Persian (fa-IR, launched with `settings.json`: language fa-IR): culture set to
    fa-IR (log), Persian labels rendered, RTL-active; rates show fa decimal
    separator (`27٫68 KB/s`); no crash; clean exit ("TrafficLens exiting" in log).
  - Clean shutdown confirmed: `CloseMainWindow` → process exits, log line
    "TrafficLens exiting". No update storm (1 sample/s, scalar-only re-render).
- **Real Windows rate verification** (TL-003), concurrent native + collector run:
  - Collector window-mean Wi-Fi: **803,647 B/s** down / **18,423 B/s** up (10.02 s).
  - Native `Get-NetAdapterStatistics` delta: **1,120,590 B/s** down / **26,086 B/s**
    up (14.57 s, longer overlapping window).
  - Same magnitude/ordering/adapter; exact equality not expected (ADR-010).
- **TL-004 real verification** (live adapter classifier output):
  - OpenVPN TAP-Windows → **Tunnel** (was Unknown; type 53 + description heuristic).
  - OpenVPN DCO → **Tunnel**.
  - Wi-Fi Direct Virtual Adapter → **Virtual** (was Wireless; now type-accurate).
  - Wi-Fi (Intel AX201, up, gateway) → Wireless, default selected.
  - Bluetooth PAN → Ethernet, down, all adapters visible.
- Prior verification (TL-002): cumulative counters matched native statistics;
  default adapter = Wi-Fi (up + gateway, non-tunnel).

## Build

- .NET 8 SDK 8.0.425 at `C:\dotnet`.
- Command: `C:\dotnet\dotnet.exe build TrafficLens.sln`

## Tests

- `tests/TrafficLens.Network.Tests` — xUnit, 206 tests, all passing (incl. 22
  per-process accounting-engine tests, 11 selection/sort tests, 12 data-size
  formatter cases, metadata-provider tests, and the TL-008 connection parser /
  key / selection / endpoint-formatter suites).
- `tests/TrafficLens.App.Tests` — xUnit (net8.0-windows, WPF), 84 tests, all passing
  (incl. 6 dashboard-graph tests, 16 Applications-ViewModel tests, the TL-008
  Connections-ViewModel tests, 6 TL-009 History-ViewModel tests, 8 TL-010
  FloatingWidget-ViewModel tests, 10 TL-010 position-clamp tests, 10 TL-011
  TrayBehavior tests, 4 TL-011 ApplicationExitCoordinator tests, and resource keys).
- `tests/TrafficLens.Infrastructure.Tests` — xUnit, 27 tests, all passing (TL-009:
  HistoryRangeCalculator, TrafficHistoryAccumulator, SqliteTrafficHistoryRepository
  over throwaway temp databases, TrafficHistoryService with fake collector/provider).
- `tests/TrafficLens.Network.Verification` — console harness; run with
  `dotnet run --project tests/TrafficLens.Network.Verification` (adapter),
  `-- --process` (per-process, elevated or non-elevated),
  `-- --connections` (active connections, non-elevated; set `TL_VERIFY_PORT` for a
  fixed listener port), or `-- --history` (TL-009 history, throwaway DB + live traffic).

## Known Issues / Not Started

- A hard process kill (`taskkill /F`) leaves the kernel ETW real-time session
  Running until stopped explicitly (`logman stop "TrafficLensProcessTrace" -ets`);
  graceful close does not leak the session.
- Per-adapter tunnel rates are published; only the system aggregate excludes them
  by default (`includeTunnels: true` to include on VPN-only hosts). Because the
  system history follows that same policy, a **VPN-only host records ~zero history**
  (tunnel bytes are never attributed to the system totals). Documented in
  `docs/NETWORK_COLLECTION.md` + ADR-009/017.
- History chart shows daily bars for 7/30/Lifetime; an hourly-or-finer view for the
  current day is a documented future refinement (raw minute samples are retained
  90 days, so it only needs a query + range).
- History records system/global totals only; per-process and per-connection history
  are out of scope (TL-007/TL-008 are live-only).
- Active-connection state is live-only (no history) and report raw IP endpoints; no
  reverse DNS in TL-008 (deferred).
- Range buttons do not show an explicit "selected" highlight; the selected range is
  visually implied by the plotted window. (Future polish.)

## Resolved

- **Lingering `TrafficLens.App` after graceful window Close (TL-007F).** Root
  cause: `WindowsNetworkTrafficCollector` disposes on the WPF dispatcher thread via
  `StopAsync().GetAwaiter().GetResult()`; `await loop;` without
  `ConfigureAwait(false)` captured the `DispatcherSynchronizationContext`, re-posting
  the continuation to a dispatcher blocked in `GetResult()` → `StopAsync` never
  resumed and `App.OnExit` never returned. Fixed with `await
  loop.ConfigureAwait(false)` (ADR-015); verified 3× non-elevated launch→close +
  elevated ETW-running→close, all exiting promptly with the previously-missing
  `Network traffic collector stopped` now logged and no orphaned session/process.

## Git Commit

- TL-011 (system tray): `6e4d122` — `feat: add system tray with minimize/close-to-tray, singleton restore, and coordinator-based exit (TL-011)`; docs `8f230ec`.
- TL-010 (floating widget): `f3dc2e9` — `feat: add always-on-top floating widget reusing live aggregate rates with persisted position/topmost (TL-010)`; docs `a527168`.
- TL-009 (SQLite history): `861269c` — `feat: add SQLite traffic history with ranges, History page, and native bar chart (TL-009)`.
- TL-008 (active connections): `c29adf4` — `feat: add active connections provider and Connections view via IP Helper owner-PID tables (TL-008)`.
- TL-007F (shutdown deadlock fix): `e09111e` — `fix: prevent shutdown deadlock by not capturing the SynchronizationContext in collector StopAsync (TL-007F)`; docs `f425dca`.
- TL-007 (per-process traffic, Applications-list UI): `27ca92d` — `feat: add per-process Applications view with sort, search, icons, and permission UX (TL-007)`; docs `b1059c6`.
- TL-007 (per-process traffic collector): `8f080fb` — `feat: add per-process traffic collector via real-time ETW kernel network events (TL-007)`; docs `6f5dc49`.
- TL-005 (dashboard): `bb6deef` — `feat: add live dashboard view with adaptive rate formatting (TL-005)`
- `2bf03c9` — `fix: classify OpenVPN TAP/DCO (type 53) and virtual nics correctly via driver descriptions (TL-004)`
- `6bfa9d6` — `feat: add download/upload rate calculation from counter deltas with monotonic timing (TL-003)`
- `e3bef48` — TL-002 collector; `7f841be` — M0 smoke test; `d8f2933` — TL-001 bootstrap.

## Next Recommended Task

- TL-011 (system tray) is complete and verified, closing M7. Next scheduled item
  is the **TL-012..TL-013 milestone (settings)** — the full Settings page
  (language, start-with-Windows, tray options) that subsumes the minimal tray
  options popup introduced in TL-011.