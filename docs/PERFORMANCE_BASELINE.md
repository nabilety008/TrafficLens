# TrafficLens — TL-017 Performance Baseline

> **Branch:** `feature/tl017-performance` | **Base:** master v0.1.1 (`1bcc9c9`) | **Measured:** 2026-09-18

## Environment

| Item | Value |
|------|-------|
| App version | 0.1.1 |
| .NET runtime | 8.0.425 |
| Published exe | `artifacts/publish/win-x64/TrafficLens.exe` |
| Elevation | **Non-elevated** (typical tray usage) |
| ETW sessions | 0 (requires elevation) |
| Test settings | en-US, MinimizeToTray True, CloseToTray True, FloatingWidgetEnabled False |

## Methodology

| Phase | How | Samples |
|-------|-----|---------|
| Cold start | Kill all → 3 s settle → `Start-Process`, measure time to visible main window via `User32.EnumWindows` + `IsWindowVisible` | 1 run |
| Warm start | Graceful exit → relaunch within 2 s | 1 run |
| Scenario soak | 15 s intervals; WS/Private via `Process.WorkingSet64`, CPU% via `TotalProcessorTime` delta | 20 samples/run (idle), 12/run (scenarios) |
| GC / counters | `dotnet-counters collect --counters System.Runtime`, 20 s, format csv | 2060 raw samples → 77 counter rows |
| ETW active | *Deferred* — UAC prompt not automated; app ETW session only activates when app is elevated |

---

## Startup

| Run | Time to main window |
|-----|---------------------|
| Cold | **1301 ms** |
| Warm | **1339 ms** |

*Note: WPF application requires JIT + assembly load on first run; warm reuses cached app domain, difference minimal given single-file published layout.*

---

## Idle (tray-hidden) — 5-minute soak

| Metric | First sample | Last sample | Δ | Avg | Min | Max |
|--------|-------------|-------------|---|-----|-----|-----|
| WS MB | 302.85 | 307.97 | +5.12 | 306.7 | 302.85 | 310.37 |
| Private MB | 164.72 | 170.81 | +6.09 | 169.4 | 164.72 | 174.14 |
| Threads | 28 | 23 | −5 | 22.7 | 21 | 28 |
| Handles | 683 | 682 | −1 | 677.1 | 673 | 683 |
| CPU% | 0% | 1.87% | — | **2.19%** | 0% | 4.95% |

---

## Dashboard visible — 3-minute soak

| Metric | Avg | Min | Max |
|--------|-----|-----|-----|
| WS MB | 309.33 | 299.42 | 316.19 |
| Private MB | 170.37 | 161.65 | 177.50 |
| Threads | 22.08 | 19 | 27 |
| Handles | 663.17 | 658 | 668 |
| CPU% | **2.37%** | 0% | 4.37% |

---

## Applications page — 3-minute soak

| Metric | Avg | Min | Max |
|--------|-----|-----|-----|
| WS MB | 304.59 | 300.95 | 306.52 |
| Private MB | 165.20 | 162.74 | 167.02 |
| Threads | 23 | 21 | 27 |
| Handles | 680.25 | 673 | 687 |
| CPU% | **2.06%** | 0% | 4.37% |

---

## Connections page — 3-minute soak

| Metric | Avg | Min | Max |
|--------|-----|-----|-----|
| WS MB | **350.95** | 333.27 | 363.24 |
| Private MB | **205.56** | 188.61 | 217.36 |
| Threads | 22.58 | 21 | 24 |
| Handles | 740.92 | 737 | 745 |
| CPU% | **7.64%** | 0% | 10.51% |

*Note: Connections page is the most expensive view — real-time per-connection counter polling and UI rendering drive the elevated memory and CPU profile.*

---

## Active traffic (dashboard + Cloudflare download) — 60 seconds

| Metric | Avg | Min | Max |
|--------|-----|-----|-----|
| WS MB | 303.85 | 299.70 | 310.50 |
| Private MB | 165.29 | 161.63 | 171.55 |
| Threads | 24.83 | 21 | 28 |
| Handles | 667.5 | 660 | 673 |
| CPU% | **3.28%** | 0% | 5.15% |

---

## Post-traffic settle (30 s after traffic stopped)

| WS MB | Private MB | Threads | Handles | CPU% |
|-------|------------|---------|---------|------|
| 310.52 | 171.56 | 21 | 660 | **2.31%** |

---

## GC / Counter Summary (20-second capture, idle)

| Counter | Avg | Min | Max |
|---------|-----|-----|-----|
| GC Heap Size (MB) | 8.13 | 3.99 | 12.29 |
| Gen 0 GC Count (/s) | 0.05 | 0 | 1 |
| Gen 1 GC Count (/s) | 0.013 | 0 | 1 |
| Gen 2 GC Count (/s) | 0.013 | 0 | 1 |
| % Time in GC | 0% | 0% | 0% |
| GC pause time (ms/s) | 0.46 | 0 | 22.70 |
| Gen 0 size (MB avg) | 2.75 | 1.12 | 6.29 |
| Gen 1 size (MB avg) | 1.35 | 0.14 | 2.97 |
| Gen 2 size (MB avg) | 4.04 | 2.57 | 5.06 |
| LOH (KB) | 304.9 | 304.9 | 304.9 |
| POH (KB) | 151.3 | 151.3 | 151.3 |
| GC Fragmentation % | 29.09% | 16.20% | 51.66% |
| ThreadPool threads | 2.63 | 2 | 4 |
| Active Timers | 3.97 | 3 | 4 |
| Monitor Lock Contention (/s) | 0.066 | 0 | 1 |
| JIT time (ms/s) | 35.37 | 0 | 514.60 |
| Exception Count (/s) | 0 | 0 | 0 |

*GC fragmentation (29% avg) is elevated but benign — gen2 at 4 MB avg with frequent minor GC; heap compact not triggered under idle.*

---

## ETW active — Deferred

| Item | Status |
|------|--------|
| ETW session observed | Requires app running elevated |
| UAC automation | Not available in this environment |
| Measurement plan | Phase 6 long-run soak — run elevated app, capture CPU under traffic via `dotnet-counters` + `logman query -ets` from the same elevated helper process |
| Prior context | TL-007 (v0.1.0 verification) confirmed TrafficLens ETW trace provider registers when app runs elevated; CPU overhead expected 5–10% at sustained 1 Gbps traffic |

---

## Raw Data

All CSVs and JSON artifacts retained (not gitignored in branch only):

```
artifacts/tl017-baseline/
  20260917-104924/startup.csv        # startup cold/warm timing
  20260917-104924/metrics.csv        # all scenario metrics
  _gc.csv                            # full dotnet-counters System.Runtime capture
```

---

## Summary — Baseline Numbers

| Metric | Idle | Dashboard | Applications | Connections | Traffic |
|--------|------|-----------|--------------|-------------|---------|
| WS (MB) | 306.7 | 309.3 | 304.6 | **350.9** | 303.8 |
| Private (MB) | 169.4 | 170.4 | 165.2 | **205.6** | 165.3 |
| CPU% | 2.19 | 2.37 | 2.06 | **7.64** | 3.28 |
| Threads | 22.7 | 22.1 | 23.0 | 22.6 | 24.8 |
| Handles | 677 | 663 | 680 | **741** | 668 |
| Startup cold | 1301 ms | — | — | — | — |
| Startup warm | 1339 ms | — | — | — | — |
| GC Heap | 8.13 MB avg | — | — | — | — |
| Gen 0 GC | 0.05/s | — | — | — | — |
| GC pause max | 22.7 ms | — | — | — | — |

**Key observations for optimization targeting:**
1. **Connections page** — highest memory (350 MB WS) and CPU (7.64% avg, 10.5% max); primary candidate for optimization.
2. **Baseline WS ~305 MB** idle — WPF + Avalonia control overhead; potential to trim with explicit GC.GCSettings.
3. **CPU baseline 2–3%** idle — timer-driven refresh; investigate `DispatcherTimer` intervals and data update granularity.
4. **GC gen2 at 4 MB** with 29% fragmentation — periodic full compact triggers LOH allocation; monitor in soak.
5. **Thread pool 2–4 threads** — low but appropriate for tray app; watch for thread-pool starvation under load.
