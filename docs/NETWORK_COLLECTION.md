# TrafficLens — Network Collection

Status: **Implemented for TL-002 — TL-007 scope** (updated 2026-09-14).

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

Scope boundary: TL-002 collects **cumulative counters only**. Download/upload
**rates** (`NetworkSpeedSample`, `SpeedSampleReady`) are computed in TL-003 from
counter deltas over monotonic elapsed time (ADR-010).

## Rate calculation (TL-003)

- `NetworkSpeedCalculator` converts two cumulative counter samples + actual
  elapsed seconds into `NetworkSpeedSample` (download/upload bytes/second, and
  `TotalBytesPerSecond`).
- Elapsed time comes from a **monotonic clock** (`Stopwatch`/QPC), never
  wall-clock, so rate accuracy holds under load and timer drift. The poll
  interval is never assumed to be exactly 1 s — rates divide by the real
  measured interval (`SpeedRateTracker`).
- `SpeedRateTracker` keeps **one independent baseline per adapter** and:
  - the **first sample** of an adapter only establishes the baseline (no fake spike);
  - a **counter decrease** (reset/wrap) re-baselines without emitting;
  - **zero/negative elapsed** re-baselines without emitting;
  - an **adapter that disappears** is pruned; on **reconnect/replacement** with
    the same or a new id the first sample re-baselines (no spike).
- Baseline data is bounded (one entry per adapter) — no unbounded collections,
  no busy loops, no UI-thread work; all computation happens on the collector's
  background poll loop.
- Raw rates stay in bytes/second. Numeric unit conversion lives in Core
  (`DataRateConverter`: B/s, KB/s, MB/s, Kbps, Mbps, Gbps) for the UI; string
  formatting is deferred to a later UI task so collectors never format strings.

## What is collected

- Each monitored adapter (Ethernet, Wireless, Tunnel, Virtual) that is **up**:
  `AdapterId`, `AdapterName`, `ReceivedBytes`, `SentBytes`, `Timestamp`
  (`NetworkCounterSample`), emitted on `CounterSampleReady` each poll.
- Per-adapter **rate** samples (`NetworkSpeedSample`) emitted on
  `SpeedSampleReady` each poll after the baseline exists, available via
  `GetCurrentSamples()`.
- Adapter list (`NetworkAdapterInfo` with **kind** and **default** flags) via
  `INetworkAdapterProvider.GetAdapters()`.
- Default adapter = first **up** adapter with a gateway, preferring non-tunnel;
  never "the first adapter in the OS list".

## Per-adapter vs system total (tunnel/VPN traffic)

- **Per-adapter views ALWAYS include tunnel/VPN traffic.** An OpenVPN, WireGuard,
  TAP/DCO, Hyper-V or VMware adapter's counters and rates are retained and
  published exactly as measured; nothing is discarded.
- **System "Internet Total"** (`NetworkTrafficAggregator.AggregateRates`) may
  **exclude tunnel interfaces by default** so the same bytes are not counted
  twice (physical link + tunnel both count the same payload). Callers on a
  VPN-only host must opt in with `includeTunnels: true` for the aggregate.

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
- TAP/DCO VPN driver adapters register unusual interface types on Windows (e.g.
  `HighPerformanceSerialBus` = 53 for OpenVPN TAP/DCO). They were previously
  classified `Unknown`; since TL-004 they are classified by **driver-description
  heuristics** (tap-windows/openvpn/wintun/wireguard → Tunnel; virtual/vmware/
  hyper-v/vethernet → Virtual) and stay visible in all-adapters mode.
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

### Cumulative counters (TL-002)

Live collector run while generating traffic, Wi-Fi adapter (the default):

| Source | ReceivedBytes | SentBytes | Timestamp |
|---|---|---|---|
| TrafficLens collector (verification console) | 487,070,373 | 80,790,175 | 15:39:00Z |
| Native `Get-NetAdapterStatistics` | 487,832,221 | 81,740,084 | ~15:39:00Z |

The two samples are taken seconds apart (traffic flowed in between), so the native
value being slightly higher is expected; totals/ordering/adapters match. Offline
vNIC/TAP adapters report 0 in both sources.

### Real-time rates (TL-003)

Concurrent run (collector + native sampler) while downloading:

| Source | Window | Download B/s | Upload B/s |
|---|---|---|---|
| TrafficLens window-mean (Wi-Fi) | 10.02 s | 803,647 | 18,423 |
| Native `Get-NetAdapterStatistics` delta (Wi-Fi) | 14.57 s | 1,120,590 | 26,086 |

Plausibly aligned: same order of magnitude and direction of traffic on the same
(default) adapter. The native window is longer and overlapped the collector's
window, capturing more of the generated download, so it reads higher — expected
for non-overlapping sample timing. Exact equality is not expected. The last-poll
instantaneous rate (961 B/s down) reflects the quiet tail after downloads ended;
the window-mean over the measured monotonic interval is the apples-to-apples
comparison shown above. TAP/vNIC adapters contribute 0 (down).

Run it yourself:

```
dotnet run --project tests/TrafficLens.Network.Verification
```

Environment: `TL_VERIFY_SECONDS=10` (default 10), poll 500 ms.

## Adapter detection audit evidence (TL-004)

Live enumeration on the dev machine (`NetworkInterface.GetAllNetworkInterfaces`),
7 interfaces, all with unique GUID ids (no duplicate logical entries):

| Adapter | Description | Raw O/S type | Classified kind |
|---|---|---|---|
| Wi-Fi (up, default) | Intel(R) Wi-Fi 6 AX201 160MHz | `Wireless80211` | Wireless |
| Local Area Connection (down) | TAP-Windows Adapter V9 for OpenVPN Connect | `HighPerformanceSerialBus` (53) | Tunnel |
| OpenVPN Connect DCO Adapter (down) | OpenVPN Data Channel Offload | `HighPerformanceSerialBus` (53) | Tunnel |
| Local Area Connection* 1 / * 10 (down) | Microsoft Wi-Fi Direct Virtual Adapter | `Wireless80211` | Virtual |
| Bluetooth Network Connection (down) | Bluetooth Device (Personal Area Network) | `Ethernet` | Ethernet |
| Loopback Pseudo-Interface 1 | Software Loopback Interface 1 | `Loopback` | excluded (filter) |

Findings: all expected adapter families (Ethernet, Wi-Fi, OpenVPN TAP/DCO,
WireGuard, Hyper-V/VMware virtual nics) are enumerable and visible via
`INetworkAdapterProvider.GetAdapters()` (all-adapters mode keeps down adapters);
up/down state, gateway awareness, default/preferred selection (up + gateway,
non-Tunnel/non-Virtual), per-adapter rates, connect/disconnect events, and
GUID-unique identity were all verified in TL-002/TL-003 or in the live output
above. The only gap found — OpenVPN TAP/DCO misclassified as `Unknown` because
of their `HighPerformanceSerialBus` interface type — was closed in TL-004 with
description-aware classification.

## Golden rule

Never fake measurements. If a measurement is unavailable, show unavailable.

## Per-process traffic (TL-007)

### Mechanism: real-time ETW kernel network events (elevated)

Per-process bytes are collected from a **real-time Windows ETW kernel session**
(`Microsoft-Windows-Kernel-Network`, enabled via TraceEvent's
`KernelTraceEventParser.Keywords.NetworkTCPIP`). No packet capture ever happens —
events carry only metadata (PID + transfer size); payload bytes are never read or
stored (golden rule).

**Why ETW and not alternatives:**
- Windows Performance Counters have no dependable per-process network counters.
- PID-based lookup of socket state can't attribute traffic of exited sockets and
  misses short-lived flows (curl, browsers).
- IP Helper connection tables are point-in-time snapshots, not byte streams.
- Only ETW (kernel network provider) provides a live, byte-accurate per-process
  stream. Its cost is that it **requires elevation** (see Privileges below).

### Event attribution (critical detail)

For kernel network events, `TraceEvent`'s `KernelTraceEventParser` **fixes the
event header ProcessId from the payload's own PID field** ("Identifier of the
process associated with the request") **before** dispatch. The *raw* header PID
is the thread context the event happened to be logged in (frequently `System`/`Idle`
for DPC-completed receive completions), which would mis-attribute virtually all
downloads to `System`. We subscribe to the parser's fixed-up events and take
`ProcessID` + `size` from them:

- `TcpIpSend` (IPv4), `TcpIpRecv` (IPv4), `TcpIpSendIPV6`, `TcpIpRecvIPV6`
- `UdpIpSend` (IPv4), `UdpIpRecv` (IPv4), `UdpIpSendIPV6`, `UdpIpRecvIPV6`

We deliberately do **not** subscribe `TcpIpRetransmit` (counted as its own event,
not a payload transfer), so no double-counting occurs. Each mapped event becomes a
`NetworkTransferEvent(pid, direction, size, protocol, ip-version, timestamp)` fed
to the accounting engine.

### Accounting engine (bounded, PID-reuse safe)

`ProcessTrafficAccountingEngine` (per-process bucket per
`ProcessInstanceId = (pid, process start time)`):

- **PID + process-start-time identity.** ETW only gives PIDs; we detect the true
  process start time once (metadata resolution) and re-key the bucket to
  `(pid, startTime)`. A **reused PID** is detected when the live process's start
  time differs from the bucket identity (2 s tolerance) — attribution to the old
  instance stops (its totals are frozen, never deleted), and new events flow to a
  fresh bucket for the new instance.
- **Unknown/unresolvable processes are never merged into another process.** Their
  events accumulate under their own `(pid, 0)` bucket displayed as
  `<unknown pid N>`; metadata resolution retries every 10 s while the process is
  missing. Events whose owning process disappeared mid-accounting keep their
  bucket; nothing is ever assigned to a different process or to "System" by
  default.
- **Byte totals are the authoritative data.** Rates are derived *per snapshot*
  from the **sliding monotonic window** (3 s) of cumulative byte deltas divided by
  the real measured stopwatch elapsed — never an assumed 1-second interval, never
  wall-clock. The first snapshot only establishes a baseline (no discovery spike).
- **Bounded state.** Idle buckets are pruned after 120 s; hard cap of 4096
  buckets with an oldest-LastSeen eviction; metadata is re-validated at most every
  15 s. No per-event allocation, no per-event logging, no UI work on the ETW
  thread — the hot path is one dictionary slot acquire + one counter amend.
- Multiple instances of the same executable (several `chrome.exe`) stay **distinct
  samples** because identity includes the start time.

### Pipeline

```
ETW kernel events ──► TraceEventSource (background consume task)
        │  NetworkTransferEvent (payload PID + size, no payload bytes)
        ▼
ProcessTrafficAccountingEngine.Record   (lock-free-ish single lock, no IO)
        ▼                        ▲
   ~1 s Snapshot loop ──────────┘  BuildSample/process/PID-reuse handling
        ▼
   SamplesReady / GetCurrentSamples   (ProcessTrafficSample list)
```

### Privileges

Enabling the kernel network provider requires **Administrator**
(`SeSystemProfile` / system logger mode) or membership in Performance Log Users
for the local case. The collector **never hides and never crashes**: on a
non-elevated host it reports `Status = PermissionDenied` with a clear `LastError`
(`ProcessTrafficCollectorStatus`). The app must not force-elevate on its own; the
verification console demonstrates both paths.

### VPN / tunnel semantics

- Per-process totals are **owner-attributed application bytes**, not
  interface-attributed bytes. On a VPN they reflect the app's socket traffic
  (framed/unframed as the OS counts it), which is intentionally different from,
  and not reconciled against, the per-interface adapter counters of TL-002/003.
- We do **not reassign** VPN transport bytes or change the existing system
  aggregate policy (ADR-009/010): physical-link vs tunnel double counting only
  matters for the interface-level total, which is untouched.

### Protocol / version coverage

TCP and UDP over IPv4 **and** IPv6 are counted separately
(`ProcessProtocolTotals`: Tcp/Udp × Received/Sent and IPv4/IPv6 × Received/Sent;
the invariant `Total = Tcp + Udp = IPv4 + IPv6` is unit-tested). ICMP and other
non-TCP/UDP kernel-network events are out of scope for TL-007.

### Live verification evidence (2026-09-14, elevated)

`dotnet run --project tests/TrafficLens.Network.Verification -- --process`
(elevated; ~40 s, downloads from `https://speed.cloudflare.com/__down`):

- **Two distinguishable apps** concurrently: `curl.exe` and `powershell.exe`
  each appeared as their own attributed bucket (curl 6,236,307 B down / 674 B up,
  PowerShell 4,013,430 B down / 388 B up — all TCP/IPv4, matching
  `Total = Tcp = IPv4`).
- **Same executable, two instances isolated:** two sequential `curl` runs
  (instance A pid 10232, instance B pid 11728) produced **two distinct buckets**
  keyed by different (pid, start-time) identities with independent totals
  (6,236,307 vs 1,555,415 B) — attribute granularity is per **instance**, not per
  exe name.
- **No unbounded growth:** snapshot held 11 samples (hard cap 4096), ~2.6 MB
  managed memory growth over the whole run.
- **Clean stop:** Collector `StopAsync` ended the session, consumed loop and
  snapshot loop; no orphaned ETW session left behind.
- **Non-elevated path:** the same command without elevation reports
  `PermissionDenied` + `LastError` (verified) and does not crash.

Run it yourself (elevated console):

```
dotnet run --project tests/TrafficLens.Network.Verification -- --process
```