# TrafficLens — Project Status

Updated: 2026-09-14

## Current Milestone

M1 — Global network monitoring (TL-002 done, TL-003 pending).

## Task IDs

- TL-001 Project Bootstrap — **DONE**
- TL-002 Global Network Collector — **DONE**
- TL-003 Download/Upload Calculation — not started

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

## Verified

- `dotnet build TrafficLens.sln`: **Success, 0 warnings, 0 errors** (Debug and Release).
- **Automated tests:** 36/36 passed (`TrafficLens.Network.Tests`) —
  kind mapping, filter, default adapter selection, aggregation, collector
  (start/counters/events/reset/stop).
- **Real Windows verification:** running the collector under live traffic produced
  cumulative counters that track native `Get-NetAdapterStatistics`:
  - Collector (2026-09-14T15:39:00Z): Wi-Fi received `487,070,373`, sent `80,790,175`.
  - Native (same minute): Wi-Fi received `487,832,221`, sent `81,740,084`.
  - Counters grow consistently between the two captures (traffic was generated in
    between); offline vNIC/TAP adapters report 0 in both.
- Default adapter = Wi-Fi (up + gateway, non-tunnel), matching Windows routing.

## Build

- .NET 8 SDK 8.0.425 at `C:\dotnet`.
- Command: `C:\dotnet\dotnet.exe build TrafficLens.sln`

## Tests

- `tests/TrafficLens.Network.Tests` — xUnit, 36 tests, all passing.
- `tests/TrafficLens.Network.Verification` — console harness; run with
  `dotnet run --project tests/TrafficLens.Network.Verification`.

## Known Issues / Not Started

- Rates (download/upload per second) intentionally not computed — TL-003.
- `SpeedSampleReady` is declared but not raised until TL-003 (suppressed CS0067).
- Per-process bytes (ETW/perf) and connections are later milestones.
- TAP/DCO VPN adapters enumerate with kind `Unknown` when their interface type is
  not Ethernet/Wireless; they are down in this environment so this does not affect
  totals.

## Git Commit

TL-002 commit: `see git log` (recorded in TASKS.md).

## Next Recommended Task

- TL-003 — Download/Upload Calculation (compute `NetworkSpeedSample` rates from
  `NetworkCounterSample` deltas; raise `SpeedSampleReady`).