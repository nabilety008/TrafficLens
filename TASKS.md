# TrafficLens — TASKS.md

## How to use

- Optional tasks are marked `[optional]`.
- A task is DONE only when implemented, verified (built/run/tested), and documented.
- Task IDs are stable. Do not renumber existing IDs.

## Backlog

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

### TL-008 Active Connections
- [ ] `IConnectionProvider`, TCP-first connections view
- **Status: not started**

### TL-009 SQLite History
- [ ] Aggregated sampling schema and repositories
- [ ] Today / Yesterday / 7d / 30d / Lifetime views
- **Status: not started**

### TL-010 Floating Widget
- [ ] Compact always-on-top widget
- **Status: not started**

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
| M5 | Active connections | TL-008 | Not started |
| M6 | SQLite history | TL-009 | Not started |
| M7 | Tray and widget | TL-010, TL-011 | Not started |
| M8 | Alerts and settings | TL-012, TL-013 | Not started |
| M9 | Stability, performance, tests, packaging | TL-015 | Not started |
| M10 | Full Persian localization | TL-016 | Not started |