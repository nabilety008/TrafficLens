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

## ADR-017: Durable aggregated history — minute buckets + daily rollup, restart-idempotent appends

**Status:** Accepted (TL-009)

Traffic history is persisted locally in **SQLite** at
`%LOCALAPPDATA%\TrafficLens\data\trafficlens.db` as two tables: 60-second
system buckets in `traffic_samples` (UTC start-time primary key, WAL,
`busy_timeout`, `Pooling=false`) and a per-local-date rollup in `daily_usage`
(kept forever). Usage is derived **only from the collector's cumulative-counter
DELTAS** (`CounterSampleReady`) via `TrafficHistoryAccumulator` — first
observation per adapter is a baseline, non-negative deltas only, resets /
reconnects / reboots re-baseline — so values are never fabricated. Tunnels are
excluded by default, the same policy as the live aggregate (ADR-009/010); on a
VPN-only host history honestly records ~zero system usage rather than
double-counting transport bytes behind the VPN.

Restart/crash safety is structural:

- Each flush appends minute buckets in one transaction with
  `INSERT OR IGNORE` and only rolls the `daily_usage` totals when
  `changes() == 1` (the bucket did not already exist). Re-flushing after a
  restart is a no-op, so history can **never be double-counted**.
- Graceful shutdown drains the accumulator and flushes the open minute (duration
  clamped 1..60 s) before the repository is disposed. A hard kill loses at most
  the current unflushed minute (up to the 30 s flush interval); nothing is
  invented to fill it.
- Writes are serialized through a semaphore gate; the UI consumes only a cached
  immutable `HistorySnapshot` (`HistoryChanged`), so SQL never runs on the WPF
  thread and per-second live updates are unaffected.

Retention: raw `traffic_samples` are pruned on startup past 90 days; `daily_usage`
is permanent (a few KB/year). Estimated steady-state footprint ≈ 8 MB with the
90-day prune (see `docs/DATABASE.md`).

Reasoning:
- Persisting only 60 s aggregated system buckets (not samples/packets) keeps the
  DB tiny and honest; the existing BackgroundService-free design means the service
  rides the collector's events instead of adding a second NIC polling loop, so the
  live dashboard and history never disagree about the underlying counters.
- The `INSERT OR IGNORE` + `changes()==1` guard costs nothing and removes the whole
  class of duplicate-history bugs without app-level locking or startup compaction.
- Local-date rollup rows make Today/Yesterday/7d/30d/Lifetime reads O(range rows)
  and remain valid across timezone/DST boundaries (`TimeZoneInfo` bucketing in
  `HistoryRangeCalculator`).
- The history chart mirrors the dashboard's no-double-count honesty policy
  (ADR-009/010), and exports the aggregation policy in a form future milestones
  (TL-013 settings, TL-014 CSV) can reuse.

## ADR-018: Floating widget is a zero-cost window over the existing live pipeline, closable-only

**Status:** Accepted (TL-010)

A compact always-on-top floating widget is implemented as a **frameless
secondary WPF window** (`FloatingWidgetWindow`, 280×110, `ShowInTaskbar=False`,
`ResizeMode=NoResize`) driven by a thin ViewModel (`FloatingWidgetViewModel`)
that subscribes to the exact same collector/provider events as the dashboard
(`SpeedSampleReady`, `NetworkChanged`, `AdaptersChanged`) and reads the
ADR-009/010 aggregate via `NetworkTrafficAggregator.AggregateRates`. It starts
**no poll loop, timer, SQLite query, or process-metadata call** of its own —
while it is hidden it costs nothing but the subscriptions' bookkeeping.

Widget state is persisted through the existing `ISettingsService` (one
`settings.json`; no second settings file): `FloatingWidgetEnabled` (startup
restore if it was visible at last graceful shutdown), `FloatingWidgetAlwaysOnTop`
(default on), `FloatingWidgetLeft`/`FloatingWidgetTop`. Position restoration
clamps the window rectangle into the union of the monitor work areas
(`SystemParameters.VirtualScreen*`) via `WidgetPositionHelper.Clamp`, so
negative multi-monitor coordinates survive and off-screen /
monitor-disconnected positions are pulled back, never lost or mis-trusted.

Lifecycle rules:
- **Single instance.** One `FloatingWidgetService` owns the window; repeat
  `Show()` only activates the existing window, never duplicates.
- **Widget close is hide-only.** The widget's `Closing` is cancelled and turned
  into `Hide()`; pressing ✕ never exits the application.
- **Main-window close still fully exits.** `MainWindow.Closing` calls
  `Hide()` (persisting position), then normal `OnLastWindowClose` shutdown
  proceeds and DI disposal calls `FloatingWidgetService.Dispose()`, which
  detaches the cancel handler and really closes the widget. There is no
  `Environment.Exit`, no new foreground thread, and no hidden window that keeps
  the process alive (consistent with ADR-015 / TL-007F). Closing the widget
  never requires or disposes the main window.

Reasoning:
- Reusing the dashboard's live event stream guarantees the widget and dashboard
  never disagree, and keeps the widget "free" at runtime (ADR-011 already
  established the event-driven thin-VM pattern).
- Only `FloatingWidgetEnabled` (ever) plus simplistic but safe position clamping
  avoids surprising restarts while keeping the widget cheap to reason about.
- The closable-only lifecycle deliberately reserves tray behavior
  (minimize-to-tray, background persistence, startup-with-Windows startup
  semantics) for **TL-011**; the widget is presentational and must not grow into
  a second app shell.

## ADR-019: System tray via System.Windows.Forms.NotifyIcon with a coordinator-based exit pipeline

**Status:** Accepted (TL-011)

A single tray icon is implemented with the built-in **`System.Windows.Forms
NotifyIcon`** (no third-party tray library). `TrafficLens.App` keeps
`<UseWPF>true</UseWPF>` and references `Microsoft.WindowsDesktop.App.WindowsForms`
as a plain `<FrameworkReference>` (no `UseWindowsForms`), so no WinForms global
usings leak into the WPF codebase and there are no CS0104 `Point`/`Brush`/
`Color`/`UserControl` ambiguities.

Behavior:
- Exactly **one icon, created once** at startup and disposed only on real exit
  (runtime-drawn 32×32 `Bitmap` → `GetHicon()` → `Icon.FromHandle`, destroyed via
  `DestroyIcon`); tooltip `TrafficLens`; no ghost icon.
- Tray menu: Open TrafficLens / Show-Hide Floating Widget / Always on Top
  (checkable) / separator / Exit. The Always-on-Top item toggles the same widget
  pin state as the in-widget pin button (one source of truth), and the widget
  pin's tooltip/accessibility text binds the shared `AlwaysOnTopLabel`
  (TL-010 polish). Culture changes relabel the menu in place — no NotifyIcon
  recreation.
- `MinimizeToTray` (default true) and `CloseToTray` (default true) settings via
  the existing `ISettingsService`. Minimize → `StateChanged` cancels the
  minimize and hides the main window. X close → `CloseToTray` decides
  **HideToTray** or **Exit**. The first close-to-tray shows a
  once-ever balloon (`TrayCloseNoticeShown` persisted) while collectors keep
  running and history keeps accumulating in the background.
- **Single exit pipeline**: `ApplicationExitCoordinator.RequestApplicationExit()`
  latches `IsExitRequested` (no close can be intercepted afterwards), disposes
  tray + widget, and calls `Application.Current.Shutdown()` (Dispatcher-safe).
  `App.xaml` uses `ShutdownMode="OnExplicitShutdown"` so tray-hide is a real
  background run. Collectors/history/DI are disposed by container disposal;
  there is **no `Environment.Exit`/`Process.Kill`**. All exit routes — tray
  `Exit`, minimize-then-close with `CloseToTray=false` — funnel through this one
  coordinator (a close with `CloseToTray=false` calls the coordinator right in
  `MainWindow.Closing`; merely closing the window would leave the app running
  under `OnExplicitShutdown` — an actual bug caught by real-Windows
  verification and fixed).
- WinForms `NotifyIcon` creates a hidden top-level message window
  (`WindowsForms10.Window.0.app.<hash>_r3_ad1`) owned by the app process; this
  window is the in-process proxy used to verify icon creation/disposal from
  scripts (classic `ToolbarWindow32` tray-button enumeration is unavailable on
  the Windows 11 XAML shell, and UI Automation finds no "TrafficLens" tray
  element).

Reasoning:
- NotifyIcon is in-box, stable, and language-neutral (the tray technical name is
  LTR), matching the local-first/no-third-party policy.
- One icon + one exit pipeline keeps lifecycle trivially testable and prevents
  ghost icons after hard kills or double exits (idempotent).
- Centralizing exit in the coordinator keeps `Exit` and `CloseToTray=false`
  behavior identical and unit-testable without a real tray (covered by
  `TrayBehaviorTests` + `ApplicationExitCoordinatorTests`).
- Tray clicks cannot be synthesized against the Windows 11 XAML tray from an
  external process; menu/click wiring is therefore verified by unit tests and
  the same `OpenRequested`/`ExitRequested` handlers, while real-Windows
  verification drives minimize/close via Win32 messages and checks the NotifyIcon
  message-window proxy.

## ADR-020: Local alert engine over existing pipelines (no SQL in the alert path)

**Status:** Accepted (TL-012)

Alerts are evaluated in a pure, injectable **`AlertEngine`** (Core) fed only by
the two pipelines TrafficLens already runs — the network collector's
`SpeedSampleReady` and the cached history `HistoryChanged` snapshot:
- **Speed rules** (`HighDownloadSpeed`/`HighUploadSpeed`) derive rate solely from
  `INetworkTrafficCollector.GetCurrentSamples()` + `INetworkAdapterProvider
  .GetAdapters()` via the existing `NetworkTrafficAggregator.AggregateRates`
  (ADR-009/010 policy), so they measure exactly the system totals the dashboard
  shows.
- **Daily usage rules** (`DailyDownloadLimit`/`DailyUploadLimit`/`DailyTotalLimit`)
  read only the cached immutable `HistorySnapshot.Today.*` — never the SQLite
  database (queries run only inside the history service; the alert service holds
  the reference to its immutable snapshot).
- Subscription-based, push-only: the alert service has **no poll loop, no timer,
  and no DB handles**. If history storage is unavailable, daily rules suspend
  silently (logged once) while speed rules keep working.

Two distinct gating semantics (both pure and unit-tested):
- **Speed = edge-triggered with cooldown.** An alert fires only when the rate
  crosses the threshold from below *and* the cooldown has elapsed. Remaining
  above never repeats, and a drop below re-arms without clearing the cooldown —
  so sustained high traffic or flapping around a threshold produces at most one
  balloon per cooldown window (default 5 min), which is what a user actually
  wants from a local monitor.
- **Daily = once per local calendar day, restart-safe.** The last-triggered
  local date is computed with the same DST-correct `TimeZoneInfo` logic as the
  history ranges (TL-009) and persisted to settings; on startup the engine
  restores it, so a same-day restart or crash never re-fires the alert, while
  the next local day re-arms automatically.

Delivery is the existing tray `NotifyIcon` balloon (`ShowAlert`, Warning icon,
8 s), and the Balloon click funnels into the TL-011 `OpenRequested` singleton
restore. A session-only bounded history buffer (capacity 100) backs the Alerts
page; it is intentionally **not persisted** (persisted alert history is a
future refinement, not required for V1 alerts).

Reasoning:
- Local-first + agent-friendly: no cloud callbacks, no always-on push service;
  everything stays on the machine and is testable without a real NIC.
- Reusing the aggregate pipeline and the cached history snapshot avoids a second
  polling loop and keeps the alert layer free of SQLite/file/UI concerns, so
  `AlertEngine` is fully unit-testable (clock + time zone injected).
- Edge-triggered cooldown and once-per-local-day are the two semantics that
  cannot "spam" or double-fire in practice; both are enforced in pure logic and
  proven by real-Windows verification (a sustained download produced exactly one
  balloon; a same-day restart produced no repeat).

## ADR-021: Settings page — staged save, unit-of-entry thresholds, and absolute always-on-top set

**Status:** Accepted (TL-013)

### Staged-save vs immediate-apply

Settings groups differ in how urgently a change must take effect:

- **Tray behavior (minimize/close-to-tray)** applies **immediately** on toggle,
  because the value gates the very next window-close and the user expects
  instant feedback (TL-011 behavior preserved and verified live on the page).
- **Everything else (language, start-with-Windows, start-minimized, widget
  enable/always-on-top, all alert rule fields, cooldown)** is **staged and
  committed by the Save button**. A single Save computes diffs against the
  values **loaded** at page-open (`_loaded*`), applies runtime side effects
  (culture switch, HKCU Run enable/disable, widget show/hide/topmost, alert
  `RefreshConfig`), persists one logical write, then calls
  `RefreshFromSettings()` so the page and the live services agree.
- **Reset** only *stages* defaults once the user confirms a Yes/No MessageBox;
  nothing is written until an explicit Save (mirrors the destructive-reads
  model; the history database is deliberately untouched by a settings reset).

Reasoning: mixed immediate/staged mirrors what users actually mean per group,
stays predictable, and converges on a single persist path that is unit-testable.

### Thresholds are stored in the physical unit (invariant bytes), typed in a unit

Alert thresholds are persisted as **bytes** (or bytes/day-equivalents) in
`alerts.<rule>.threshold` (invariant culture). The UI lets the user type a
number and pick a unit (KB/s…TB/s for speed rules; MB…GB…TB for daily rules);
the `AlertRuleViewModel` converts display→bytes on save and bytes→display on
re-load, so switching a unit never betrays the entered magnitude. This keeps
the alert engine's invariant-byte contract (TL-012/ADR-020) unchanged and puts
all unit smarts in one converter layer covered by unit tests.

### Widget always-on-top is an absolute set from Save, a toggle from the tray

`SettingsViewModel.Save` calls the new
`IFloatingWidgetService.SetAlwaysOnTop(bool)` (absolute value from the staged
setting), while the tray menu keeps `ToggleAlwaysOnTop()` for quick flips.
The initial implementation called `ToggleAlwaysOnTop()` from Save, which
flipped relative to the **already-written** value — unchecking "Always on Top"
persisted `False` and then immediately re-applied `True`. Verified live before
the fix; `SetAlwaysOnTop` closes the bug and makes Save idempotent.

Reasoning: the settings page expresses intended *state*; the tray expresses an
*action*. Encoding that difference in the interface keeps tests honest
(the fake service records which call was made).
### Settings page DataContext is the SettingsViewModel, not MainViewModel

The SettingsView's `DataContext` is set explicitly to the injected
`SettingsViewModel` at the page level. Inheriting the window's `DataContext`
(MainViewModel) was attempted first and silently produced an empty page —
save/load commands and the alert-rule rows bound to the window model instead
of the settings model. Page-scoped `DataContext` restores standard MVVM
without leaking window concerns into the settings page.

---

## ADR-022 — Idle CPU optimization (TL-014 stability audit)

### Context

TrafficLens v0.0.16 exhibited periodic idle-CPU spikes that exceeded the 15%
single-core threshold in 30-second measurement windows during the TL-014
harness F2 soak test. The application should consume near-zero CPU when idle
(no active network traffic, no user interaction), but post-startup profiling
revealed sustained background work even with no traffic flowing.

### Root causes identified

Three independent sources combined to produce the spikes:

**1. DashboardViewModel event-per-adapter dispatch storm.**
`SpeedSampleReady` fires once per adapter per poll interval (~1 s). With N
adapters (e.g. Wi-Fi + Bluetooth + OpenVPN TAP + OpenVPN DCO + Wi-Fi Direct
= 5 adapters), `OnSpeedSample` was called N times per second. Each call
dispatched `RefreshRates` to the WPF Dispatcher, producing N layout passes,
N `INotifyPropertyChanged` storms, and N graph-buffer appends per second —
all for scalar data that changes at most once per second.

**2. ConnectionsViewModel always-on dispatch.**
`OnConnectionsChanged` was subscribed to the provider's `ConnectionsChanged`
event regardless of whether the Connections page was visible. Every ~1 s poll
dispatched to the UI thread, forcing row-view-model rebuilds and icon
resolution even though the Dashboard was the active page.

**3. WindowsConnectionProvider native table enumeration.**
`RunLoopAsync` called `EnumerateOnce()` unconditionally every ~1 s, executing
four native P/Invoke calls (`GetExtendedTcpTable` × 2 + `GetExtendedUdpTable`
× 2), process resolution for ~159 connections, and cache lookups — all
needlessly when no page was reading the data.

### Decision

**Fix 1 — Coalesce speed-sample events in DashboardViewModel.** Add a
`_refreshPending` flag; when `OnSpeedSample` is called, set the flag and
schedule a single `BeginInvoke` that calls `RefreshRates` then clears the flag.
All per-adapter events within the same second collapse into one layout pass.

**Fix 2 — Gate ConnectionsViewModel on page visibility.** Add an `_isActive`
flag set by `MainViewModel.SelectPage` via `Connections.SetActive(bool)`.
When inactive, `OnConnectionsChanged` stores data in a pending buffer without
dispatching. On activation, `SetActive(true)` immediately refreshes from the
provider cache. The constructor and `SelectPage` call
`Connections.SetActive(false)` for the default Dashboard page.

**Fix 3 — Pause connection polling when page is hidden.** Add
`SetPollingEnabled(bool)` to `IConnectionProvider` and `WindowsConnectionProvider`.
When `_pollingEnabled` is false, `RunLoopAsync` skips `EnumerateOnce()` entirely.
`ConnectionsViewModel.SetActive` propagates the flag to the provider. The four
native P/Invoke table reads per second are eliminated when no page consumes them.

**Fix 4 — Default to inactive on startup.** `MainViewModel` constructor calls
`Connections.SetActive(false)` since Dashboard is the default page.

### Rationale

- **Coalescing** is preferred over throttling because speed samples arrive
  exactly once per second per adapter; collapsing N events into one is lossless
  (the aggregate has not changed within the same poll).
- **Page-visibility gating** is a clean separation: the ViewModel owns the
  "is this page visible" policy, the provider owns the "should I poll" policy.
  No new abstractions are introduced; `_isActive` is a simple boolean.
- **Provider-level polling pause** is the most impactful fix (eliminates native
  P/Invoke + process resolution entirely) and is safe because
  `WindowsConnectionProvider.GetCurrentConnections()` returns the last good
  snapshot when polling is paused — the page always has data on activation.
- **No new user-facing features**; no weakened thresholds; no new dependencies.

### Consequences

- Idle CPU dropped from ~20% peaks to ~9.88% max in 30 s windows (avg 3.43%
  over 30 min soak), well under the 15% harness F2 threshold.
- Main-thread on-CPU time confirmed at ~1.9% in the idle state via
  dotnet-trace (113 samples / 60 s, ~11 ms on-CPU total).
- Existing tests pass unchanged (397 total, 0 warnings, 0 errors).
- `IConnectionProvider` interface gained one new method
  (`SetPollingEnabled(bool)`); all implementations updated; `FakeConnectionProvider`
  in tests stubs it as a no-op.

## ADR-023 — Packaging and installer (TL-015)

**Status:** Accepted (TL-015)

Decisions for producing the first installable release.

### Decision

**1. One-command reproducible release pipeline.** `scripts/build-release.ps1`
owns the entire flow: resolve a .NET 8 SDK, restore, Release build, all tests,
single-file win-x64 publish, artifact validation, portable ZIP, Inno Setup
compile, and a SHA-256 sidecar. Nothing binary is committed; `artifacts\` and
`*.exe.sha256` are gitignored.

**2. Self-contained single-file exe (not MSIX, not WiX).** Publish with
`PublishSingleFile=true`, native-libs self-extract, compression, no trimming,
`InvariantGlobalization=false`. Reasoning: the app is local-first and
non-elevated (ADR-014); MSIX/WiX add signing, package-store, and file-layout
complexity with no benefit here, and would complicate the per-user
`%LOCALAPPDATA%` file model. The fa-IR satellite is bundled inside the exe.

**3. Inno Setup 6 per-user installer.** `PrivilegesRequired=lowest`, install to
`{localappdata}\Programs\TrafficLens`, stable AppId, Start-Menu shortcut,
desktop-icon option off by default, post-install launch. Per-user avoids UAC,
matches the app's asInvoker manifest and LocalAppData-only write pattern, and
still supports clean in-place upgrades via the uninstall DisplayVersion.

**4. Never ship debug symbols; never force-kill the app.** The `.iss` excludes
`*.pdb`. If the app is already running, a `[Code]` WMI check prompts the user to
close it (`WbemObjectSet.Count`, wpReady); Inno's built-in file-in-use dialog is
the second safety net. The installer must never terminate `TrafficLens.exe`.

**5. Installer must not touch user data.** Settings, SQLite history, and logs
live in `%LOCALAPPDATA%\TrafficLens\` and are never created, migrated, or
deleted by the installer; uninstall removes only the install dir + shortcuts
(`dirifempty` on the app-data path only). Manual data removal is documented.

**6. Clean product metadata.** `IncludeSourceRevisionInInformationalVersion=false`
so ProductVersion/FileVersion are plain `0.1.0` (no developer-machine git hash);
`AssemblyName=TrafficLens` so process name, exe, and uninstall keys are stable;
`AssemblyTitle=TrafficLens Network Monitor` so FileDescription is user-friendly.

### Rationale

- Pipeline reproducibility over manual steps: every artifact (exe, portable ZIP,
  installer, checksum) must be regenerable with one command and verified, fitting
  the agent-friendly and never-fabricate rules.
- Single-file self-contained keeps the installed footprint to `TrafficLens.exe`
  + Inno runtime files, simplifying the install/upgrade/uninstall story and the
  per-user file model; no runtime dependency on a framework install.
- Per-user Inno is the lowest-friction honest installer for a non-elevated app;
  MSIX/WiX were rejected for added complexity, not capability.
- Killing the app during update would contradict the graceful-exit design
  (`ApplicationExitCoordinator`); prompting respects the singleton and ETW-clean
  shutdown invariants.
- User-data separation is deliberate: history and settings are user-owned, not
  install-owned (matches uninstall tests: data preserved hash-identically
  through 0.1.0 → uninstall → reinstall → 0.1.1 → 0.1.0).

### Consequences

- Installer output: `artifacts\installer\TrafficLens-Setup-<ver>-win-x64.exe`
  + `.exe.sha256`; portable `TrafficLens-Portable-<ver>-win-x64.zip`.
- The installer is unsigned (no code-signing certificate in TL-015); Windows
  SmartScreen may warn. Documented in `docs/PACKAGING.md` as a future task.
- The tray/app icon is a temporary placeholder (`packaging/placeholder.ico`);
  branding is future work.
- Inno Setup 6.7.3 was installed per-user via `winget` (ISCC at
  `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe`); the pipeline resolves ISCC
  and fails with a clear message if missing.

## ADR-024 — Localized error/status detail surfaces (TL-016)

**Status:** Accepted (TL-016)

Decision for completing "no hard-coded user-facing strings" under full Persian
localization.

### Decision

**1. Localized summary text is the only surfaced detail; raw messages stay in
the log.** When a collector/provider/history snapshot reports a failure, the
ViewModel shows a localized banner/text from `Strings*.resx`: Applications →
`PermissionDeniedDetailLabel` / `MonitoringFailedDetailLabel`, Connections →
`ConnectionsErrorDetailLabel`, History → `HistoryErrorDetailLabel`. The raw
developer-facing message (e.g. `_collector.LastError`, provider `Error`,
`HistorySnapshot.Error`) is no longer rendered to the UI; it continues to drive
the error state and is written to the application log.

**2. App-layer fix only; no architecture change.** The `ILocalizationService`
contract, ViewModel landscape, and error-state model are unchanged. Each affected
ViewModel gains a private localized-detail field refreshed in
`RefreshLocalizedStrings()` and the existing culture-change event path re-renders
it, so switching English ⇄ فارسی at runtime re-localizes the banner.

**3. Remaining hard-coded UI text is deliberate.** The graph axis "now" label was
the last literal; it is now data-bound via `TrafficGraphControl.NowLabel` (DP,
default `"now"`) to the localized `Dashboard.GraphNowLabel`. Language endonyms
(`English` / `فارسی`) and the `TrafficLens` brand are intentionally never
translated. History chart dates use `CultureInfo.CurrentCulture` so fa-IR renders
Persian-calendar dates.

**4. Key parity is a test invariant.** `LocalizationResourceTests.RequiredKeys`
asserts the en and fa-IR key sets are identical; new keys are added to both files
together. fa-IR content is verified on the published build by
`scripts/tl016-verify.ps1` (nav + dashboard + History page in Persian, no English
leak).

### Rationale

- Raw collector errors are technical strings (provider exceptions, native
  messages) that cannot be meaningfully translated per-locale; translating them
  verbatim would be fabricated localization. Keeping them in the log preserves
  debuggability while the UI stays fully localized.
- Value semantics preserved: the banner still appears exactly when the underlying
  error exists (`PermissionDenied`/`Failed` status, `HasError`, `IsUnavailable`),
  so behavior is unchanged and testable.
- Surfacing only at the App layer keeps Core/Network/Infrastructure free of UI
  strings (matching the existing convention).

### Consequences

- `Strings.resx` and `Strings.fa-IR.resx` grew to 146 keys each (identical sets);
  `LocalizationResourceTests.RequiredKeys` updated and pass.
- Tests asserting raw English text was surfaced were updated to assert the
  localized detail and the absence of the raw string (en + fa-IR).
- Future raw messages added by providers simply stay log-only; UI consumers add a
  resx key when user-facing detail is required.

## ADR-025 — Branding foundation, About page and Diagnostics (TL-016 continuation)

**Status:** Accepted (TL-016)

Decision completing the "product polish + branding foundation" scope of TL-016:
a replaceable icon pipeline, an About page, and a copy-to-clipboard Diagnostics
block.

### Decision

**1. One replaceable brand asset + a generator as the single source of truth.**
`scripts/generate-icons.ps1` renders the brand glyph (rounded `#1E1E2E` square,
cyan `#4FC3F7` down-arrow, teal `#26A69A` up-arrow — the TL-011 tray glyph) at
16–256 px and writes `assets/branding/TrafficLens.ico` (PNG-encoded multi-size
ICO) plus 256/128 PNGs. The assets are small, committed binaries; the generator
keeps them reproducible. Replacing final marketing art means swapping the
drawing routine (or the files) with zero wiring changes.

**2. Branding wiring is centralized, not hard-coded per surface.** The ICO is
declared once in `TrafficLens.App.csproj` (`<ApplicationIcon>` and a `<Resource>`
loadable via the pack URI `pack://application:,,,/TrafficLens.ico`); the window
icon, floating-widget glyph, tray icon, About page and installer
(`SetupIconFile={#BrandIcon}` in `TrafficLens.iss`) all consume it. The tray
icon prefers the embedded ICO and falls back to the runtime-drawn glyph if the
resource is unavailable — preserving the TL-011 zero-asset guarantee as a
fallback path.

**3. About page is a first-class localized page.** A 7th nav item (`About`, page
hosted like the others via a ViewModel + View, DI-registered as singletons)
shows product identity, version, runtime, OS, display language and the data /
settings / logs paths. All text comes from `Strings*.resx`; paths and endpoints
render LeftToRight (matching the existing per-control RTL convention); the
brand name stays untranslated (per ADR-024). The nav bar switched from a
`StackPanel` to a `WrapPanel` so seven items cannot overflow the 640px minimum
window width.

**4. Diagnostics is a pure, testable builder + thin actions.** Version comes from
`AssemblyInformationalVersionAttribute` (no `Assembly.Location`, which is empty
under single-file publish — avoids IL3000). `DiagnosticsInfo.Build` is a pure
static function that joins `Label: Value` lines, so clipboard content is unit-
tested without touching the clipboard. Copy uses WPF `Clipboard.SetText` in a
try/catch (never throws); "Open logs folder" shell-opens the entries directory,
falling back to its parent if the `logs` dir has not been created yet.

**5. Versioning stays at the v0.1.0 baseline.** No version bump in this task;
the About page reads the runtime informational version, so the next release
candidate only edits `Directory.Build.props`.

### Rationale

- A generated asset set avoids binary drift between the window icon, tray icon,
  EXE icon and installer icon, and keeps the "replaceable, not final" promise
  cheap: one routine to edit, one script to run.
- The About/Diagnostics surface is the least architectural way to expose where
  the app stores data — the first thing users and support need when reporting
  issues — without adding new systems (no telemetry, no crash reporting).
- Keeping the tray fallback guarantees the app still works if a resource is
  missing (e.g. future trim/publish changes), matching the existing fallback
  philosophy in TL-011.

### Consequences

- `Strings.resx` / `Strings.fa-IR.resx` grew from 146 to 161 symmetric keys;
  `LocalizationResourceTests.RequiredKeys` updated.
- New tests: `AboutViewModelTests`, `MainViewModelTests` (navigation incl. About),
  `BrandAssetsTests` (ICO header/frames, PNG dimensions). Suite now 410 tests.
- `packaging/placeholder.ico` was removed; the installer points at the shared
  brand asset and carries `VersionInfo*` setup metadata. Stale docs updated:
  `PACKAGING.md` (branding section), `docs/BRANDING.md` (new),
  `PROJECT_STATUS.md`, `TASKS.md`, `CHANGELOG.md`. ADR-024's "146 keys" note
  was accurate at the time and is superseded by this task's 161 keys.
- English installer and placeholder project URLs in `TrafficLens.iss` remain
  documented follow-ups for the real release, not this task.



## ADR-026 — Windows feature updates are held, never disabled (Batch 4)

**Status:** Accepted (post-v0.1.4 Batch 4)

The previous Windows Update behavior could write `NoAutoUpdate=1` under
`HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU`, which disables
automatic updates broadly — including security and quality fixes. That was
**wrong** for TrafficLens and is replaced.

### Decision

**TrafficLens never disables Windows Update.** The feature-update option applies
only Microsoft's supported **Target Feature Update** policy:

- `ProductVersion`, `TargetReleaseVersion` (DWORD 1) and
  `TargetReleaseVersionInfo` (the currently installed release, e.g. `25H2`)
  under `HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate`.
- This pins the machine to its current Windows feature version while security
  updates, quality updates, Defender definition updates and all Windows Update
  servicing continue untouched.
- Windows Update services, BITS, Defender, quality-update pause/deferral and
  safeguard holds are never modified. `NoAutoUpdate` is never written by
  TrafficLens (asserted in tests).

**Detect, never guess.** The target release is read from the live machine
(build >= 22000 means Windows 11 regardless of the stale `ProductName` string;
release from `DisplayVersion`, falling back to `ReleaseId`, normalized to strict
`YYH1/H2`). An incomplete or unreadable detection **refuses** to apply the
policy rather than pinning a wrong version.**Ownership, snapshot, rollback.** A schema-versioned change record
(`%LOCALAPPDATA%\TrafficLens\windowsupdate-state.json`) snapshots every value
before it is written. Only TrafficLens-owned values are restored on release;
an externally created policy key (any pre-existing value TrafficLens does not
own, including an external `NoAutoUpdate`) is detected as organization-managed
and **never overwritten**. A partial apply failure rolls back owned values and
deletes the record only when provably clean. Legacy records from the old
`NoAutoUpdate` behavior are migrated: the legacy `NoAutoUpdate` value is
restored/removed on release using its snapshotted previous state.

### Consequences

- Security posture is never weakened by the option; the Windows Update agent
  keeps servicing the machine.
- The hold is reversible and auditable via the change record.
- Machine-wide effect still requires one elevated `reg.exe` invocation (UAC);
  reads never require elevation (read-only registry handles).
