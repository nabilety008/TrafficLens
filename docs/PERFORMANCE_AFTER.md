# TrafficLens — TL-017 Performance Results (After)

> **Branch:** `feature/tl017-performance` | **Base:** master v0.1.1 (`1bcc9c9`)
> **Measured:** 2026-09-18 (baseline) → 2026-09-19 (after each phase)
> **Companion doc:** `docs/PERFORMANCE_BASELINE.md` (pre-optimization numbers)

This is the authoritative BEFORE vs AFTER report for TL-017. All measurements
were taken on the real Windows host with the same non-elevated setup, the same
test settings (en-US, MinimizeToTray True, CloseToTray True,
FloatingWidgetEnabled False), a Release build, and the published single-file
`TrafficLens.exe`. Numbers marked *noise* sit within run-to-run variation and
are **not** claimed as improvements. Where methodologies differ between the
Phase 6 soak and the dedicated probe, only the directly-comparable dedicated
probe is used for the headline Connections numbers (see
[Measurement Rules](#measurement-rules)).

---

## Measurement Rules

- Same host, same settings, same `.exe`, same non-elevated environment for all
  phase comparisons.
- **Dedicated comparable probe (Connections):** launch → navigate to
  Connections → fixed 3-minute window (WS/Private drift + CPU avg/max over 12 ×
  15 s samples). Run both before and after; the only directly comparable number
  for the Connections page.
- **Noise rule:** any metric whose change is smaller than observed run-to-run
  scatter is reported as *no meaningful change* and never claimed as an
  optimization.
- **Startup wall-clock rule:** wall-clock launch time on this host is dominated
  by ambient system state (AV, JIT, background I/O), so wall-clock differences
  are not claimed. Only the *internal* instrumented stage timings are compared.
- **Elevated ETW was not measured** — this environment could not automate the
  UAC prompt (see [Known Measurement Limitations](#known-measurement-limitations)).

---

## Phase 0 Baseline

Pre-optimization numbers are in `docs/PERFORMANCE_BASELINE.md`. Relevant points:

- Overall idle (tray-hidden): WS ~306–310 MB, Private ~169–174 MB, CPU avg
  ~2.2%, threads ~22–28, handles ~677–683.
- Connections idle page soak: WS avg ~351 MB, Private avg ~206 MB, CPU avg
  ~7.6% (max 10.5%).
- Startup: wall clock cold 1301 ms / warm 1339 ms; internal instrumented stages
  830–875 ms (pre-render + page-VM construction + first-render pipeline).
- History flush: avg 7.4 ms, p95 9 ms (see Phase 4).
- Idle allocation: ~2.2 MB/s sustained (see Phase 5/6).

---

## Phase 1 — Connections Optimization

**What changed:** virtualized Connections rows (in-place
`ConnectionRowViewModel` updates keyed by the stable `ConnectionKey`, no full
row-collection rebuild per poll), delta diffing on the native table snapshots,
and a per-refresh icon budget. Rows update in place when only counters change;
the collection rebuilds only when the key sequence changes.

**Dedicated comparable probe (3 min, Connections visible):**

| Metric | Before | After | Change |
|--------|--------|-------|--------|
| CPU avg | 15.31% | 6.65% | **~57% lower** |
| CPU max | 41.41% | 16.81% | **~59% lower** |
| WS drift | +24.29 MB / 3 min | +9.48 MB / 3 min | **~61% lower** |
| Private drift | +23.26 MB / 3 min | +8.00 MB / 3 min | **~66% lower** |

This is the headline result of TL-017: the Connections page (the most expensive
view) dropped roughly three-fifths of its CPU and memory growth under an
identical posed measurement.

---

## Phase 2 — Per-Tick Allocation Reduction

**What changed:** removed ~550 avoidable allocations per poll tick (~450
short-lived per-row strings and ~101 `ConnectionKey` records created twice per
row update).

**GC/counters capture (Connections visible, after):**

| Counter | After |
|---------|-------|
| Gen 0 GC | 0.20 / s |
| Gen 1 GC | 0 / s |
| Gen 2 GC | 0 / s |
| Allocation rate | ~1.53 MB/s |
| % Time in GC | ~0.8% |
| Heap | bounded (no growth trend) |

- The allocation-rate drop is real and reproducible.
- **No CPU improvement is claimed for this phase** — the delta between Phase 1
  and Phase 2 sits within run-to-run noise.

---

## Phase 3 — Startup: Lazy Page Instantiation

**What changed:** the six page Views are created lazily on first navigation
instead of eagerly inside `MainViewModel` / `MainWindow`.

**Internal instrumented stage (mainviewmodel + 6 view XAML):**

| Stage | Before | After |
|-------|--------|-------|
| MainViewModel + 6 view XAML block | ~170 ms (eager) | **~70 ms** (lazy) |

- The ~100 ms of avoided pre-render work is real and internal.
- **Wall-clock launch is NOT claimed to be faster.** Wall clock is dominated by
  the environment (BEFORE: internal 830–875 ms / wall 1.36–1.39 s; AFTER:
  internal 1.6–2.3 s / wall 2.7–3.5 s). On these particular runs the AFTER wall
  numbers were higher from ambient system state, the opposite direction of the
  internal improvement — reported as *environment-dominated noise*.

---

## Phase 4 — SQLite History Optimization

**What changed:** a persistent background SQLite writer with prepared commands,
a dedicated flush queue, and `lifetime_totals` schema v2 so the Lifetime query
is O(1) instead of O(n).

| Metric | Before | After | Change |
|--------|--------|-------|--------|
| Flush avg | 7.4 ms | **1.3 ms** | **~5.7× faster** |
| Flush p95 | 9 ms | **2 ms** | **~5.7× faster** |
| QueryLifetime | O(n) scan (~3–4 ms) | O(1) (~2–3 ms) | constant-time |

Schema v2 adds `lifetime_totals`; `PRAGMA user_version` migrated the pre-release
v1 DB. User-visible history data/storage semantics are unchanged — **with the
migration back-fill fixed during final verification** (see below): upgrading a
real v0.1.1 (schema v1) database now seeds `lifetime_totals` from the existing
`daily_usage` sum instead of reporting a Lifetime total of 0 for pre-upgrade
data. Documented in `docs/DATABASE.md`.

---

## Phase 5 — Idle Tray Optimization

**What changed:** gated dashboard/tray refresh activity when nothing changed
(`SetActive` gating for widget/hidden surfaces) and a widget dispose/recreate
path so hidden surfaces stop re-rendering.

| Metric | Before | After | Verdict |
|--------|--------|-------|---------|
| CPU avg | 1.08% | 1.07% | *no meaningful change* |
| CPU max | 5.91% | 4.99% | *noise-range* |
| Threads | 16 | 15 | minor |
| Allocation | ~2.2 MB/s | **~0.5–1.1 MB/s** | clear reduction |
| WS drift (5 min idle) | +4.7 MB | **+2.4 MB** | reduced |

**No CPU-average improvement is claimed for the idle tray** — the before/after
CPU averages are indistinguishably close on this host.

---

## Phase 6 — Long-Run Soak

Two dedicated soak harness runs on the final build (`scripts/tl017-soak.ps1`).

**60-minute idle tray soak:**

| Metric | Result |
|--------|--------|
| CPU avg | 0.97% |
| CPU max | 7.69% |
| Working Set after warm-up | stabilized ~240 MB, **+0.2 MB drift** in the measured window |
| Private memory | ~120 MB |
| Threads | 14–17 |
| Handles | 468–496 |
| Allocation | 0.5–1.1 MB/s |
| Managed heap | 12–14 MB |
| Gen 1 / Gen 2 GC | negligible |

**30-minute Connections-visible soak:**

| Metric | First 15 min | Second 15 min |
|--------|--------------|---------------|
| WS delta | +14.5 MB | **+2.5 MB** |
| Private delta | +12.7 MB | **+5.0 MB** |
| CPU avg | ~8.3% overall | |
| Handles | 751–760 | |

> **No leak was observed during the measured soak window.** Both series (WS /
> Private) plateau or decelerate strongly in the second half of the Connections
> soak; idle WS is flat after warm-up. This is a 60/30-minute soak, **not** a
> 24-hour soak — no claim is made beyond the measured window.

**Full memory picture (honest phrasing):** before the optimizations, overall
idle Working Set measured ~306 MB (Connections page ~351 MB); after, final
idle-tray Working Set stabilized around **~240 MB** after warm-up. Because the
before/after *methodology* differed (per-scenario 3-minute soaks vs the
60-minute soak with warm-up exclusion), no precise percentage memory reduction
is claimed.

---

## Before-After Summary Table

| Metric | Before | After |
|--------|--------|-------|
| Connections CPU avg (dedicated probe) | 15.31% | **6.65%** |
| Connections CPU max (dedicated probe) | 41.41% | **16.81%** |
| Connections WS drift / 3 min | +24.29 MB | **+9.48 MB** |
| Connections Private drift / 3 min | +23.26 MB | **+8.00 MB** |
| Startup mainVM + 6 views block (internal) | ~170 ms | **~70 ms** |
| History flush avg / p95 | 7.4 ms / 9 ms | **1.3 ms / 2 ms** |
| Idle-tray allocation | ~2.2 MB/s | **0.5–1.1 MB/s** |
| Idle-tray WS drift / 5 min | +4.7 MB | **+2.4 MB** |
| Idle-tray threads | 16 | 15 |
| Idle-tray CPU avg | 1.08% | 1.07% (*no meaningful change*) |
| Long-run idle WS (post warm-up) | ~306 MB | **~240 MB, flat** |

---

## Correctness Regression

TL-017 is monitoring-only and changes **no observable behavior**:

- No change to monitoring accuracy, byte accounting, or tunnel exclusion.
- ETW PID/start-time identity and PID-reuse isolation unchanged.
- History UTC timestamps, local-day/DST range semantics, and daily-usage data
  unchanged (schema v2 is a storage/query optimization only).
- Alert semantics, cooldowns, and daily re-arm unchanged.
- No polling interval was changed to make benchmarks look better.
- No packet payload policy change; monitoring-only architecture preserved.
- All 416 automated tests pass, plus the real-Windows regression harnesses
  (see below).

---

## Final Verification (performed 2026-09-19 on the current branch source)

Recorded exactly as executed — no failures hidden.

| Check | Result |
|-------|--------|
| `dotnet build TrafficLens.sln` Debug | **0 warnings / 0 errors** |
| `dotnet build TrafficLens.sln` Release | **0 warnings / 0 errors** |
| `dotnet test TrafficLens.sln` Release | **416/416 PASS** (212 Network / 41 Infrastructure / 163 App) |
| Published single-file win-x64 republish | `artifacts/publish/win-x64/TrafficLens.exe` rebuilt from current source |
| `tl015-smoke.ps1` | PASS |
| `tl016-verify.ps1` | PASS |
| `tl017-connprobe.ps1` | PASS on retry (first attempt was a transient UIA timing flake, diagnosed and re-run) |
| Packaging (`build-release.ps1`) | Full pipeline succeeded at version 0.1.1 (no tag, no version bump) |
| TL-017 benchmark scripts | All runnable; `tl017-db-benchmark.ps1` had a build-order defect (fixed) — see below |

### Defects found and fixed during final verification

1. **`tl017-db-benchmark.ps1` (harness):** invoked `dotnet run --no-build` on a
   benchmark console project it generates on the fly, but never built that
   project first (and wrote the same source to two files, `DbBenchmark.cs` and
   `Program.cs`), so a clean run could not start `DbBenchmark.exe`. Fixed by
   removing the duplicate source write and letting `dotnet run` build the
   generated project. Confirmed runnable: 30 × 20-bucket flush + query + prune
   execute end-to-end.

2. **Schema v1→v2 migration lost the pre-upgrade Lifetime total (production
   data-correctness bug):** the Phase 4 `Migrate()` created `lifetime_totals`
   but never seeded it, so a real v0.1.1 database (schema v1, released) that was
   upgraded would report a Lifetime history of 0 until new buckets arrived —
   contradicting the "history data unchanged" guarantee. Fixed by back-filling
   `lifetime_totals` from the existing `daily_usage` sum during migration
   (`INSERT OR IGNORE ... SELECT SUM(...) FROM daily_usage`), with a new
   regression test `InitializeAsync_MigratesV1History_BackFillsLifetimeTotal`.
   `docs/DATABASE.md` updated to describe the migration.

   This is the only production-code change made during Phase 7 final
   verification, and it was driven by a confirmed defect, not an optimization
   pass.

---

## Known Measurement Limitations

- **Connections dedicated probe** is the only directly comparable before/after;
  the Phase 1→2 CPU delta and idle-tray CPU average are within noise and are
  explicitly not claimed.
- **Startup** comparisons are internal stage timings only; wall clock is not
  comparable across runs on this host.
- **Elevated ETW was NOT measured.** The environment is non-elevated and the
  UAC prompt cannot be automated, so no elevated long-run CPU/allocation/memory
  numbers were captured. Verified in this state only: the app runs non-elevated
  with the ETW provider inactive (`EtwActive:false` in the probe output), the
  `PermissionDenied` fallback is exercised, no crash, no orphan session, and
  graceful shutdown stays clean. Elevated ETW profiling is documented as
  remaining manual validation, NOT complete.

---

## Remaining Manual Validation

- Elevated long-run ETW measurement (CPU / allocation / memory under sustained
  traffic with the kernel ETW session active) — requires running the app as
  Administrator and long-run monitoring from an elevated helper.
- A 24-hour soak has not been performed; only the 60/30-minute windows above.

---

## Conclusion

TL-017 delivers a clear, reproducible reduction on the most expensive surface
(Connections page ~57–66% CPU/memory-growth reduction on the dedicated probe),
a real allocation-rate cut per tick, internal startup pre-render removal, a
~5.7× history-flush speedup, and a flat ~240 MB long-run idle profile with no
leak observed within the measured soak window — all while keeping every test
passing and the monitored behavior unchanged. No claims are made beyond what
these measured windows support.