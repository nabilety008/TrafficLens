# TrafficLens — Performance

> **Related docs:** `docs/PERFORMANCE_BASELINE.md` (pre-TL-017 numbers) and
> `docs/PERFORMANCE_AFTER.md` (TL-017 authoritative BEFORE vs AFTER report —
> connections optimization, per-tick allocations, lazy startup, SQLite history,
> idle tray, and long-run soak results).

This document captures the idle-CPU optimization performed in TL-014, the
measurement methodology behind it, and the resulting evidence. It exists so
future work can (a) reproduce the measurements and (b) avoid regressing the
idle-CPU guarantees.

## Guarantees

When the app is idle (no active network traffic, no user interaction, window
visible or hidden to tray), repeated 30-second measurements must show:

- **max single-core CPU ≤ 15%** per 30-second window (harness F2),
- **no periodic spikes** — a flat, near-zero idle profile is the goal.

Achieved post-fix: **avg 3.43%, max 9.88%** over a 30-minute soak (the
remaining work is one WPF Arrange pass per second and negligible background
tasks).

## Measurement methodology

### Harness scope

`scripts/tl014-stability.ps1` exercises a Release build on the real host:

- **F1** warm-up + idle 60 s — dashboard/connections/history/GC-count bounds.
- **F2** 30-min idle soak — CPU is sampled per second and folded into 30-second
  windows; every window's max must stay ≤ 15% of one core.
- **F3** 10-min memory soak — WorkingSet and PrivateMemorySize drift bounds.
- **F4** interaction stress — 50 navigation, 25 widget toggles, 25 window
  show/hide, 10 restart cycles.

### CPU attribution (profiling)

`dotnet-trace` is used to capture managed + native stacks as Speedscope JSON:

```text
dotnet-trace collect -p <pid> -o out.speedscope.json --duration 1:00 --format Speedscope
```

The Speedscope "Left Heavy" view with "Time (Wall Clock)" attributed to the
WPF Dispatcher thread shows exactly where idle CPU goes. Sampling
concentrations above ~2% of samples on one thread indicate sustained work.

### Windowed sampling script

For the harness itself, per-second CPU is captured with
`Get-Process`/`Get-Counter`-free sampling over `Process.TotalProcessorTime`
deltas, folded into 30-second windows. The CPU spike timing script lives below.

## Root causes found (TL-014)

### 1. DashboardViewModel event-per-adapter dispatch storm

`SpeedSampleReady` fires once per adapter per poll (~1 s). With N adapters
(Wi-Fi + Bluetooth + OpenVPN TAP + OpenVPN DCO + Wi-Fi Direct = 5 on the
reference host) `OnSpeedSample` ran `RefreshRates` N×/s. Each call produced a
WPF layout pass, N `INotifyPropertyChanged` notifications, and an extra
graph-buffer append — for scalar data that changes at most once per second.

Observed before the fix: WPF Dispatcher thread ~50% on-CPU (22,449 samples in
45 s), concentrated in `RefreshRates`/measure/arrange.

Fix: `_refreshPending` flag + `CoalesceRefresh()` in `DashboardViewModel`.
The first event in a second sets the flag and schedules one
`Dispatcher.BeginInvoke`; every subsequent event in the same second is dropped.
One layout pass per second, regardless of adapter count.

### 2. ConnectionsViewModel always-on dispatch

`OnConnectionsChanged` dispatched to the UI thread every ~1 s poll and rebuilt
row view models + resolved icons even when the Connections page was invisible
(the Dashboard is the default page).

Fix: `_isActive` flag + `SetActive(bool)`, driven by
`MainViewModel.SelectPage`. Inactive state buffers `_pendingConnections`
without dispatching; activation triggers an immediate refresh from the
provider cache.

### 3. WindowsConnectionProvider native table enumeration

`RunLoopAsync` called `EnumerateOnce()` unconditionally every ~1 s: four
native P/Invokes (`GetExtendedTcpTable` × 2 + `GetExtendedUdpTable` × 2) plus
process resolution for ~159 connections — all while no page read the data.

Fix: `IConnectionProvider.SetPollingEnabled(bool)` +
`WindowsConnectionProvider.SetPollingEnabled`. When polling is paused,
`RunLoopAsync` skips `EnumerateOnce()`; `GetCurrentConnections()` keeps
returning the last good snapshot on activation.

### 4. Default page not deactivated

The `MainViewModel` constructor now calls `Connections.SetActive(false)` so
the hidden-by-default Connections page never starts active.

## Evidence

| Metric | Pre-fix | Post-fix |
|---|---|---|
| F2 soak 30 min, max CPU in 30 s windows | ~20%+ periodic peaks (FAIL) | **9.88%** (avg 3.43%) — PASS |
| Main thread idle on-CPU | 22,449 samples / 45 s (~50%) | 113 samples / 60 s (~1.9%) |
| Layout passes per second (5 adapters) | ~5 | 1 |

Traces: `%TEMP%\opencode\tl-before.speedscope.json` (pre),
`%TEMP%\opencode\tl-fixed.speedscope.json` (post).
Full harness log: `%TEMP%\opencode\tl014-v2.log`.

## Regression notes

- Coalescing is lossless: all events arrive in the same poll second and feed
  the same aggregate value; merging them only removes redundant work.
- Polling pause loses **nothing** for the user — connection data is live-only;
  the page refreshes from the provider cache on activation.
- `FakeConnectionProvider` stubs `SetPollingEnabled` as a no-op; all 397
  tests (App 145 / Network 212 / Infrastructure 40) pass with the fixes.