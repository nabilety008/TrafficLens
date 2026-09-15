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

