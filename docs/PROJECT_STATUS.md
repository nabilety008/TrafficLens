# TrafficLens — Project Status

Updated: 2026-09-14

## Current Milestone

M1 (global monitoring), M3 (network interfaces), and M2 (TL-005 dashboard +
TL-006 live graph) are complete.

## Task IDs

- TL-001 Project Bootstrap — **DONE**
- TL-002 Global Network Collector — **DONE**
- TL-003 Download/Upload Calculation — **DONE**
- TL-004 Network Adapter Detection — **DONE** (audit + gap fix)
- TL-005 Dashboard — **DONE**
- TL-006 Live Traffic Graph — **DONE**
- TL-007 and later — not started

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

## Verified

- `dotnet build TrafficLens.sln`: **Success, 0 warnings, 0 errors** (Debug and Release).
- **Automated tests:** 129/129 passed (`TrafficLens.Network.Tests` 115, `TrafficLens.App.Tests` 14).
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

- `tests/TrafficLens.Network.Tests` — xUnit, 115 tests, all passing (incl. 20 graph
  buffer/scale tests).
- `tests/TrafficLens.App.Tests` — xUnit (net8.0-windows, WPF), 14 tests, all passing
  (incl. 6 dashboard-graph tests).
- `tests/TrafficLens.Network.Verification` — console harness; run with
  `dotnet run --project tests/TrafficLens.Network.Verification`.

## Known Issues / Not Started

- Per-adapter tunnel rates are published; only the system aggregate excludes them
  by default (`includeTunnels: true` to include on VPN-only hosts).
- Graph history is in-memory only (5.5 min); persistent SQLite history is TL-009.
- Per-process and connections are later milestones.
- Range buttons do not show an explicit "selected" highlight; the selected range is
  visually implied by the plotted window. (Future polish.)

## Git Commit

- TL-006 (live traffic graph): `3ad4b86` — `feat: add live traffic graph with bounded history and adaptive scale (TL-006)`; docs `6f24811`.
- TL-005 (dashboard): `bb6deef` — `feat: add live dashboard view with adaptive rate formatting (TL-005)`
- `2bf03c9` — `fix: classify OpenVPN TAP/DCO (type 53) and virtual nics correctly via driver descriptions (TL-004)`
- `6bfa9d6` — `feat: add download/upload rate calculation from counter deltas with monotonic timing (TL-003)`
- `e3bef48` — TL-002 collector; `7f841be` — M0 smoke test; `d8f2933` — TL-001 bootstrap.

## Next Recommended Task

- TL-007 — Per-process traffic: `IProcessTrafficCollector` with process mapping to
  connections/ports (requires ETW or elevated counters; see M4). M2 is now complete,
  so the graph backlog (TL-006) is done; TL-007 is the next scheduled milestone.