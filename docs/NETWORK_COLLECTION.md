# TrafficLens — Network Collection

Status: **Research phase** (updated during TL-001, mechanism selected in TL-002).

## Goal

Collect, at minimum:

- Per-interface download/upload byte counters
- Per-process download/upload bytes
- Active TCP/UDP connections with owning process

## Candidate mechanisms (Windows)

| Mechanism | Interfaces/global speed | Per-process bytes | Connections | Notes |
|---|---|---|---|---|
| Windows Performance Counters (`\Network Interface(*)`, `\TCP\...`) | Good (per-interface) | No (per-process net counters are unreliable/absent) | No | Needs no extra privilege for reads; counters can be stale |
| IP Helper API (`GetIfTable2`, `GetExtendedTcpTable`) | Yes (per-interface octets) | No | Yes (TCP/UDP + PID) | Native, stable, no admin required for read |
| ETW (Microsoft-Windows-Kernel-Network) | No | Yes (per-process bytes) | Partial | Requires tracing session + admin/elevation; filter/timing work; most accurate per-process source |
| GetExtendedUdpTable / TCP (Async) | — | — | Yes | Supplement for UDP/established states |

## Selection considerations (pending decision in TL-002)

Accuracy requirements:
- Global totals must be truthful — derived from adapter byte counters, never fabricated.
- Per-process accounting candidates ranked by accuracy.

Required privileges:
- Perf counters and IP Helper: user-level reads.
- ETW: may require elevation; document how the app behaves when unavailable.

Performance & compatibility:
- Prefer low-frequency polling of monotonic counters over high-rate sampling.
- Must handle adapter disconnect, VPN connect/disconnect, PID reuse.

## Decision

To be recorded here after TL-002 research completes. Current open question:
whether per-process bytes come from ETW or from IP Helper-estimated deltas,
and how unclaimed traffic is reported.

## Golden rule

Never fake measurements. If a measurement is unavailable, show unavailable.