# TrafficLens — Decisions (ADR)

This log records architecture/design decisions and the reasons behind them.

## ADR-001: Project split into four assemblies

**Status:** Accepted (TL-001)

Decided to split the codebase into `App / Core / Network / Infrastructure`.

Reasoning:
- Keeps UI, domain contracts, collection logic, and infrastructure replaceable.
- Prevents circular dependencies and makes testing easier.
- Matches the spec's required solution structure.

## ADR-002: Microsoft.Extensions.DependencyInjection for DI

**Status:** Accepted (TL-001)

Using the standard MS DI container with a composition root in `App.xaml.cs`.

Reasoning:
- Built into .NET, familiar to agents, no third-party dependency needed for a small app.
- Can be replaced later if requirements grow.

## ADR-003: .NET resx resources + ILocalizationService for localization

**Status:** Accepted (TL-001)

User-facing strings come from `Strings.resx` (neutral/English) and satellite
`Strings.fa-IR.resx`. An `ILocalizationService` abstracts lookup and fallback.

Reasoning:
- Standard .NET approach; no third-party library.
- English is neutral so fallback is trivial.
- `IsRightToLeft` + `FlowDirection` switching prepare the UI for Persian without redesign.

## ADR-004: Structured JSON file logging in Infrastructure

**Status:** Accepted (TL-001)

Custom `FileLoggerProvider` writes one JSON object per line under
`%LOCALAPPDATA%\TrafficLens\logs`.

Reasoning:
- Lightweight, no external provider; infrastructure project owns it.
- Structured output is machine-parseable for later diagnostics.
- Logging never throws (must not crash the app).

## ADR-005: Network collection deferred to TL-002

**Status:** Accepted (TL-001, changed to TBD)

Contracts (`INetworkTrafficCollector`, etc.) are defined now; `TrafficLens.Network`
stays empty until the collection mechanism is researched.

Reasoning:
- Spec explicitly forbids implementing collection during bootstrap milestone.
- Choosing the correct Windows mechanism (perf counters vs IP Helper vs ETW) requires
  investigation documented in `docs/NETWORK_COLLECTION.md`.

## ADR-006: .NET 8 (LTS) targeting

**Status:** Accepted (TL-001)

Targets `net8.0` (projects) and `net8.0-windows` (App). SDK 8.0.425 installed locally.

Reasoning:
- .NET 8 is LTS; net8.0-windows enables WPF.
- Widely supported on Windows 10/11 x64.

## ADR-007: Split cumulative counters from rates in the collector contract

**Status:** Accepted (TL-002)

`INetworkTrafficCollector` now exposes:
- `event EventHandler<NetworkCounterSample>? CounterSampleReady`
- `IReadOnlyList<NetworkCounterSample> GetCurrentCounterSamples()`

and `NetworkSpeedSample` / `SpeedSampleReady` / `GetCurrentSamples()` (TL-001 contracts)
are kept but remain unimplemented until TL-003.

Reasoning:
- Cumulative octet counters are the truthful, native data source and belong to TL-002.
- Rates are derived deltas over a polling window; mixing them into TL-002 forced
  premature rate logic and fabrication risks.
- Keeping both members leaves one stable contract for consumers (TL-003 just starts
  raising `SpeedSampleReady`).

## ADR-008: Use System.Net.NetworkInformation (IP Helper) for TL-002 collection

**Status:** Accepted (TL-002)

The collector uses the .NET managed wrapper over the IP Helper API
(`NetworkInterface.GetAllNetworkInterfaces`, `GetIPv4Statistics`,
`NetworkChange.NetworkAddressChanged`/`AvailabilityChanged`), polling at 1 s.

Reasoning:
- User-level reads; no admin/ETW/driver (see `docs/NETWORK_COLLECTION.md`).
- Native cumulative octet counters match `Get-NetAdapterStatistics` exactly
  (verified on real hardware, see `docs/NETWORK_COLLECTION.md`).
- Built into .NET — no new dependencies beyond `Microsoft.Extensions.Logging.Abstractions`
  and `Microsoft.Extensions.DependencyInjection.Abstractions`.
- Perf counters rejected (stale-prone, no benefit); ETW deferred to the per-process
  milestone (requires elevation).

## ADR-009: Aggregation excludes tunnels by default to avoid double-counting

**Status:** Accepted (TL-002)

`NetworkTrafficAggregator.GetNonOverlappingAdapters` defaults to excluding tunnel
adapters (WireGuard/OpenVPN-style) from the system total; physical adapters and
virtual nics are summed; down adapters are excluded; `includeTunnels: true` is the
explicit opt-in for VPN-only hosts.

Reasoning:
- Tunnels re-transmit the same payload already counted on the physical link; summing
  inflates numbers (double-counting).
- Excluding by default keeps the "system total" truthful for typical hosts.
- A VPN-only host has no honest non-overlapping total; returning empty is preferred
  to a fabricated one.

## ADR-010: Monotonic-clock delta rates; tunnels kept per-adapter, excluded from aggregate

**Status:** Accepted (TL-003)

Rates (`NetworkSpeedSample`) are computed as cumulative-counter deltas divided by
actual elapsed **monotonic** time (`Stopwatch`/QPC), not wall-clock and not the
assumed poll interval. `SpeedRateTracker` keeps one baseline per adapter with
re-baseline rules (first sample, counter decrease, zero elapsed, adapter
disappearance/replacement) so no fake spike is emitted.

Separately: **per-adapter samples always include tunnel/VPN traffic**; only the
system "Internet Total" aggregate may exclude tunnel interfaces by default
(extending ADR-009's non-overlapping policy to rate samples).

Reasoning:
- Ip Helper counters are cumulative; the truthful rate is `delta / realElapsed`.
  Assuming 1 s poll makes rates wrong under load/timer drift.
- Wall-clock is subject to jumps (NTP, sleep); QPC is monotonic.
- The golden rule ("never fabricate") forbids spike emissions on invalid
  transitions; re-baselining is the honest response.
- Adapter-level views describe each physical/VPN link truthfully; only the
  system-wide number needs the no-double-count caveat.

## ADR-011: Dashboard is a thin event-driven ViewModel over collector/provider abstractions

**Status:** Accepted (TL-005)

`DashboardViewModel` (App) consumes only `INetworkTrafficCollector` and
`INetworkAdapterProvider` (via DI), subscribes to `SpeedSampleReady` and the
adapter-changed events, and marshals every update to the WPF `Dispatcher`
(`InvokeAsync`) — no polling loop inside the ViewModel, no Windows networking
APIs in the view layer. Rates come exclusively from the existing
`NetworkTrafficAggregator.AggregateRates` policy (ADR-009/ADR-010): the three
prominent cards aggregate non-tunnel adapters, while each adapter row keeps its
own per-adapter rates including tunnels. Unit strings (B/s, KB/s, MB/s, Mbps)
are technical notation and are never translated; only number formatting (decimal
separator) follows the current culture via `DataRateFormatter` (Core), which is a
pure static formatter with no XAML dependencies.

Reasoning:
- Keeps UI binding, no code-behind networking (MVVM rule), no update storm
  (one sample per second, unchanged strings are not re-raised).
- The dashboard never fabricates: VPN-only hosts show honest zero/absent totals
  while tunnel rows still render their real per-adapter rates.
- Presentation formatting must be unit-testable without WPF, hence it lives in
  Core beside `DataRateConverter` rather than in a view converter.
- UI requirements: no unbounded history is accumulated (TL-006 owns the graph
  buffers); connection state (no network / disconnect / reconnect) is surfaced
  via `HasConnection` and localized status text, never exceptions.

## ADR-014: No automatic elevation; explicit Restart-as-Administrator only

**Status:** Accepted (TL-007, M4 UI)

Per-process ETW collection requires an elevated process (ADR-013), but the app
never elevates itself. On a non-elevated launch the process collector reports
`Status = PermissionDenied` + `LastError`, the Applications view shows an honest
status banner with a "Restart as Administrator" action, and the only elevation
path is that explicit user-invoked command: `Process.Start` with the `runas`
verb targeting a fresh instance, followed by `Application.Current.Shutdown()`
for the current instance. Any `Stopped`/`Failed` collector state is rendered as
`Start Monitoring` on the page (the UI re-invokes the collector, never UAC).

Reasoning:
- Auto-elevation on startup would trigger an unexpected UAC prompt and violate
  the "no automatic elevation" requirement (ADR-013).
- Banners + explicit actions keep failure modes visible and actionable without
  hiding the loss of per-process visibility.
- Restarting via `runas` is a shell decision on the user's machine; the app only
  launches the same executable and exits (no command-line flags needed).

## ADR-013: Per-process traffic via elevated real-time ETW kernel network events

**Status:** Accepted (TL-007)

Per-process download/upload bytes are collected from a **real-time ETW kernel
session** (`Microsoft-Windows-Kernel-Network`, `NetworkTCPIP` keyword) through
TraceEvent, in a background consume task, feeding a bounded accounting engine
that publishes ~1 s snapshots of per-instance samples
(`ProcessInstanceId = (pid, process start time)`).

Reasoning:
- ETW kernel-network is the only live, byte-accurate per-process stream on Windows;
  performance counters have no dependable per-process net counters, and socket/table
  lookups cannot attribute short-lived flows. ETW's cost is elevation, which is
  surfaced honestly (see privilege behavior below), never hidden.
- Attribution uses the **payload PID field** — TraceEvent's kernel parser fixes the
  header PID from the payload before dispatch, so DPC-completed receive events (header
  PID = System/Idle) are attributed to the true socket owner.
- **PID reuse** is handled by identity `(pid, start time)`: a reused PID with a
  different start time ends attribution to the old bucket and starts a fresh
  instance. Multiple instances of one executable stay distinct samples.
- Rates are monotonic-window deltas (ADR-010's principle applied per process); byte
  totals are the authoritative, testable invariant
  (`Total = Tcp + Udp = IPv4 + IPv6`).
- Nothing is fabricated or merged: unresolvable PIDs keep their own `<unknown>`
  bucket, and no event is ever re-assigned to a different process.
- Privilege behavior: non-elevated hosts get `Status = PermissionDenied` +
  `LastError`; the app never auto-elevates.
- Tunnel/VPN: per-process totals are owner-attributed application bytes and are
  intentionally not reconciled with the interface-level totals; the ADR-009/010
  aggregate policy is unchanged (no reassignment of tunnel transport bytes).
- Bounded resources (idle-prune 120 s, cap 4096 buckets, metadata revalidation 15 s)
  satisfy the no-unbounded-growth requirement.

## ADR-012: Native WPF graph rendering with a documented adaptive-scale hysteresis

**Status:** Accepted (TL-006)

The live traffic graph is rendered by a custom `FrameworkElement`
(`TrafficGraphControl`) that draws both series directly into the WPF
`DrawingContext` (`OnRender`) using two `StreamGeometry` polylines over a fixed
grid. No third-party chart library is used, and no per-sample UI element is ever
created: each poll updates a bounded in-memory sample list
(`TrafficSampleBuffer`, 5.5 min retention / 1320 samples) and invalidates the
framework element, which redraws from the full slice.

The shared Y-axis maximum (`AdaptiveGraphScale`) follows an explicit rule that is
unit-tested and documented here:

- **Immediate growth.** If the peak in the current window equals or exceeds the
  current maximum, the scale is raised immediately to a "nice" ceiling
  (k × 10^n with k ∈ {1, 2, 5}) that covers the peak. Sudden spikes always stay
  visible.
- **Hysteretic shrink.** The scale only lowers after peak traffic has stayed below
  35% of the current maximum for `ConsecutiveLowUpdatesRequired = 2` consecutive
  updates, then snaps to a nice ceiling covering `max(peak, floor)`. This
  prevents the constant up/down flicker a naive per-sample rescale would produce.
- **Floor.** The scale never drops below 2 KB/s (`FloorBytesPerSecond = 2048`),
  so an all-zero window still has a non-zero denominator (no divide-by-zero) and
  renders a flat baseline.

The scale is presentation-only — it never rounds or alters the measured sample
values (raw bytes/second are preserved in `TrafficGraphPoint`).

Reasoning:
- Native `OnRender` keeps the graph dependency-free, cheap to invalidate at
  1 sample/second, and consistent with the app's dark theme brushes; a third-party
  chart would add weight with no functional gain for a single two-series plot.
- The scale policy balances the competing requirements — spikes visible, no
  flicker, zero-safe — with an explicit, testable rule instead of ad-hoc logic.
- Timeline always draws oldest-left → newest-right (control forces LTR) so the
  graph remains readable under fa-IR RTL layouts.

## ADR-015: Collector StopAsync never captures the caller's SynchronizationContext

**Status:** Accepted (TL-007F)

`WindowsNetworkTrafficCollector.Dispose` runs `StopAsync().GetAwaiter().GetResult()`
and is invoked on the WPF dispatcher thread during `App.OnExit` → service-provider
disposal. If any awaited continuation inside `StopAsync` captures the ambient
`SynchronizationContext`, that continuation is posted back to the dispatcher
queue, which is blocked in `GetResult()` — the collector completes its loop but
`StopAsync` never resumes, `Dispose` never returns, and the process lingers
forever after a graceful window close (TL-007F).

Rule: every awaited continuation in a collector's `StopAsync`/`Dispose` path must
use `ConfigureAwait(false)` so shutdown is deterministic and independent of the
thread/context the collector is stopped from. The debugger proof: a live
non-elevated repro + `dotnet-dump` showed the sole foreground thread (main)
stuck in `WindowsNetworkTrafficCollector.Dispose` → `InternalWaitCore` →
`SpinThenBlockingWait` while `StopAsync` sat in `RunLoopAsync`'s completed task
wait; the regression test `Dispose_FromNonPumpingSyncContext_DoesNotDeadlock`
(thread with a non-pumping `SynchronizationContext`) reproduces the hang without
the fix and passes with it.

Reasoning:
- Deterministic cancellation/disposal over forced termination (`Environment.Exit`,
  `taskkill /F`) — the process exit is guaranteed by ordinary async/await
  semantics, no app-level kill is introduced.
- Affects both collectors (`WindowsEtwProcessTrafficCollector` already used
  `await lifetime.ConfigureAwait(false)`; `WindowsNetworkTrafficCollector` now
  does the same for its loop await).

## ADR-016: Active connections from IP Helper owner-PID tables (live-only, no fabricated peers)

**Status:** Accepted (TL-008)

Active TCP/UDP connections are enumerated directly from the Windows IP Helper
owner-PID tables via `GetExtendedTcpTable` (`TCP_TABLE_OWNER_PID_ALL`) and
`GetExtendedUdpTable` (`UDP_TABLE_OWNER_PID`) for IPv4 **and** IPv6, polled at
~1 s off the UI thread (`WindowsConnectionProvider`). This is a read-only,
**non-elevated** operation — unlike per-process byte accounting (ADR-013), the
connection list is owned by the user and needs no administrator rights.

Modeling rules:

- **Identity** is `ConnectionKey = (Protocol, AddressFamily, LocalAddress,
  LocalPort, RemoteAddress?, RemotePort?, ProcessId)`, so the ViewModel updates
  rows in place and only rebuilds when the key sequence changes (no flicker).
- **No fabricated peers.** UDP rows have no remote endpoint in the table, so the
  remote stays null and renders empty; TCP rows in a listening/unconnected state
  carry the native `0.0.0.0`/`::` + port 0 sentinel, which `EndpointFormatter`
  suppresses rather than showing a fake peer. `LISTEN` is never treated as outbound.
- **Endpoints are technical values** (raw IPs, ports, protocol/state names): they
  are always rendered LTR and untranslated, consistent with ADR-011; only the
  surrounding labels are localized.
- **Live-only.** The provider keeps the current snapshot only; there is no history,
  no persistence, and no reverse DNS in TL-008 (privacy: endpoint/process metadata
  only, never payloads/URLs/TLS).
- **Failure honesty.** A partial table failure is a warning and the successful
  tables are still published; a total failure keeps the last good snapshot and sets
  `LastError`, which any subsequent success clears — never a silent empty list.
- **Process attribution** reuses `IProcessMetadataProvider` through a bounded
  resolver cache (TTL 3 s, capacity 512, FIFO, negative caching) keyed by the same
  `(pid, start time)` identity from TL-007, so PID reuse cannot mis-attribute.

Reasoning:
- IP Helper tables are the native, stable, dependency-free source and were already
  proven for interface counters (ADR-008); no packet capture / WFP / WinDivert /
  driver is introduced.
- Verified against `netstat -ano` (the same MIB owner-PID data): TCP state
  histogram and UDP count matched; `Get-NetTCPConnection` reports additional
  `Bound` rows that are **not** in the owner-PID table, so we intentionally match
  the table rather than the cmdlet.
- Keeping selection/sort/format logic pure in Core (and the ViewModel a thin,
  dispatcher-marshalled consumer) makes the behavior testable without a live table
  or the UI.

