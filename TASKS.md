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

### TL-012 Alerts — **DONE**
- [x] Core alert domain (`src/TrafficLens.Core/Alerts`, no WPF/OS dependencies):
      `AlertType` (HighDownloadSpeed / HighUploadSpeed / DailyDownloadLimit /
      DailyUploadLimit / DailyTotalLimit with `IsSpeedRule`/`IsDailyUsageRule`),
      `AlertConfig` (immutable record + `Default()`; 5 rules all **disabled by
      default** with suggested thresholds 50 MB/s / 20 MB/s / 50 GB / 20 GB /
      100 GB and 5 min cooldown), `AlertEvent` (type/value/threshold/occurred),
      `AlertSignal`, `AlertEngine` (pure, gate-locked, clock-injected), and
      `AlertHistoryBuffer` (session-only, capacity 100, newest-first)
- [x] Speed semantics (tested): triggers on **upward crossing only**;
      `rate ≥ threshold` consumes the armed crossing and signals only when
      `now ≥ CooldownUntilUtc`; dropping below re-arms but **does not clear the
      cooldown** — remaining above never repeats, flapping yields ≤ 1 alert per
      cooldown window; no spam
- [x] Daily semantics (tested): at most once per **local calendar day**
      (DST-safe via injected `TimeZoneInfo`); the last-triggered local date is
      persisted (`alerts.lastTriggered.…`) on trigger and restored on
      construction so a same-day restart (or crash) never re-fires; next local
      day re-arms
- [x] Alert pipeline never touches SQL: speed evaluated from
      `INetworkTrafficCollector.GetCurrentSamples()` +
      `INetworkAdapterProvider.GetAdapters()` via the existing
      `NetworkTrafficAggregator.AggregateRates` (ADR-009/010 policy) on
      `SpeedSampleReady`; daily evaluated from the cached
      `ITrafficHistoryService.GetSnapshot()` on `HistoryChanged` — history
      unavailable ⇒ daily rules suspended silently, speed continues
- [x] App services (`TrafficLens.App/Services`): `AlertSettings` (flat settings
      keys + Load/Save + Load/SaveTriggeredDates, invariant culture),
      `AlertNotification` + `AlertMessageFormatter` (structured, re-localized on
      culture change), `IAlertService` + `AlertService` (singleton, owns the
      engine, subscribes both pipelines, raises `AlertRaised`, logs every
      trigger, clean `Dispose`)
- [x] Tray delivery: `ISystemTrayService.ShowAlert(title, message)` +
      `SystemTrayService.ShowAlert` (RunOnUi, EnsureCreated, Warning balloon,
      8 s, dropped+logged if tray unavailable); `BalloonTipClicked` →
      `OpenRequested` (same singleton-restore handler verified in TL-011)
- [x] Alerts page (M8 UI surface): `AlertsViewModel` (count formatter,
      TimeText via `ToLocalTime`, newest-first rows), `Views/AlertsView.xaml`
      (+DI code-behind), `MainViewModel` Alerts nav + `ShowAlertsCommand`,
      `MainWindow` Alerts button + `AlertsHost` ContentControl, `App.xaml.cs`
      registrations + `AlertRaised → ShowAlert` wiring
- [x] Localization en + fa-IR: `AlertsNavLabel`, `AlertsTitleLabel`,
      `AlertsNoAlertsLabel`, `AlertsCountFormat`, `AlertTitle`, 6×
      `AlertType*`, 5× `AlertMsg*`; `LocalizationResourceTests.RequiredKeys`
      extended
- [x] Tests: `AlertEngineTests` (17 engine cases — upward-crossing-only,
      cooldown window, no-repeat-while-above, re-arm, once-per-day, DST-safe
      UTC+14 local-day identity, next-day re-arm, restore semantics; 3 buffer
      tests), `AlertServiceTests` (10), `AlertsViewModelTests` (4);
      `Fakes.cs` gained `FakeHistoryService`/`FakeAlertService`/
      `FakeTrayService.ShowAlert`
- [x] Build Debug + Release: **0 warnings / 0 errors**; **349/349 tests**
      (App 116 / Network 206 / Infrastructure 27)
- [x] Real Windows GUI verification (`scripts/tl012-verify.ps1`): (A) speed
      256 KB/s threshold + controlled `1Gb.dat` download → exactly one
      notification, no spam while above, re-arm after drop-below; (B) daily
      total 10 MB → once per local day, `alerts.lastTriggered.dailyTotal`
      persisted, same-day restart → no repeat; (C) notification while hidden
      in tray (no "notification dropped", tray icon alive); (D) full
      regression with default-disabled alerts + graceful exit, no ETW orphans
- [x] Docs updated (PROJECT_STATUS, TASKS, CHANGELOG, ARCHITECTURE, DECISIONS
      ADR-020)
- **Status: done**
- **Commit:** `6512011` (code + tests + script; see `docs/PROJECT_STATUS.md`)

### TL-013 Settings — **DONE**
- [x] Full Settings page (`SettingsView.xaml` + DI code-behind, nav button +
      host in `MainViewModel`/`MainWindow`): General (language combo,
      start-with-Windows, start-minimized, tray minimize/close), Floating
      Widget (enable, always-on-top, show/hide-from-page buttons), Alerts
      (5 rules with enable checkbox + threshold field + unit combo + cooldown
      minutes 1–1440)
- [x] Staged-save model: numeric fields/dropdowns apply on **Save** (validate →
      one logical persist → runtime apply → `RefreshFromSettings`); tray
      checkboxes apply **immediately** (no Save); Reset stages defaults behind
      a Yes/No confirmation dialog; `SavedNotice` + dirty tracking
- [x] `AlertRuleViewModel` per rule: threshold entered in the displayed unit
      (unit combo KB/s / MB/s / GB/s / TB/s for speed; MB/GB/TB for daily),
      converted invariant-bytes on persist, re-parse on load; cooldown
      validation bounds 1–1440 with inline error text
- [x] Save wiring uses `IFloatingWidgetService.SetAlwaysOnTop` (added) so an
      unchecked always-on-top **persists and applies** (the old
      `ToggleAlwaysOnTop` flipped relative to the already-written setting and
      re-enabled topmost); tray menu keeps the toggle
- [x] Startup registration: `IStartupRegistrationService` +
      `StartupRegistrationService` (HKCU `…\Run` value `TrafficLens` = quoted
      exe path, optional ` --minimized`, removes only its own value name);
      `--minimized` CLI arg starts hidden to tray (log marker `Starting hidden
      to system tray`)
- [x] `JsonSettingsService` hardening (tested): partial file merges defaults,
      unknown keys preserved on Save, malformed JSON falls back to defaults
      without crashing
- [x] Localization en + fa-IR for every Settings string (nav/title/sections/
      labels/units/save/reset/confirm/notice); `LocalizationResourceTests.
      RequiredKeys` extended
- [x] Tests: `SettingsViewModelTests` (Save staging/validation/language apply/
      widget state via `SetAlwaysOnTop`/alert config/reset defaults),
      `JsonSettingsServiceTests` (merge/preserve/malformed), fakes +
      localization keys; **382/382** (App 138 / Network 206 / Infrastructure
      38), Debug + Release 0 warnings / 0 errors
- [x] Real Windows GUI verification (`scripts/tl013-verify.ps1`, blocks A–I):
      partial-merge + unknown-key preservation and malformed fallback through
      the real page; en→fa→en combo switch + restart; tray immediate-apply +
      close-to-tray; widget enable/topmost-off/hide/show + restart
      persistence; page-configured 256 KB/s threshold → real alert → Reset with
      native `Reset to Defaults` confirmation (found + answered via Win32)
      → defaults + history DB untouched; HKCU Run create/quoted/--minimized/
      own-value-only removal; `--minimized` hidden start + tray alive; restart
      persistence; graceful exit with no orphan ETW sessions
- [x] Docs updated (PROJECT_STATUS, TASKS, CHANGELOG, ARCHITECTURE, DECISIONS
      ADR-021)
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

### TL-011 System Tray — **done** (see the completed block at the top)
### TL-012 Alerts
- [ ] Local alert architecture (usage thresholds, notifications)
- **Status: done** (see the completed block at the top)

### TL-013 Settings
- [ ] Settings UI and persistence
- **Status: not started**

### TL-014 Stability & Performance Audit — **DONE**
- [x] Harness F1: warm-up + idle (60 s) — dashboard/connections/history/GC handles
      pass; WS +8 MB, handles +6, threads +9
- [x] Harness F2: 30-min soak — max CPU ≤15% single-core in 30 s windows
      (achieved avg 3.43%, max 9.88%)
- [x] Fix 1 — DashboardViewModel event coalescing: `_refreshPending` flag +
      `CoalesceRefresh()` merges all per-adapter `SpeedSampleReady` events into
      one `RefreshRates` call per second (N events/s → 1 layout pass/s)
- [x] Fix 2 — ConnectionsViewModel page-visibility gating: `_isActive` flag,
      `SetActive(bool)` wired from `MainViewModel.SelectPage`; inactive state
      stores pending data without dispatching to UI thread
- [x] Fix 3 — WindowsConnectionProvider polling pause: `SetPollingEnabled(bool)`
      on `IConnectionProvider` skips `EnumerateOnce()` (4 native P/Invokes +
      process resolution for ~159 connections) when Connections page hidden
- [x] Fix 4 — Default deactivation: `MainViewModel` constructor calls
      `Connections.SetActive(false)` since Dashboard is default page
- [x] Pre-fix evidence: main thread 50% on-CPU (22449 samples/45 s) via
      dotnet-trace Speedscope; N events/s dispatch storm confirmed
- [x] Post-fix evidence: main thread 1.9% on-CPU (113 samples/60 s); F2 soak
      30 min: avg 3.43%, max 9.88% (all under 15%)
- [x] Harness F3: memory leak (10 min soak) — WS +5.8 MB, Private +15.5 MB,
      handles -3, threads -9 — PASS
- [x] Harness F4: navigation stress — 50 nav, 25 widget, 25 window, 10 restart
      cycles — PASS
- [x] SingleInstanceGuard (6 tests), FileLoggerProvider retention (3 tests),
      SpeedRateTracker sleep-gap test — all green
- [x] Final: 397 tests (App 145, Network 212, Infrastructure 40), 0 warnings,
      0 errors (Debug + Release)
- [x] Docs: ADR-022, TASKS, PROJECT_STATUS, CHANGELOG, PERFORMANCE.md
- **Status: done**
- **Commit:** (see `docs/PROJECT_STATUS.md` Git Commit section)

### TL-015 Packaging / Installer
- [x] x64 packaging and installer
- [x] `scripts/build-release.ps1` one-command release pipeline
- [x] Single-file self-contained win-x64 publish (`TrafficLens.exe`)
- [x] Inno Setup 6 per-user installer (no PDBs, running-app notice, never force-kill)
- [x] Install / uninstall / reinstall / upgrade verification with user-data preservation
- [x] Installer + portable ZIP SHA-256; artifacts gitignored
- [x] Metadata: clean product version 0.1.0, FileDescription, app.manifest
- [x] Docs: PACKAGING.md, ADR-023, PROJECT_STATUS, CHANGELOG
- **Status: done**
- **Commit:** (see `docs/PROJECT_STATUS.md` Git Commit section)

### TL-016 Localization / Persian UI
- [x] Audit: resx key parity (en + fa-IR), `LocalizationService` (RTL, CultureChanged,
      fallback), runtime switcher already in place (`SwitchToEnglishCommand`/
      `SwitchToPersianCommand`, Settings `LanguageOptions`, persisted via settings)
- [x] No hard-coded user-facing strings: graph "now" label now via `NowLabel`
      DependencyProperty (bound to Dashboard `GraphNowLabel`, default "now");
      all UI text through `Strings*.resx` except endonyms (English / فارسی) and brand
- [x] Error/status surfaces localized instead of raw English provider messages:
      Applications (`PermissionDeniedDetailLabel`, `MonitoringFailedDetailLabel`),
      Connections (`ConnectionsErrorDetailLabel`), History (`HistoryErrorDetailLabel`);
      raw detail stays in app log
- [x] Culture-aware history chart date labels (`ToString(..., CultureInfo.CurrentCulture)`
      so fa-IR uses the Persian calendar)
- [x] en + fa-IR key parity maintained: 146 keys each, identical key sets
      (`LocalizationResourceTests.RequiredKeys` extended)
- [x] Tests updated/added: 398 tests (App 146, Network 212, Infrastructure 40) — localized
      detail assertions, raw English never surfaced, fa-IR detail localization
- [x] Build Debug + Release: 0 warnings / 0 errors
- [x] Real GUI verification (`scripts/tl016-verify.ps1` on published single-file build):
      en-US labels render, fa-IR content scan (nav + dashboard + History page in Persian,
      no English leak), graceful exit, no orphan process/ETW; TL-015 smoke regression
      still passes
- **Status: done**
- **Commit:** (see `docs/PROJECT_STATUS.md` Git Commit section)

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
| M7 | Tray and widget | TL-010, TL-011 | Done |
| M8 | Alerts and settings | TL-012, TL-013 | Done |
| M9 | Stability & performance | TL-014 | Done |
| M10 | Packaging / installer | TL-015 | Done |
| M11 | Full Persian localization | TL-016 | Done |