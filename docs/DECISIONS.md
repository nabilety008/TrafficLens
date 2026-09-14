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