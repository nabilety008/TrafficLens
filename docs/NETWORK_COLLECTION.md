# TrafficLens — Network Collection

Status: **Implemented for TL-002 scope** (updated 2026-09-14).

## Goal

Collect, at minimum:

- Per-interface download/upload byte counters (TL-002: cumulative; TL-003: rates)
- Per-process download/upload bytes (later milestone)
- Active TCP/UDP connections with owning process (later milestone)

## Candidate mechanisms (Windows)

| Mechanism | Interfaces/global speed | Per-process bytes | Connections | Notes |
|---|---|---|---|---|
| Windows Performance Counters (`\Network Interface(*)`, `\TCP\...`) | Good (per-interface) | No (per-process net counters are unreliable/absent) | No | Needs no extra privilege for reads; counters can be stale |
| IP Helper API (`GetIfTable2`, `GetExtendedTcpTable`) | Yes (per-interface octets) | No | Yes (TCP/UDP + PID) | Native, stable, no admin required for read |
| ETW (Microsoft-Windows-Kernel-Network) | No | Yes (per-process bytes) | Partial | Requires tracing session + admin/elevation; filter/timing work; most accurate per-process source |
| GetExtendedUdpTable / TCP (Async) | — | — | Yes | Supplement for UDP/established states |

## Decision (TL-002)

**Mechanism:** `System.Net.NetworkInformation` — the .NET managed wrapper over the
Windows IP Helper API (`GetIfTable2`, `GetIfEntry2`). Polling at 1 s default.

Why this over the alternatives for TL-002:

- **Truly user-level.** No admin rights, no ETW session, no elevation, no driver.
  `Get-NetAdapterStatistics` and our collector agree from the same user context.
- **Per-interface cumulative octet counters** are exposed directly as
  `IPv4InterfaceStatistics` (`BytesReceived`/`BytesSent`), the exact data TL-002
  needs.
- **Adapter connect/disconnect is observable** via `NetworkChange.NetworkAddressChanged`
  events, so no busy polling of interface presence is required.
- Performance counters were rejected because they're stale-prone and unnecessary for
  an app that already owns its polling loop; ETW is rejected now because it needs
  elevation and belongs to the per-process milestone.

Scope boundary: TL-002 intentionally collects **cumulative counters only**.
Download/upload **rates** (`NetworkSpeedSample`, `SpeedSampleReady`) are derived in
TL-003 from counter deltas and are out of scope here (ADR-007).

## What is collected

- Each monitored adapter (Ethernet, Wireless, Tunnel, Virtual) that is **up**:
  `AdapterId`, `AdapterName`, `ReceivedBytes`, `SentBytes`, `Timestamp`
  (`NetworkCounterSample`), emitted on `CounterSampleReady` each poll.
- Adapter list (`NetworkAdapterInfo` with **kind** and **default** flags) via
  `INetworkAdapterProvider.GetAdapters()`.
- Default adapter = first **up** adapter with a gateway, preferring non-tunnel;
  never "the first adapter in the OS list".

## Accuracy and limitations

- Counters are the OS's own cumulative octet counters (IP Helper) — they are truthful,
  not synthesized. Verified against `Get-NetAdapterStatistics` (see below).
- The 1 s polling cadence is a **point sample** of cumulative counters; rate accuracy
  is determined in TL-003 (deltas over the polling interval).
- **Counter reset/wrap:** IP Helper counters are unsigned but can be zeroed by the OS
  (reset, rebaseline). The collector detects a decrease against the previous sample,
  logs a warning, and re-baselines. No fabricated numbers are produced.
- **Adapter set changes** (connect/disconnect) raise `NetworkChanged` and are also
  detected by diffing the adapter ID set between polls, so applications relying on the
  event still observe changes if the OS event is missed.
- TAP/DCO VPN driver adapters may report an `Unknown` interface type (not
  Ethernet/Wireless/Tunnel) in some environments; they are filtered out only when
  down; when up they would currently be classified `Unknown`. If needed, TL-004 can
  classify them by description/textual heuristics.
- Link speed comes from `NetworkInterface.Speed`, which can be `-1`/`null` on some
  drivers.

## Aggregation policy (system totals, double-counting)

`NetworkTrafficAggregator`:
- **Independent counter sources MAY be summed**: physical Ethernet/Wireless and
  virtual nics (Hyper-V/VMware-style) are treated as independent.
- **Tunnel adapters are excluded from the default "system total"** (WireGuard,
  OpenVPN-style) because they carry the same payload already counted on the physical
  link, so summing would over-count.
- Adapters that are **down** are excluded.
- If the only active connection is a tunnel (VPN-only laptop), the default total is
  **empty — the honest result** rather than a fabricated number. Callers that want
  tunnel-framed traffic must opt in with `includeTunnels: true`.

## Privileges

None beyond a normal user. Enumeration, counters, and change events are all
user-level reads. The app never requests admin and never captures packet payloads.

## Verification evidence (2026-09-14)

Live collector run while generating traffic, Wi-Fi adapter (the default):

| Source | ReceivedBytes | SentBytes | Timestamp |
|---|---|---|---|
| TrafficLens collector (verification console) | 487,070,373 | 80,790,175 | 15:39:00Z |
| Native `Get-NetAdapterStatistics` | 487,832,221 | 81,740,084 | ~15:39:00Z |

The two samples are taken seconds apart (traffic flowed in between), so the native
value being slightly higher is expected; totals/ordering/adapters match. Offline
vNIC/TAP adapters report 0 in both sources.

Run it yourself:

```
dotnet run --project tests/TrafficLens.Network.Verification
```

Environment: `TL_VERIFY_SECONDS=7` (default 6), poll 500 ms.

## Golden rule

Never fake measurements. If a measurement is unavailable, show unavailable.