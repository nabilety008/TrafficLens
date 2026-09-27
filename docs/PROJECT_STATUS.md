# TrafficLens — Project Status

Updated: 2026-09-27

## Current Milestone

**v0.1.4 final human UI polish** on `feature/winui3-migration`, from the approved
baseline `99397a4`. Six defects found in human review were fixed, scoped strictly
to those items — no backend, collector, monitoring or packaging changes, and no new
timer, poller or background worker.

- **Focused tests: 100 passed, 0 failed** (`TrafficLens.WinUI.Tests`).
- **Full solution suite: 666 passed, 0 failed** (270 App / 212 Network /
  84 Infrastructure / 100 WinUI).
- **Release x64 build: 0 warnings, 0 errors.**
- **Minimum publish check:** 815 files, no PDBs, no test assemblies,
  `TrafficLens.WinUI.pri` present and a valid `mrm_` container, canonical icon
  unchanged (`9F8DD468…`), `fa-IR` satellite present.
- **Fixed:** title-bar/caption-button collision (padding now resolved from the live
  `AppWindow.TitleBar` insets and the window DPI rather than a per-language
  margin); widget X button did not disable or persist the widget (it bypassed the
  service and hid the window directly); no quick widget toggle at the top of
  Settings (both switches are now views of one idempotent state owner); Alerts was
  one long vertical form (five uniform cards in a wrapping grid, cooldown kept
  separate); collapsed navigation clipped labels (all seven items now have native
  Segoe Fluent Icons and an icon-only compact pane); the widget could be dragged
  off-screen (now clamped to the monitor work area using the real DPI-scaled size,
  with nearest-work-area recovery for a disconnected monitor).
- **Bugs found while implementing and fixed:** `SelectWorkArea` let a zero-overlap
  work area override a positive-overlap one, so a widget near the right edge of one
  monitor would jump to the next; `RestoreIfEnabled` reloaded the state and then
  called the idempotent `SetEnabled(true)`, so an enabled widget was never recreated
  on start-up; the drag delta mixed DIPs with physical pixels; and
  `AppWindowChangedEventArgs` has no `DidLayoutChange`, so the caption safe area was
  not re-applied on resize.
- **Right-to-left title-bar defect found by launching the Persian build and fixed.**
  The caption insets are reported in flow order, not as physical left and right:
  measured at 150% DPI the shell reported `LeftInset` 207 px with `RightInset` 0
  while the caption buttons were on the physical right (x 796..1003), so the safe
  area was applied to the wrong edge and the right-aligned title ran under
  Minimize/Maximize/Close. `ResolvePadding` now takes the insets in flow order plus
  the layout direction and returns physical left/right padding, and the call site
  passes `RootGrid.FlowDirection == FlowDirection.RightToLeft`. The unit test that
  had encoded the wrong assumption was replaced by the measured case.
- **Human verification of all six items: PENDING.** Automated checks are not a
  visual PASS; title-bar spacing in Persian, card sizing, collapsed navigation and
  widget dragging all still need a human look.
- **No installer or ZIP has been produced for this change.** The v0.1.4 artifacts
  generated from `99397a4` are **superseded and are not final**; they must be
  regenerated only after human verification passes. No merge, tag, push or publish
  was performed.

**Prior: v0.1.4 WinUI 3 release candidate** on `feature/winui3-migration` (WPF
rollback `f9017f0`). WUI-001 through WUI-008 implemented; **WUI-009 extended
audit intentionally stopped** before every planned long-duration phase
completed (long performance/soak: **NOT TESTED**). Completed audit evidence is
preserved in `%TEMP%\opencode\wui009-*`; WUI-009 production fixes committed in
`21ca7c1`.

- **Release pipeline run once, green:** Release x64 build **0 warnings / 0
  errors**, **571/571 tests PASS** (270 App / 212 Network / 84 Infrastructure /
  5 WinUI).
- **Entry point is WinUI 3** (`TrafficLens.WinUI.exe`, unpackaged, Windows App
  SDK 2.5.1, self-contained `win-x64` loose layout, no single-file, no MSIX).
  The WPF project is retained as rollback/reference and is not published.
- **Artifacts:** installer `TrafficLens-Setup-0.1.4-win-x64.exe` (85.78 MB,
  `ProductVersion 0.1.4`) + portable `TrafficLens-Portable-0.1.4-win-x64.zip`
  (123.61 MB, 815 entries), each with a `.sha256` sidecar. Installer SHA-256
  `E03C4B00…`, ZIP SHA-256 `0539049C…`.
- **Superseded broken RC — DO NOT DISTRIBUTE.** The first v0.1.4 RC (installer
  `3E9DEA0E…`, ZIP `660E1D4C…`, 814 entries) was **invalid**: `dotnet publish`
  omitted `TrafficLens.WinUI.pri`, so the compiled XAML was absent. It is kept
  only as history and must not be shipped.
- **RC defect found and fixed — publish/resource packaging.** MRT Core writes
  the project PRI straight to `$(TargetDir)` (bin) and only registers it as a
  publishable item when `AppxPackage == true` (MSIX). This app is unpackaged, so
  `dotnet publish -o` silently dropped the PRI. `TrafficLens.WinUI.dll` embeds
  zero XBF resources, so the app started, set its culture, and then died with
  `XamlParseException: XAML parsing failed` in `MainWindow.InitializeComponent()`
  (Application Error 1000, `Microsoft.UI.Xaml.dll`, `0xc000027b`). Fixed by an
  explicit `IncludeProjectPriFileInPublish` target that adds the generated PRI
  to `ResolvedFileToPublish`, and by a release guard that fails the run before
  ZIP/installer creation when the PRI is absent, empty, truncated or not a
  valid PRI container (`mrm_`).
- **Smart App Control did NOT block the runtime attempt.** The process started
  normally and **no Code Integrity event was written**; the earlier
  `0x800711C7` framing for this build was wrong. The failure was purely the
  incomplete publish output. SAC remains ON and unchanged
  (`VerifiedAndReputablePolicyState=1`).
- **Runtime launch now PASSES** (one attempt, PID 12484): `WinUI MainWindow
  activated`, culture `fa-IR`, 7 nav items with داشبورد selected, live
  dashboard data (دانلود 8.55 KB/s, آپلود 666 B/s, مجموع 9.2 KB/s), network +
  connection + history services started, SQLite ready. Floating Widget is
  visible at **340x140** with `Always On Top` active. The app was left running
  for human visual inspection. **Icon/widget/Persian human verification is
  still PENDING** — nothing here is a human PASS.
- **Unsigned RC:** no code-signing certificate exists, so
  `Get-AuthenticodeSignature` reports `NotSigned` and the published checksums
  describe the unsigned build only. The pipeline is signing-ready (`-Sign`).
- **Trust-path conclusion (researched, not assumed):** there is **no supported
  deterministic way to launch this release unsigned** on a Smart App
  Control-enforced host. Microsoft app intelligence may allow a file it can
  classify as safe, or the file may be signed with an **RSA** certificate
  chaining into the Microsoft Trusted Root Program — but cloud reputation only
  accrues through real-world distribution, so it is not a reproducible release
  gate, and a self-signed certificate is not honoured for this purpose. EV
  certificates are no longer a shortcut (same reputation schedule as OV since
  2024); OV from a Trusted Root Program CA is sufficient, and Microsoft's docs
  recommend Azure Artifact Signing for non-Store distribution. Signing is the
  only supported route, so signing- and human-dependent checks stay PENDING.
- **Signing-pipeline audit (static, no certificate used):** the sign set is now
  *discovered* rather than hard-coded. It was previously a 6-name list that
  **omitted the shipped Persian satellite `fa-IR/TrafficLens.WinUI.resources.dll`** —
  a partial signature is treated as untrusted, so that release would have been
  unlaunchable despite every signing call succeeding. Discovery now selects all
  7 TrafficLens-owned PE images, re-verifies the whole set afterwards (status
  `Valid` **and** an RFC 3161 timestamp countersignature), rejects ECC and
  expired certificates up front (SAC is RSA-only), and passes `/sm` when the
  certificate is in `LocalMachine\My`. Microsoft/.NET/WinAppSDK binaries are
  never re-signed. No certificate, thumbprint or key exists in the repo and
  none was purchased or generated.
- **Runtime verification PENDING / not possible on this host:** Smart App
  Control is ON (policy `{0283ac0f-fff1-49ae-ada1-8a933130cad6}`,
  `VerifiedAndReputablePolicyState=1`, no enterprise-authored policy) and blocks
  fresh unsigned binaries (`0x800711C7`). No security setting was changed and
  no bypass was attempted. v0.1.4 was verified **statically**: publish payload
  (x64 PE, Windows App SDK runtime, no PDBs/test assemblies/WPF exe, branding
  assets, embedded en + `fa-IR` satellite), archive structure and entry names,
  clean extraction, version metadata, signature status and checksums.
  Install/launch/reputation checks and the TL-023 lifecycle harness are
  **PENDING** a signed or otherwise trusted build.
- **Human GUI verification PENDING** for the WUI-009 widget title bar and for
  the final window/widget icon state (`6710e4c`, `452e650`, `6381601`).
  WUI-001 shell **human PASS** (`5677e14`).
- No merge, tag or publish was performed. v0.1.3 artifacts were preserved
  unchanged (installer SHA-256 `55EBCCD8…`).

**Prior: v0.1.3** released on `hotfix/ui-polish-after-0.1.2`: dark theme
consistency, white content background removal, ComboBox/ContextMenu dark
popups, complete runtime English/Persian switching, Floating Widget
lifecycle fix, About icon quality, navigation crash fix (ComboBox GridLength),
Alerts page configured rules display, and localized empty states.
542/542 tests pass, Debug + Release
0 warnings/0 errors. Human GUI verification PASS.

Headline: History `Today` shows an "Hourly Traffic" title with up to 24
zero-filled local-hour bars that balance **exactly** against the Today summary
cards (both derive from the same committed `daily_usage`/`traffic_samples` rows).
**458/458 tests** (212 Network / 55 Infrastructure / 191 App), Debug + Release
0 warnings / 0 errors. Real-Windows GUI verification
(`scripts/tl018-verify.ps1`) PASS on the published single-file build: en-US and
fa-IR hourly/daily chart titles with range-button switching, all 5 range
buttons, window resize, a real-traffic data chain with exact
`daily_usage Today == Σ(today's traffic_samples)` reconciliation, graceful
exit, and no orphan processes. Bar geometry is custom `OnRender` (no UIA tree,
so rendered pixels are honestly not asserted; the DB chain + unit math are
verified instead).

**Prior:** M12 (TL-017 performance audit) completed and **merged into `master`**
at `21dabb9` (see `docs/PERFORMANCE_AFTER.md` for the BEFORE vs AFTER report).

Previous milestones (for context): **M10/M11/M11a** on `master` — TL-015
reproducible packaging/installer pipeline and release v0.1.1 (tag `v0.1.1`);
TL-016 full Persian localization (merged `9dc8335`, no hard-coded UI strings,
graph "now" label data-bound, localized status/error detail surfaces, culture
-aware History dates, en + fa-IR at 161 symmetric keys); TL-016 continuation
branding foundation (icon pipeline → `assets/branding/`, About page,
Diagnostics block, suite at 410 tests).

## Task IDs

- TL-001 Project Bootstrap — **DONE**
- TL-002 Global Network Collector — **DONE**
- TL-003 Download/Upload Calculation — **DONE**
- TL-004 Network Adapter Detection — **DONE** (audit + gap fix)
- TL-005 Dashboard — **DONE**
- TL-006 Live Traffic Graph — **DONE**
- TL-007 Per-Process Traffic — **DONE** (collector + engine + verification +
  Applications-list UI)
- TL-007F Shutdown deadlock fix — **DONE**
- TL-008 Active Connections — **DONE**
- TL-009 SQLite History — **DONE**
- TL-010 Floating Widget — **DONE**
- TL-011 System Tray — **DONE**
- TL-012 Alerts — **DONE**
- TL-013 Settings — **DONE**
- TL-014 Stability & Performance Audit — **DONE**
- TL-015 Packaging / Installer — **DONE**
- TL-016 Localization / Persian UI — **DONE** (localization merged `9dc8335`; branding/About continuation done on `master`)
- TL-017 Performance Audit — **DONE** (merged into `master` at `21dabb9`)
- TL-018 Current-Day Hourly History — **DONE** (on `feature/tl018-hourly-history`, ready to merge on approval)
- TL-020 Connections Readability & Usability — **DONE** (on `feature/tl020-connections-readability`, ready to merge on approval)
- TL-021 Dashboard "Today at a Glance" + "Top App Now" — **DONE** (on `feature/tl021-dashboard-insights`)
- TL-022 History This Month + Today vs Yesterday + CSV Export — **DONE** (on `feature/tl022-history-insights`)
- TL-023 Release Hardening & Clean-Machine Validation — **DONE** (on `feature/tl023-release-hardening`)
- TL-024 Installer Localization & Packaging Polish — **DONE** (on `feature/tl024-installer-polish`)

## Active

- TL-028 (Alert threshold editing + Floating Widget quick control, v0.1.4, on
  `feature/winui3-migration`): two human-found functional defects, scoped strictly to
  those two. No alert backend, collector, monitoring or packaging changes; no new
  timer, poller or background worker.
  - **Alert thresholds visible and editable**: all five rule cards render their
    numeric threshold at a real height again (`ItemsWrapGrid` item height 104 → 120,
    auto rows above a trailing spacer; the old `*` row crushed the editor to ~6 px)
    and the editor is no longer gated on the rule's own enabled state, which all five
    rules ship as disabled. `ThresholdText`/`UnitIndex` still two-way, LTR digits,
    right-aligned, same units, same parse/validation/save mapping, so the backend
    reads the same persisted values.
  - **Floating Widget quick control in the shell**: a compact native `ToggleButton`
    (Segoe Fluent Icons glyph + localized label) in the top-left of the title bar,
    available on every page — not a navigation item, not a separate page, not buried
    in Settings. The title-bar grid is pinned left-to-right so the control stays
    physically top-left in both directions; the title keeps its own direction and
    alignment so the Persian title still sits against the navigation edge, clear of
    the caption buttons.
  - **One widget-enabled state**: a click calls `SetEnabled` (persists the existing
    setting, shows/hides the window) and the service's `EnabledChanged` /
    `IsVisibleChanged` events push the result back, so shell ↔ Settings ↔ widget
    cannot disagree and restart shows the persisted value. No second flag, no second
    service, no polling. Always On Top stays in Settings only.
  - **Human clarification honoured**: the mistaken `Floating Widget` navigation item
    and its dedicated page were removed (page and its unused description resource
    deleted); navigation is back to Dashboard / Applications / Connections / History /
    Alerts / Settings / About. The widget window and service are untouched.
  - **Quick action anchored in the physical top-left (`ebbf725`)**: the caption
    reserve moved from title-bar padding to a margin on the title, and the grid keeps
    no horizontal padding, so the control's offset depends only on the window's own
    left edge. Measured at 150% DPI: constant 29 physical px (12 DIP) across resize,
    maximize, restore and navigation collapse, and it does not mirror right in
    Persian. `ResolvePadding` unchanged.
  - **Duplicate Settings widget control removed**: human screenshot review of the
    Persian Settings page found the widget could be enabled from two places. The
    top quick card — card, `WidgetQuickToggle`, its `Toggled` handler and the two
    labels the code-behind filled — is deleted, not collapsed. One widget section
    remains (enable + Always On Top) and the sections below moved up with no blank
    card or gap. `WidgetToggleSync.Apply` went with the pair of switches it existed
    to feed; `Resolve` is still shared by both windows. Both remaining switches are
    views of the one `FloatingWidgetEnabled` state written only through
    `SetEnabled` — no second setting, service or lifecycle, still no polling. Always
    On Top stays in Settings only. The localization keys are still used by the shell
    control, the tray and the WPF views, so none was removed.
  - **Verified**: focused 145/145 (`TrafficLens.WinUI.Tests`), full suite 711/711
    (App 270 / Network 212 / Infrastructure 84 / WinUI 145), Release x64 build 0
    warnings / 0 errors. Runtime, one launch, `fa-IR`, PID 11208: one widget section
    on Settings, 29 px relLeft held through resize/maximize/restore/pane collapse,
    and all five live state paths (quick ON, quick OFF, Settings ON, Settings OFF,
    widget X) agreed in both directions and persisted each time.
  - **Status**: automated checks pass, human verification pending. No artifact
    produced; the v0.1.4 artifacts from `99397a4` remain superseded. Not merged,
    tagged, pushed or published.
  - **Known, not fixed here**: the Persian title still runs under the caption
    buttons on this machine because `AppWindow.TitleBar.LeftInset` and `RightInset`
    both report 0 here, leaving only the 16-DIP base reserve. This is the pre-existing
    zero-inset case that the separate DPI-aware fallback task covers; that fallback
    is **not** present at this HEAD and was deliberately not started, so this entry
    does not claim it fixed.

- TL-025 (Release Candidate 0.1.2, on `release/0.1.2`):
  - **Version bump**: 0.1.1 → 0.1.2 in `Directory.Build.props` (centralized source), `TrafficLens.iss` defaults, lifecycle validation assertions.
  - **Release candidate built**: installer (69 MB), portable ZIP (68.6 MB). Final SHA256: `9FABE44055AA9EE91E537ECFB365B2B5CECAED0D86BB1D3AE2EF230223E0079A`.
  - **Lifecycle validation**: 19/19 PASS (tests 1-5: checksum, install, launch, exit, startup). WDAC blocked uninstaller — accurately reported, not fabricated.
  - **Installer metadata**: ProductVersion=0.1.2, FileVersion=0.1.2, AppId unchanged. Installer title "TrafficLens 0.1.2".
  - **English installer GUI**: PASS (human visual verification).
  - **Persian installer GUI**: PASS (human visual verification) — RTL, task localization, Ready page, no mojibake, no clipping.
  - **Fixes applied**: title truncation (AppVerName), Persian task strings ({cm:} constant syntax).
  - **Status**: RC ready for merge/tag/publish approval.

## Completed

- TL-024 (Installer Localization & Packaging Polish, on `feature/tl024-installer-polish`):
  - **Bilingual installer**: English and Persian (Farsi) language support. Custom `packaging/Persian.isl` language file with RTL layout (`RightToLeft=yes`, LanguageID=$0429, CodePage=1256). Translated wizard messages for all primary installer pages (welcome, destination, install, completion, etc.). Language selection dialog at installer startup.
  - **Custom messages**: `AppRunningWarning` translated per language via `[CustomMessages]` section. Pascal Script updated to use `CustomMessage('AppRunningWarning')`.
  - **Metadata cleanup**: `AppPublisherURL` and `AppSupportURL` placeholder entries removed from `[Setup]` section. No valid official URL exists; placeholders removed rather than replaced with generics.
  - **Lifecycle validation**: 37/37 PASS — 5 new assertions (9.5–9.9): AppPublisherURL absent, AppSupportURL absent, stable AppId unchanged, English language present, Persian language present.
  - **Known limitations**: Some uncommon Inno Setup error messages (e.g., `ArchiveIncorrectPassword`, `ErrorRegCreateKey`) fall back to English. All primary wizard-flow messages are translated. Unsigned build. Upgrade test unavailable (no prior artifact).
  - **Constraints respected**: zero monitoring changes, zero database changes, zero collectors, zero ETW changes, zero polling/timer changes, zero application feature changes.

- TL-023 (Release Hardening & Clean-Machine Validation, on `feature/tl023-release-hardening`):
  - **Release lifecycle validation** (`scripts/tl023-release-lifecycle.ps1`): 31-point deterministic validation covering the full install → launch → exit → startup registration → uninstall → data preservation → reinstall → portable → checksum lifecycle. Uses isolated temp directories; cleans up after itself; preserves developer's real user data.
  - **Results**: 31/31 PASS — SHA256 verified, clean install (exit 0, directory created, Start Menu present), first launch (window renders, settings/DB initialize), graceful exit (WM_CLOSE, no orphans, no ETW), startup registration (default-off, write/read, no duplicate on reinstall), uninstall (binaries removed, Start Menu removed, settings.json preserved, trafficlens.db preserved), reinstall (data loads, settings intact, app launches), portable build (extracts, launches, uses shared LocalAppData, no orphans), installer metadata (ProductName, ProductVersion, FileVersion correct).
  - **Release pipeline**: `build-release.ps1` produces installer (69 MB), portable ZIP (68.6 MB), SHA256 sidecar — all verified. 509/509 tests pass, Debug + Release 0 warnings/0 errors.
  - **Known limitations**: unsigned build, installer English-only, placeholder project URLs, WDAC blocks installed copy in enterprise environments (published build works).
  - **Constraints respected**: zero new product polling loops, zero new collectors, zero new ETW sessions, zero DB/schema changes, zero monitoring/accounting changes, TL-017 optimizations untouched.

- TL-021 (Dashboard "Today at a Glance" + "Top App Now", on `feature/tl021-dashboard-insights`):
  - **Today at a Glance**: Three-card grid on Dashboard showing Download/Upload/Total bytes used today, reading from `ITrafficHistoryService.HistoryChanged` → `HistorySnapshot.Today` (`TrafficUsage`). Formatted via `DataSizeFormatter.Format()`. No new polling; reuses existing History pipeline. Handles unavailable history service and unavailable snapshots (shows "0 B").
  - **Top App Now**: Card below Today showing the process with highest current throughput (`ProcessSampleSelection.TopConsumer()`). Reads from `IProcessTrafficCollector.SamplesReady` event. Shows app name, total rate, download/upload rates. Idle state (no active traffic) shows localized "No active traffic" text. Permission-denied and unavailable states handled with appropriate localized warnings.
  - **DashboardViewModel**: Constructor extended with optional `ITrafficHistoryService?` and `IProcessTrafficCollector?` parameters (backward-compatible, DI-resolved). Event subscriptions (`HistoryChanged`, `SamplesReady`, `StatusChanged`) in constructor, unsubscribed in `Dispose()`. All updates gated by `_isActive`.
  - **MainWindow.xaml**: New sections inserted between tunnel hint and Active Adapter section. Three-card Today grid (Download/Upload/Total) + Top App card. All numeric content `FlowDirection="LeftToRight"` for RTL compatibility. `AutomationProperties.Name` on all cards.
  - **Localization**: 12 new keys in both `Strings.resx` and `Strings.fa-IR.resx` (TodayAtGlanceLabel, DownloadTodayLabel, UploadTodayLabel, TotalTodayLabel, TopAppNowLabel, TopAppApplicationLabel, TopAppCurrentLabel, TopAppNoDataLabel, TopAppPermissionDeniedLabel, TopAppUnavailableLabel, TopAppDownloadLabel, TopAppUploadLabel).
  - **Tests (+22 → 495/495)**: `DashboardInsightTests` — 7 Today tests, 11 Top App tests, 4 Lifecycle tests. All use `FakeHistoryService`, `FakeProcessCollector`, no live DNS or DB.
  - **Constraints respected**: No new timers/polling/ETW sessions/history polling; no new DB queries; no per-process metadata in hot path; no schema changes. All data from existing event pipelines.

- TL-022 (History This Month + Today vs Yesterday + CSV Export, on `feature/tl022-history-insights`):
  - **This Month range**: New `HistoryRange.ThisMonth` enum value using local calendar month semantics (first day of current month through current time, NOT rolling 30 days). `HistoryRangeCalculator.ToLocalDateRange` handles it with half-open `[monthStart, today+1)` using local calendar. `TrafficHistoryService` computes via bounded `SumDaily` over month range.
  - **Today vs Yesterday comparison**: Compares Today's usage against Yesterday's usage scaled to equivalent elapsed local-day interval via `DayFraction` (ratio of current time through local day). Shows Today total, Yesterday-at-this-time equivalent, absolute difference, and percentage. Safe zero-denominator handling. Hidden when no comparison data (yesterday zero, day fraction zero, or not Today range).
  - **CSV export**: `SaveFileDialog`, UTF-8 with BOM, header row (`Period,DownloadBytes,UploadBytes,TotalBytes`), invariant numeric values, deterministic column order. Uses same data as selected range (hourly for Today, daily for other ranges). Async file IO; IO error surfaces as localized non-crashing message box.
  - **HistoryViewModel**: Extended with ThisMonth selection, comparison properties, export command. `ApplySnapshot` handles ThisMonth chart slicing, `UpdateComparison` computes scaled yesterday equivalent.
  - **HistoryView.xaml**: This Month button in range bar, comparison border below chart (Today/Yesterday-at-this-time/Difference), Export CSV button with `AutomationProperties.Name`.
  - **Localization**: 9 new keys in both `Strings.resx` and `Strings.fa-IR.resx`.
  - **Tests (+14 → 509/509)**: `HistoryRangeCalculatorTests` (+4: ThisMonth range semantics), `HistoryViewModelTests` (+10: ThisMonth selection, comparison positive/negative/equal/zero-yesterday/hidden/day-fraction-scaling, localization).
  - **Constraints respected**: No new timers/polling/ETW sessions; no new DB queries or schema changes; no monitoring/accounting changes.

- TL-020 (Connections Readability & Usability, on `feature/tl020-connections-readability`):
  - **Hide Listeners filter** (`ConnectionsFilter.HideListeners`): Hides TCP listeners (`State == Listen`) and unconnected UDP entries (no remote endpoint) from the Connections list. Persisted via `ISettingsService` (`ConnectionsHideListeners` key), default `false`. Checkbox added to Connections page toolbar.
  - **Copy actions**: Right-click context menu on connection rows with Copy Local Endpoint, Copy Remote Endpoint, Copy Remote IP, Copy Process Name. Keyboard accessible, localized (en-US + fa-IR).
  - **Optional reverse DNS resolution** (`EnableReverseDns`): OFF by default, user-enabled, persisted via `ISettingsService` (`ConnectionsEnableReverseDns` key). Bounded DNS cache (TTL 30 min success / 5 min failure, max 1024 entries, 4 concurrent lookups, failure caching, cancellation-safe). Resolves remote IP → hostname for display as secondary info; raw IP always visible. Setting added to Settings page under General section.
  - **Accessibility**: Keyboard navigation, Escape dismiss, `AutomationProperties.Name` on all interactive elements, focus order.
  - **Performance**: Zero background cost when disabled; reuses existing `AdaptersChanged` events; no new timers/polls/workers; preserves TL-017 virtualization/diffing/visibility gating.
  - **Tests** (+3 → 191 App tests): `Filter_HideListeners_HidesTcpListenersAndUnconnectedUdp`, `HideListeners_Setting_PersistsAndRestores`, `EnableReverseDns_DefaultFalse_PersistsAndRestores`.
  - **GUI verified** (published single-file build): fresh profile onboarding, HideListeners toggle, EnableReverseDns toggle, copy actions, en-US/fa-IR/RTL, runtime language switch, all pages functional, graceful exit, no orphan process.

- TL-017 (performance audit, M12, merged into `master` at `21dabb9`):
  - **Phase 1 — Connections optimization** (commit `03c47a4`): in-place
    `ConnectionRowViewModel` updates keyed by `ConnectionKey`, delta diffing
    on native snapshots, per-refresh icon budget. Dedicated comparable probe
    (3 min Connections visible): CPU avg **15.31% → 6.65%**, CPU max
    **41.41% → 16.81%**, WS drift **+24.29 → +9.48 MB/3min**, Private drift
    **+23.26 → +8.00 MB/3min** (~57–66% lower).
  - **Phase 2 — Per-tick allocation reduction** (commits `213a887`, `303288e`):
    ~550 avoidable allocations/tick removed (~450 short-lived row strings, ~101
    double-created `ConnectionKey`); GC capture: Gen0 0.20/s, Gen1/Gen2 0,
    ~1.53 MB/s allocation, ~0.8% time in GC, bounded heap. **No CPU improvement
    claimed** (within noise).
  - **Phase 3 — Lazy page instantiation** (commits `cbc8aea`, `0db764e`): six
    page Views created on first navigation; internal mainVM+6-view block ~170 →
    ~70 ms (~100 ms pre-render removed). **Wall-clock NOT claimed faster**
    (environment-dominated; internal 830–875 ms / wall 1.36–1.39 s BEFORE vs
    internal 1.6–2.3 s / wall 2.7–3.5 s AFTER).
  - **Phase 4 — SQLite history optimization** (`737fb3f`): persistent background
    writer + prepared commands + `lifetime_totals` schema v2; flush avg/p95
    **7.4 ms/9 ms → 1.3 ms/2 ms** (~5.7x); QueryLifetime O(n)→O(1). **Migration
    back-fill fixed during final verification** — see "Defects found and fixed"
    below.
  - **Phase 5 — Idle tray optimization** (`843f343`): `SetActive` gating for
    hidden widget/hidden surfaces + dispose/recreate; allocation ~2.2 MB/s →
    ~0.5–1.1 MB/s, WS drift +4.7 → +2.4 MB/5min, threads 16 → 15. **Idle CPU
    avg 1.08% → 1.07% explicitly within noise — NOT claimed.**
  - **Phase 6 — Long-run soak** (`abaf129`, soak harness `scripts/tl017-soak.ps1`):
    60-min idle tray (CPU avg 0.97%, max 7.69%, WS ~240 MB flat +0.2 MB drift,
    Private ~120 MB, threads 14–17, handles 468–496, heap 12–14 MB) + 30-min
    Connections (CPU avg ~8.3%, WS +14.5 first 15m/+2.5 second 15m, Private
    +12.7/+5, handles 751–760). **"No leak was observed during the measured
    soak window"** — 60/30-minute soak, not a 24h soak.
  - **Correctness preserved:** monitoring accuracy, byte accounting, tunnel
    exclusion, ETW PID/start-time identity, history UTC timestamps, local-day/
    DST, alert semantics, polling intervals, packet payload policy, monitoring-
    only architecture all unchanged.
  - **Elevated ETW NOT measured** (non-elevated env; UAC not automatable):
    verified only `PermissionDenied` fallback, no crash, no orphan session.
    Documented as remaining manual validation.
  - **Final verification (2026-09-19):** 416/416 tests (212 Network / 41
    Infrastructure / 163 App), Debug + Release 0 warnings/0 errors, published
    single-file republished, `tl015-smoke.ps1` PASS, `tl016-verify.ps1` PASS,
    `tl017-connprobe.ps1` PASS on retry (first attempt was a transient UIA
    timing flake, diagnosed then re-run clean); packaging pipeline
    (`build-release.ps1`) succeeds at version 0.1.1 with no tag/bump; all TL-017
    benchmark harnesses runnable.
  - **Defects found and fixed during final verification:**
    1. `scripts/tl017-db-benchmark.ps1` (harness) — `dotnet run --no-build` on
       an unbuilt generated project + duplicate source file write; fixed and
       confirmed runnable (30×20 flush + query + prune).
    2. Schema v1→v2 migration (production data-correctness) — `Migrate()`
       created `lifetime_totals` but never seeded it, so upgrading a real
       v0.1.1 (schema v1) database would report Lifetime history 0 until new
       buckets arrived. Fixed by back-filling from the existing `daily_usage`
       sum; regression test
       `InitializeAsync_MigratesV1History_BackFillsLifetimeTotal`; `DATABASE.md`
       updated. This is the only production-code change made during Phase 7 and
       it was driven by a confirmed defect, not an optimization pass.
  - Docs: `docs/PERFORMANCE_AFTER.md` (authoritative BEFORE vs AFTER report),
    this status file, `TASKS.md`, `CHANGELOG.md`, link from
    `docs/PERFORMANCE.md`, `docs/DATABASE.md` (schema v2 + migration).
- TL-018 (current-day hourly history, M13, on `feature/tl018-hourly-history`):
  - **Core (`TrafficLens.Core/History`):** `HourlyUsagePoint` (immutable record
    struct: `StartUtc`, `EndUtcExclusive`, `LocalHour`, download/upload bytes)
    and `HourlyHistoryBuilder` — a pure static builder that buckets
    `TrafficHistoryBucket` samples into UTC-hour slots counted from the local
    day's midnight-UTC (`MidnightUtc(DateOnly, TimeZoneInfo)` =
    `ConvertTimeToUtc` of local `Unspecified` midnight). Slots are zero-filled,
    slots whose start ≥ "now" are excluded, the current partial hour is clamped
    to `EndUtcExclusive = nowUtc`, and slot labels use the local hour of the
    UTC slot start. DST-correct by construction: spring-forward → 23 slots with
    no skipped local label; fall-back → 25 slots whose duplicated local label
    maps to two distinct UTC slots; half-hour-offset zones (e.g. +05:30) use
    `Math.Floor((bucketStart − dayStart).TotalHours)` and produce 12 slots.
  - **Repository:** `ITrafficHistoryRepository.QuerySamplesAsync(startUtcInclusive,
    endUtcExclusive, ct)` added; `SqliteTrafficHistoryRepository` implements it as
    one bounded query
    (`WHERE bucket_start_utc >= $start AND bucket_start_utc < $end ORDER BY
    bucket_start_utc`). `EXPLAIN QUERY PLAN` regression test proves the built-in
    INTEGER PRIMARY KEY seek is used (no `SCAN`, no new index).
  - **Snapshot:** `HistorySnapshot` gains a 9th positional member `TodayHourly`
    (`IReadOnlyList<HourlyUsagePoint>`; `Unavailable` passes empty). The service
    captures `nowUtc` once, computes `dayStartUtc` via `HourlyHistoryBuilder.
    MidnightUtc`, runs the single bounded query, and builds the hourly series —
    no extra queries, no SQL in the App/VM layers.
  - **App:** `HistoryChartPoint` (label + bytes) replaced the daily-only point
    model in `HistoryBarChartControl.Points`; `HistoryViewModel` maps daily
    (`MM-dd` label) or hourly (`HH:00` label) series depending on the selected
    range and the presence of hourly data, and a new `ChartTitleLabel`
    ("Hourly Traffic" on Today, "Daily Traffic" otherwise) drives the chart
    title (localized en + fa-IR `HistoryHourlyTrafficLabel`; resx parity now
    162 keys each).
  - **Tests (+16):** 8 `HourlyHistoryBuilderTests` (zero-fill to now, slot
    attribution + partial-hour clamp, top-of-hour exclusion, local-midnight
    empty, out-of-window ignored, spring-forward 23-slot with skipped local
    label, fall-back 25-slot with duplicated label, +05:30 half-hour zone) using
    fixed-zone helpers or the real
    `TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time")`
    (verified 2026 DST days: 23h spring / 25h fall); 3
    `SqliteTrafficHistoryRepositoryTests` (half-open range returns only in-range
    rows oldest-first, empty range returns none, query-plan avoids SCAN); 1
    `TrafficHistoryServiceTests.HourlySeries_InSnapshot_ReflectsCommittedBuckets`;
    `HistoryViewModelTests` rewritten to 10 (hourly labels, zero-hour bars,
    chart-title data-independent switching, culture switch, HasData);
    `AlertServiceTests`/`AlertEngineTests`/`LocalizationResourceTests` updated.
  - **Verification:** **432/432 tests** (212 Network / 53 Infrastructure / 167
    App), Debug + Release 0 warnings / 0 errors; the published single-file build
    passed `scripts/tl018-verify.ps1` (see "Verified").
  - **Scope kept additive:** no schema change, no new index, no new timers, no
    change to write cadence/collectors/`lifetime_totals`, TL-017 optimizations
    untouched.
- TL-019 (First-Run Get Started + Contextual Clarity, on `feature/tl019-get-started`):
  - **First-run Get Started overlay:** Lightweight, dismissible panel that auto-shows
    on a brand-new profile (no prior `settings.json`), explains TrafficLens in
    concise skimmable sections (What TrafficLens does, Applications/Administrator
    privilege, VPN & tunnels exclusion from system total, System tray behavior,
    Floating widget, Alerts, Start with Windows, Language switcher), and persists
    completion via existing `ISettingsService` (`HasCompletedOnboarding` key).
    Manual reopen available from About ("Get Started") and Settings ("Get Started")
    without resetting persisted state.
  - **Contextual tunnel hint:** Small non-alarming hint near the Dashboard system
    total ("System total excludes tunnel adapters to avoid double-counting.") that
    appears only when an active tunnel adapter (`NetworkAdapterKind.Tunnel` with
    `IsUp=true`) is present. Driven by existing `AdaptersChanged` events — no new
    timers, no polling, no collector, no monitoring changes.
  - **Administrator UX:** Onboarding explains Applications page requires elevation
    (Windows ETW kernel provider); the app never auto-elevates, restarts elevated,
    changes ETW session behavior, or fakes process data.
  - **Localization (en-US + fa-IR):** Exact resource parity (18 new keys added to
    both `Strings.resx` and `Strings.fa-IR.resx`, 180 total); RTL layout correct;
    runtime language switch updates open onboarding panel.
  - **Accessibility:** Keyboard navigation, focus order, Escape dismiss,
    `AutomationProperties.Name` on all interactive elements; panel focuses
    dismiss button on open.
  - **Performance:** Zero continuous background cost — no new timers/polls/workers,
    no duplicate subscriptions; reuses existing adapter events.
  - **Tests (+23 → 455/455):** `OnboardingViewModelTests` (10), Dashboard tunnel
    hint (5), About/Settings/MainViewModel reopen (6), `JsonSettingsService`
    `SettingsFileExisted` (2), resource parity extended.
  - **Verification:** Debug + Release 0 warnings / 0 errors; published single-file
    build GUI verified (fresh profile auto-show, dismiss persistence, restart no
    auto-show, About/Settings manual reopen, en/fa-IR/RTL, runtime language
    switch, Dashboard/Applications/History/Alerts/Settings/Widget/Tray functional,
    graceful exit, no orphan process).
  - **Core (`TrafficLens.Core/History`):** `HourlyUsagePoint` (immutable record
    struct: `StartUtc`, `EndUtcExclusive`, `LocalHour`, download/upload bytes)
    and `HourlyHistoryBuilder` — a pure static builder that buckets
    `TrafficHistoryBucket` samples into UTC-hour slots counted from the local
    day's midnight-UTC (`MidnightUtc(DateOnly, TimeZoneInfo)` =
    `ConvertTimeToUtc` of local `Unspecified` midnight). Slots are zero-filled,
    slots whose start ≥ "now" are excluded, the current partial hour is clamped
    to `EndUtcExclusive = nowUtc`, and slot labels use the local hour of the
    UTC slot start. DST-correct by construction: spring-forward → 23 slots with
    no skipped local label; fall-back → 25 slots whose duplicated local label
    maps to two distinct UTC slots; half-hour-offset zones (e.g. +05:30) use
    `Math.Floor((bucketStart − dayStart).TotalHours)` and produce 12 slots.
  - **Repository:** `ITrafficHistoryRepository.QuerySamplesAsync(startUtcInclusive,
    endUtcExclusive, ct)` added; `SqliteTrafficHistoryRepository` implements it as
    one bounded query
    (`WHERE bucket_start_utc >= $start AND bucket_start_utc < $end ORDER BY
    bucket_start_utc`). `EXPLAIN QUERY PLAN` regression test proves the built-in
    INTEGER PRIMARY KEY seek is used (no `SCAN`, no new index).
  - **Snapshot:** `HistorySnapshot` gains a 9th positional member `TodayHourly`
    (`IReadOnlyList<HourlyUsagePoint>`; `Unavailable` passes empty). The service
    captures `nowUtc` once, computes `dayStartUtc` via `HourlyHistoryBuilder.
    MidnightUtc`, runs the single bounded query, and builds the hourly series —
    no extra queries, no SQL in the App/VM layers.
  - **App:** `HistoryChartPoint` (label + bytes) replaced the daily-only point
    model in `HistoryBarChartControl.Points`; `HistoryViewModel` maps daily
    (`MM-dd` label) or hourly (`HH:00` label) series depending on the selected
    range and the presence of hourly data, and a new `ChartTitleLabel`
    ("Hourly Traffic" on Today, "Daily Traffic" otherwise) drives the chart
    title (localized en + fa-IR `HistoryHourlyTrafficLabel`; resx parity now
    162 keys each).
  - **Tests (+16):** 8 `HourlyHistoryBuilderTests` (zero-fill to now, slot
    attribution + partial-hour clamp, top-of-hour exclusion, local-midnight
    empty, out-of-window ignored, spring-forward 23-slot with skipped local
    label, fall-back 25-slot with duplicated label, +05:30 half-hour zone) using
    fixed-zone helpers or the real
    `TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time")`
    (verified 2026 DST days: 23h spring / 25h fall); 3
    `SqliteTrafficHistoryRepositoryTests` (half-open range returns only in-range
    rows oldest-first, empty range returns none, query-plan avoids SCAN); 1
    `TrafficHistoryServiceTests.HourlySeries_InSnapshot_ReflectsCommittedBuckets`;
    `HistoryViewModelTests` rewritten to 10 (hourly labels, zero-hour bars,
    chart-title data-independent switching, culture switch, HasData);
    `AlertServiceTests`/`AlertEngineTests`/`LocalizationResourceTests` updated.
  - **Verification:** **432/432 tests** (212 Network / 53 Infrastructure / 167
    App), Debug + Release 0 warnings / 0 errors; the published single-file build
    passed `scripts/tl018-verify.ps1` (see "Verified").
  - **Scope kept additive:** no schema change, no new index, no new timers, no
    change to write cadence/collectors/`lifetime_totals`, TL-017 optimizations
    untouched.
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

- TL-003: Download/upload rate calculation:
  - `NetworkSpeedCalculator` — cumulative-counter deltas / real monotonic elapsed
    (QPC), never an assumed 1 s interval.
  - `SpeedRateTracker` — per-adapter baselines with re-baseline rules (first
    sample, reset/wrap/decrease, zero elapsed, disappearance/replacement); no
    fake spikes.
  - `SpeedSampleReady` raised with real rates; `GetCurrentSamples()` returns them.
  - `NetworkTrafficAggregator.AggregateRates` — system "Internet Total",
    tunnel-excluding by default; per-adapter views keep tunnel/VPN traffic.
  - `DataRateConverter` (Core) — B/s, KB/s, MB/s, Kbps, Mbps, Gbps for the UI.
  - Extension of ADR-009 policy to rates; ADR-010 (monotonic clock).
  - Tests added (calculator, tracker, conversions, aggregate, collector rates).
- TL-004 (audit + targeted fix):
  - Verified all existing TL-002/TL-003 adapter detection satisfies the checklist:
    Ethernet/Wi-Fi/OpenVPN TAP/DCO/Hyper-V/VMware/all-virtual visible;
    ID/name/description/type/status, up/down, gateway awareness, default preferred
    selection, all-adapters mode (down adapters kept), per-adapter rates,
    connect/disconnect events, GUID-unique identity — all covered.
  - Gap found and fixed: OpenVPN TAP/DCO drivers register as
    `HighPerformanceSerialBus` (type 53) and were classified `Unknown`.
    Added description-aware `NetworkAdapterKindMapper.Map(type, description)`
    (tap-windows/openvpn/wintun/wireguard → Tunnel;
    virtual/vmware/hyper-v/vethernet/virtualbox → Virtual).
    `AdapterFilter.IsMonitored` relaxed for Unknown-type adapters with
    recognized tunnel/virtual descriptions so WireGuard (or similar) remains
    visible if its O/S type is Unknown. `DefaultAdapterSelector` uses the
    description-aware overload so TAP stays non-default.
- TL-005: Dashboard connected to real TL-002/TL-003 data:
  - `DataRateFormatter` (Core) — adaptive B/s/KB/s/MB/s and Mbps, culture-aware
    decimal separator; unit symbols intentionally untranslated (ADR-011).
  - `DashboardViewModel` + `AdapterListItemViewModel` (MVVM, DI, no code-behind
    networking). Consumes `INetworkTrafficCollector` + `INetworkAdapterProvider`;
    subscribes to `SpeedSampleReady` and adapter-change events; all updates
    marshalled to the WPF Dispatcher; `IDisposable` unsubscribes all handlers.
  - Prominent Download/Upload/Total cards with **real live rates** from
    `NetworkTrafficAggregator.AggregateRates` (ADR-009/ADR-010 policy — tunnels
    excluded from the system total, never double-counted).
  - Active/preferred adapter card (name, kind, Connected/Disconnected) via the
    existing default-selector — never the first adapter.
  - Compact adapter list (friendly name, kind, up/down, current ↓/↑ rates);
    VPN/TAP/DCO/Wi-Fi-Direct virtuals stay visible with their own per-adapter
    rates, distinct from the system aggregate.
  - Connection states handled without crashing: no network, disconnect,
    reconnect, VPN-only host (honest zero/absent totals + per-adapter tunnel
    rates), collector temporarily without a sample.
  - Localization: en + fa-IR strings for Dashboard labels, adapter kinds, and
    connection states; RTL-safe layout (rate texts forced LTR, FlowDirection
    follows culture).
  - `TrafficLens.App.Tests` (net8.0-windows, WPF-ready): aggregate→VM mapping,
    adapter→VM mapping, no-network, reconnect-after-disconnect, VPN-only honesty,
    culture-switch reformatting, localization resource existence.
- TL-006: Live traffic graph (completes M2):
  - Core graph layer (`TrafficLens.Core/Graph`) — no WPF dependencies, raw
    bytes/second only:
    - `TrafficGraphPoint` — timestamped raw down/up rates (no formatted strings).
    - `TrafficSampleBuffer` — bounded (5.5 min retention / 1320 samples) thread-safe
      ring buffer; dedupe rejects same-poll duplicate timestamps; age-out by
      wall-clock timestamp; `Slice(window, now)` is non-mutating so range
      switching never clears history; preserves real timestamps (no assumed 1 s
      interval), so dropouts are rendered as honest gaps.
    - `AdaptiveGraphScale` — single shared Y max for both series; immediate
      growth on spikes; hysteretic shrink (sustained low < 35% for 2 consecutive
      updates) to avoid flicker; 2 KB/s floor prevents divide-by-zero at zero
      traffic (ADR-012).
    - `GraphTimeRange` — 30 s / 60 s / 300 s enumeration.
  - `TrafficGraphControl` (App, `Controls/`) — light native WPF `FrameworkElement`
    drawing both series as `StreamGeometry` in `OnRender`; grid + axis labels via
    `DataRateFormatter`; forces `FlowDirection=LeftToRight` so the timeline is
    always oldest-left → newest-right even under fa-IR.
  - `DashboardViewModel` — feeds the existing `AggregateRates` aggregate into the
    buffer once per poll (dedupe handles multi-adapter `SpeedSampleReady` bursts);
    `SelectGraphRangeCommand` + per-range selection flags; localized graph labels.
  - `MainWindow.xaml` — "Live Traffic" section with range buttons (30 s / 1 m / 5 m),
    download/upload legend, and the graph control.
  - Localization: en + fa-IR for live-traffic heading, range and series labels.
  - Tests: graph buffer/scale suites in `TrafficLens.Network.Tests`; VM graph
    appends/dedupe/range-slice/no-network/zero/large tests in
    `TrafficLens.App.Tests`; localized graph keys in resource tests.

- TL-007 (per-process traffic, collector milestone — M4 backend):
  - `ProcessTrafficCollectorStatus` (Stopped/Starting/Running/PermissionDenied/Failed),
    extended `IProcessTrafficCollector` (Status/LastError), richer
    `ProcessTrafficSample` (pid + start-time identity, name, path, icon-available,
    byte totals, monotonic-window rates, Timestamp) — all in Core.
  - `WindowsEtwProcessTrafficCollector` (Network/Process) — real-time ETW kernel
    session (`TraceEventSession`, `NetworkTCPIP`); eight Tcp/Udp IPv4/IPv6 event
    handlers mapping payload PID + size into `NetworkTransferEvent` (never the raw
    header PID — DPC-computed receive completions would mis-attribute to
    System/Idle); ~1 s snapshot loop raising `SamplesReady`; non-elevated hosts
    report `PermissionDenied` + `LastError` without crashing and without forcing
    UAC.
  - `ProcessTrafficAccountingEngine` — per-instance buckets keyed by
    `ProcessInstanceId = (pid, process start time)`; monotonic sliding-window rates
    (never assumed 1 s); metadata resolve/rekey once the true start time is known;
    **PID-reuse isolation** (mismatched start time stops attribution to the old
    bucket and starts a fresh instance); unknown/unresolvable processes kept in
    their own `<unknown pid N>` bucket, never merged into another process; idle
    prune 120 s, hard cap 4096 buckets (oldest-LastSeen eviction); hot path is
    allocation-free (`CollectionsMarshal.GetValueRefOrAddDefault`); no per-event
    UI work, no logging, no SQLite.
  - `WindowsProcessMetadataProvider` — guarded `System.Diagnostics.Process` reads
    (name/path/icon/start time), PID-reuse detection by start-time mismatch
    (>2 s tolerance), never throws.
  - `ProcessProtocolTotals` — per instance Tcp/Udp × Received/Sent and IPv4/IPv6 ×
    Received/Sent with the tested invariant `Total = Tcp + Udp = IPv4 + IPv6`.
  - DI: `IProcessTrafficCollector` singleton registered in
    `NetworkServiceCollectionExtensions`; provider/session owned by the Network
    layer; document VPN/tunnel semantics (owner-attributed app bytes, not
    interface bytes; ADR-009/010 aggregate policy unchanged).
  - Real-time kernel provider requires elevation; detail + rationale in
    `docs/NETWORK_COLLECTION.md` (TL-007 section) and ADR-013.

- TL-007 (per-process traffic, Applications-list UI milestone — M4 UI):
  - `ProcessSampleSelection`/`ProcessSortKey` (Core) — pure sample filtering and
    seven sort keys (total/download/upload rate, downloaded/uploaded/total bytes,
    name) with deterministic tie-breaking (name, start time, PID); presentation
    concern, never mutates collector state.
  - `DataSizeFormatter` (Core) — binary unit totals (B/KB/MB/GB, culture-aware,
    negatives clamped); totals are technical notation, matching ADR-011.
  - `ApplicationsViewModel` + `ProcessRowViewModel` (App) — consumes
    `IProcessTrafficCollector.SamplesReady` once per second, keeps
    per-instance rows keyed by `ProcessInstanceId` (same PID + different start
    time = distinct rows), updates rows in place (no flicker/re-add), exposes
    top-consumer (now/download/upload) cards, sort + search (name substring
    case-insensitive + PID prefix), localized status text, and per-row state from
    the sample's `IsRunning` flag (`Running`/`Exited`/unknown).
  - Privilege UX: non-elevated hosts show a permission banner with
    `StartMonitoringCommand`, `Failed`/`Stopped` show monitoring controls; the
    only elevation path is an explicit `Restart-as-Administrator` command
    (`Process.Start` `runas` + shutdown) — **the app never auto-elevates**
    (ADR-014).
  - `ProcessIconResolver` (App) — shell32 `SHGetFileInfo` P/Invoke
    (`SHGFI_ICON | SHGFI_LARGEICON`) + `Imaging.CreateBitmapSourceFromHIcon` +
    `DestroyIcon`, frozen fallback, bounded FIFO cache (128) with max 8
    extractions per refresh — icons resolved on the UI thread only; no
    `System.Drawing` dependency.
  - `MainWindow` — Dashboard / Applications navigation buttons (nav row), content
    hosted in a `ContentControl` switched by `MainViewModel.ShowDashboardCommand`
    / `ShowApplicationsCommand`; `IProcessTrafficCollector` started at startup in
    `App.xaml.cs` (network collector + process collector).
  - Localization: all Applications keys added to `Strings.resx` (en) and
    `Strings.fa-IR.resx` (fa, valid UTF-8).

- TL-009 (SQLite history, M6):
  - Core history domain (`TrafficLens.Core/History`), no WPF/OS dependencies:
    - `HistoryRange` (Today/Yesterday/Last7Days/Last30Days/Lifetime),
      `TrafficUsage` (immutable record struct: download/upload/total),
      `DailyUsagePoint`, `HistorySnapshot` (immutable; `For(range)` derives
      per-range totals; `Unavailable(lastError)`).
    - `HistoryRangeCalculator` — half-open local-date ranges using
      `TimeZoneInfo.ConvertTimeFromUtc` → `DateOnly`, so DST + local midnight are
      correct; ranges verified by tests (Today `(today, today+1)`,
      Last7Days `(today-6, today+1)`, Last30Days `(today-29, today+1)`).
    - `TrafficHistoryAccumulator` — extends `NetworkCounterSample` DELTAS into
      per-UTC-minute buckets; first observation per adapter is baseline-only;
      non-negative deltas only; positive deltas from all eligible adapters summed
      per minute; `DrainCompleted` (full 60 s buckets) vs `DrainAll` (includes the
      open minute, duration clamped 1..60) for shutdown flush.
    - Contracts `ITrafficHistoryRepository` + `ITrafficHistoryService` (cached
      immutable `HistorySnapshot`; `HistoryChanged`; SQL never runs on the UI thread).
  - Infrastructure (`TrafficLens.Infrastructure/History`):
    - `SqliteTrafficHistoryRepository` — SQLite via `Microsoft.Data.Sqlite`;
      schema v1 (`PRAGMA user_version`), tables `traffic_samples`
      (`bucket_start_utc INTEGER PK, bucket_duration_seconds, download_bytes,
      upload_bytes`) and `daily_usage` (`local_date TEXT PK, download_bytes,
      upload_bytes`); `journal_mode=WAL`, `busy_timeout=5000`, `Pooling=false`
      (deterministic handles; keeps tests from holding file locks); every append
      is one transaction guarded by `INSERT OR IGNORE` + `changes()==1` before the
      `daily_usage` upsert so an app restart can never duplicate history;
      `daily_usage` kept forever, raw samples pruned older than 90 days on startup;
      `QueryDailyAsync` / `QueryLifetimeAsync` / `PruneRawSamplesBeforeAsync`.
    - `TrafficHistoryService` — subscribes the existing collector's
      `CounterSampleReady` (never starts a second NIC polling loop), keeps an
      adapter-kind map refreshed on `AdaptersChanged`, excludes tunnels by default
      (same rules as the ADR-009/010 aggregate), flushes *completed* minute buckets
      every 30 s in a background loop, re-baselines on resets/reconnects/reboots so
      measurements are never fabricated, and flushes everything on stop (graceful
      shutdown preserves at most the current open minute).
    - `HistoryServiceCollectionExtensions.AddHistoryServices(dbPath)`.
  - `AppPaths` — `DataDirectory = Root\data`, `DatabaseFile`,
    `EnsureDirectories` creates the `data` folder.
  - App History page:
    - `HistoryViewModel` — Five-range buttons (Today/Yesterday/Last 7 Days/Last 30
      Days/Lifetime) bound via `SelectRangeCommand`; Download/Upload/Total cards
      and a localized "No history yet" overlay when `Lifetime.TotalBytes == 0`;
      unavailable-banner when storage failed (`HistoryUnavailableLabel` +
      `LastError`), all from the cached immutable snapshot via `HistoryChanged`.
    - `HistoryView` (XAML + code-behind DI) hosting a native
      `HistoryBarChartControl` (`FrameworkElement`, `OnRender`, no chart library):
      bars always oldest-left → newest-right regardless of `FlowDirection`;
      Today/Yesterday render a single bar, 7 days → 7 bars, 30 days/Lifetime →
      30 daily bars; tooltips show `date → DataSizeFormatter` totals.
    - `MainViewModel.ShowHistoryCommand` + localized nav label; `MainWindow`
      History host alongside Dashboard/Applications/Connections;
      `App.xaml.cs` registers the ViewModel/View and starts the history service
      (fire-and-forget with error logging) after the network collector.
  - Localization: `HistoryLabel`, `HistoryDailyTrafficLabel`, `TodayLabel`,
    `YesterdayLabel`, `Last7DaysLabel`, `Last30DaysLabel`, `LifetimeLabel`,
    `HistoryNoDataLabel`, `HistoryUnavailableLabel` added to `Strings.resx` (en)
    and `Strings.fa-IR.resx`; download/upload/total label reuse.
- TL-010 (floating widget, M7):
  - `FloatingWidgetViewModel` — thin event-driven VM over the existing rate
    pipeline (`SpeedSampleReady`/`NetworkChanged`/`AdaptersChanged`, no own poll
    loop or timer; ADR-009/010 aggregate via `NetworkTrafficAggregator`), formats
    Download/Upload/Total via `DataRateFormatter` (units stay LTR under RTL),
    localized title/labels, `TogglePinCommand` + `CloseWidgetCommand`,
    dispatcher-marshalled updates, IDisposable (mirrors `DashboardViewModel`).
  - `FloatingWidgetWindow` — 280×110 frameless (`WindowStyle=None`,
    `ResizeMode=NoResize`), `ShowInTaskbar=False`, dark theme from the shared
    `DarkTheme.xaml` brushes, drag by empty area (`DragMove`), pin toggle + hide
    buttons bound to commands, values forced LTR; no second taskbar app.
  - `FloatingWidgetService` (singleton) — owns the single widget instance;
    `Show`/`Hide`/`Toggle`/`RestoreIfEnabled`; repeat show only activates (no
    duplicates); widget close is hide-only (`Closing` cancelled); `Dispose`
    detaches the cancel handler and really closes the window so shutdown is never
    pinned open.
  - `WidgetPositionHelper.Clamp` — pure multi-monitor-position recovery: union of
    monitor work areas, negative virtual-screen coordinates preserved (secondary
    monitor left of primary), off-screen/disconnected-monitor clamping, oversized
    window folded to top-left; real areas from `SystemParameters.VirtualScreen*`.
  - Settings via the existing `ISettingsService` (single `settings.json`):
    `FloatingWidgetEnabled` (startup restore), `FloatingWidgetAlwaysOnTop`
    (default on), `FloatingWidgetLeft`/`FloatingWidgetTop`.
  - Main UI: header toggle button (`Show`/`Hide Floating Widget` exchange on
    `IsVisibleChanged`), `MainViewModel.ToggleFloatingWidgetCommand` +
    `FloatingWidgetToggleLabel`, `MainWindow.Closing` hides the widget then normal
    shutdown proceeds (ADR-015/TL-007F — no `Environment.Exit`, no hidden window
    keeping the process alive); started via `App.xaml.cs`
    (`RestoreIfEnabled` after services start).
  - Localization: `FloatingWidgetLabel`, `AlwaysOnTopLabel`,
    `ShowFloatingWidgetLabel`, `HideFloatingWidgetLabel` in en + fa-IR;
    Download/Upload/Total reuse existing keys.
- TL-011 (system tray, M7):
  - `SystemTrayService`/`ISystemTrayService` (App) — a single WinForms
    `NotifyIcon` over a plain `<FrameworkReference
    Include="Microsoft.WindowsDesktop.App.WindowsForms" />` (`UseWPF` kept, no
    `UseWindowsForms` ⇒ no WinForms global usings / no CS0104). One icon created
    once, disposed only on real exit; tooltip `TrafficLens`; no ghost; hidden
    `WindowsForms10..._ad1` message window as the in-process proxy.
  - Runtime-drawn 32×32 icon (dark rounded square + `#4FC3F7`/`#26A69A`
    chevrons on `#1E1E2E`, readable 16–32 px; `GetHicon`+`FromHandle`,
    `DestroyIcon` on dispose).
  - Tray menu relabeled in place on culture change (never recreated): Open
    TrafficLens / Show-Hide Floating Widget / Always on Top (checkable) /
    separator / Exit. Always on Top drives the same widget pin state; the widget
    pin tooltip/accessibility binds the shared `AlwaysOnTopLabel` (TL-010
    polish).
  - `TrayBehavior` (pure) — `MinimizeToTray`/`CloseToTray` (default true),
    `TrayCloseNoticeShown`; `ResolveCloseAction` → Exit|HideToTray; once-only
    close notice.
  - `ApplicationExitCoordinator` — single idempotent `RequestApplicationExit()`
    (latch → dispose tray+widget → `Shutdown()`, Dispatcher-safe); no
    `Environment.Exit`/`Process.Kill`; collectors/history/DI disposed via
    container. `App.xaml` `ShutdownMode="OnExplicitShutdown"`.
  - `MainWindow`: `Closing` → `ResolveCloseAction` (Exit calls the coordinator;
    HideToTray cancels+Hides+balloon), `StateChanged` minimize→Hidden,
    `OpenRequested` restores the singleton window. `MainViewModel` exposes the
    two tray settings + localized labels in a minimal `…` options popup (TL-013
    owns the full settings page).
  - Localization en + fa-IR: `OpenTrafficLensLabel`, `ExitLabel`,
    `MinimizeToTrayLabel`, `CloseToTrayLabel`, `TrayCloseNoticeBalloon`; internal
    tray technical name stays LTR.
- TL-012 (alerts, M8):
  - Core alert domain (`TrafficLens.Core/Alerts`, no WPF/OS dependencies):
    `AlertType` (HighDownloadSpeed / HighUploadSpeed / DailyDownloadLimit /
    DailyUploadLimit / DailyTotalLimit with `IsSpeedRule`/`IsDailyUsageRule`),
    `AlertConfig` (immutable record; all **5 rules disabled by default** with
    suggested thresholds 50 MB/s, 20 MB/s, 50 GB, 20 GB, 100 GB; default 5 min
    cooldown), `AlertEvent`, `AlertSignal`, `AlertEngine` (pure, gate-locked,
    clock + `TimeZoneInfo` injected), `AlertHistoryBuffer` (session-only,
    capacity 100, newest-first).
  - Speed semantics (tested): triggers on **upward crossing only**;
    `rate ≥ threshold` consumes the armed crossing and signals only when
    `now ≥ CooldownUntilUtc`; dropping below re-arms but does **not** clear the
    cooldown, so "remain above" never repeats and flapping yields ≤ one alert
    per cooldown window (no spam).
  - Daily semantics (tested): at most once per **local calendar day**
    (DST-safe); the last-triggered local date is persisted
    (`alerts.lastTriggered.dailyDownload|dailyUpload|dailyTotal`) on trigger and
    restored on construction so a **same-day restart (or crash) never re-fires**;
    the next local day re-arms.
  - Pipeline never touches SQL: speed is evaluated from
    `INetworkTrafficCollector.GetCurrentSamples()` +
    `INetworkAdapterProvider.GetAdapters()` via the existing
    `NetworkTrafficAggregator.AggregateRates` (ADR-009/010 policy) on
    `SpeedSampleReady`; daily is evaluated from the cached
    `ITrafficHistoryService.GetSnapshot()` on `HistoryChanged`. History
    unavailable ⇒ daily rules suspend silently (logged once), speed continues.
  - App (`TrafficLens.App/Services`): `AlertSettings` (flat settings keys,
    Load/Save + Load/SaveTriggeredDates, invariant culture), `AlertNotification`
    + `AlertMessageFormatter` (structured, re-localized on culture change),
    `IAlertService`/`AlertService` (singleton; owns the engine; subscribes both
    pipelines; raises `AlertRaised`; logs every trigger; clean `Dispose`).
  - Tray delivery: `ISystemTrayService.ShowAlert` +
    `SystemTrayService.ShowAlert` (RunOnUi, EnsureCreated, Warning balloon, 8 s,
    dropped+logged only if the tray is unavailable);
    `BalloonTipClicked → OpenRequested` (the same singleton-restore handler
    verified by TL-011).
  - Alerts page: `AlertsViewModel`, `Views/AlertsView.xaml` (+DI code-behind),
    `MainViewModel` Alerts navigation, `MainWindow` Alerts host, `App.xaml.cs`
    DI registrations + `AlertRaised → ShowAlert` wiring.
  - Localization en + fa-IR: `AlertsNavLabel`, `AlertsTitleLabel`,
    `AlertsNoAlertsLabel`, `AlertsCountFormat`, `AlertTitle`, `AlertType*` ×6,
    `AlertMsg*` ×5; `LocalizationResourceTests.RequiredKeys` extended.
- TL-013 (settings, M8):
  - Full Settings page (`SettingsView.xaml` + DI code-behind; `NavSettings`
    button + host in `MainViewModel`/`MainWindow`): **General** (language combo,
    start-with-Windows, start-minimized, minimize/close-to-tray), **Floating
    Widget** (enable, always-on-top, show/hide buttons that reuse the TL-010/
    TL-011 service), **Alerts** (all 5 rule rows from TL-012 with enable checkbox,
    threshold text field, unit combo, and the shared cooldown bounded 1–1440).
  - Staged-save vs immediate-apply (ADR-021): numeric fields/dropdowns commit
    on **Save** — validate (cooldown 1–1440, threshold > 0) → single logical
    persist (language, startup registration, tray, widget, alerts) → runtime
    apply (`SetCulture`, enable/disable HKCU Run, `RefreshConfig`,
    widget Show/Hide, `SetAlwaysOnTop`) → `RefreshFromSettings`; tray checkboxes
    apply **immediately**; Reset stages defaults behind a Yes/No MessageBox;
    `SavedNotice` + dirty tracking.
  - `AlertRuleViewModel` per rule: threshold typed in the displayed unit and
    persisted as invariant bytes; unit combos KB/s/MB/s/GB/s/TB/s (speed) and
    MB/GB/TB (daily) re-parse on load; inline validation error text.
  - Widget always-on-top semantics fixed (verified live): `SettingsViewModel.Save`
    calls the new `IFloatingWidgetService.SetAlwaysOnTop(bool)` so an unchecked
    setting persists and clears `WS_EX_TOPMOST` across restart; the tray menu
    keeps `ToggleAlwaysOnTop`.
  - Startup registration: `IStartupRegistrationService`/
    `StartupRegistrationService` — HKCU `…\CurrentVersion\Run` value
    `TrafficLens` = fully-quoted exe path (+ optional ` --minimized`), removes
    only its own value name, preserves sibling values; `--minimized` CLI arg
    starts hidden to tray (logged `Starting hidden to system tray`).
  - `JsonSettingsService` hardening: partial file merges defaults, unknown keys
    preserved on Save, malformed JSON falls back to defaults (all verified on
    the real page with a real Save click).
  - Localization en + fa-IR for the full Settings surface; required-keys test
    extended. Tests: `SettingsViewModelTests`, `JsonSettingsServiceTests`,
    fakes (`SetAlwaysOnTop`), localization keys.
  - **382/382 tests** (App 138 / Network 206 / Infrastructure 38); Debug +
    Release 0 warnings / 0 errors.
  - Real-Windows GUI verification (`scripts/tl013-verify.ps1`, Release,
    blocks A–I, all green): partial-merge/unknown-key/malformed recovery; en→fa→en
    combo switch with restart persistence; tray immediate-apply + close-to-tray
    via Win32; widget enable/topmost-off/hide/show + restart; page-configured
    256 KB/s threshold → exactly one real alert download → Reset via the native
    `Reset to Defaults` MessageBox (found + answered `IDYES` over Win32) →
    defaults restored, history SQLite DB mtime untouched; HKCU Run create/
    quoted/`--minimized`/own-value-only removal with sibling preservation;
    hidden `--minimized` start with tray alive; combined restart persistence;
    graceful exit, no orphan ETW sessions.
- TL-014 (stability & performance audit, M9):
  - **Root cause — DashboardViewModel event-per-adapter dispatch storm:**
    `SpeedSampleReady` fires once per adapter per second; with 5 adapters
    `OnSpeedSample` was calling `RefreshRates` 5×/s, producing N layout passes,
    N `INotifyPropertyChanged` storms, and N graph-buffer appends per second —
    all for scalar data that changes at most once per second. Fixed by adding
    a `_refreshPending` flag and `CoalesceRefresh()` method that merges all
    per-adapter events into a single `RefreshRates` call per second via
    `Dispatcher.BeginInvoke`.
  - **Root cause — ConnectionsViewModel always-on dispatch:** every ~1 s poll
    dispatched `OnConnectionsChanged` to the UI thread and forced row rebuilds
    even when the Dashboard page was active and Connections was invisible. Fixed
    by adding `_isActive` flag with `SetActive(bool)` called from
    `MainViewModel.SelectPage`; when inactive, connection data is buffered in
    `_pendingConnections` without dispatching; on activation, data is refreshed
    immediately from the provider cache.
  - **Root cause — WindowsConnectionProvider native table enumeration:**
    `RunLoopAsync` called `EnumerateOnce()` unconditionally every ~1 s,
    executing four native P/Invoke table reads (`GetExtendedTcpTable` × 2 +
    `GetExtendedUdpTable` × 2) plus process resolution for ~159 connections,
    all needlessly when the Connections page was hidden. Fixed by adding
    `SetPollingEnabled(bool)` to `IConnectionProvider` and
    `WindowsConnectionProvider`; when polling is paused, `RunLoopAsync` skips
    `EnumerateOnce()` entirely.
  - **Default deactivation:** `MainViewModel` constructor calls
    `Connections.SetActive(false)` since Dashboard is the default page.
  - **Pre-fix evidence:** main thread 50% on-CPU (22,449 samples / 45 s) via
    dotnet-trace Speedscope; N events/s dispatch storm confirmed.
  - **Post-fix evidence:** main thread 1.9% on-CPU (113 samples / 60 s);
    harness F2 soak 30 min: avg 3.43%, max 9.88% (threshold 15%).
  - **Additional regression tests:** SingleInstanceGuard (6 tests),
    FileLoggerProvider retention (3 tests), SpeedRateTracker sleep-gap test.
  - **397 tests** (App 145 / Network 212 / Infrastructure 40); Debug +
    Release 0 warnings / 0 errors.
  - ADR-022 (idle CPU optimization: coalescing, page-visibility gating,
    polling pause, default deactivation).
- TL-015 (packaging / installer, M10):
  - **Release pipeline** (`scripts/build-release.ps1`): resolve a .NET 8 SDK
    (`--list-sdks` guard), restore → Release build (0 warnings / 0 errors) →
    all tests → single-file self-contained win-x64 publish → validation
    (exe size, PE machine 0x8664, product/file version) → portable ZIP →
    Inno Setup compile → SHA-256 checksum file. Artifacts land under
    `artifacts\` (gitignored); nothing binary is committed.
  - **Single-file publish:** `PublishSingleFile` + native-libs self-extract +
    compression, `InvariantGlobalization=false` (fa-IR satellite bundled inside
    the exe), `AssemblyName=TrafficLens` so the process/exe is `TrafficLens`.
  - **Installer** (`packaging/TrafficLens.iss`): Inno Setup 6.7.3,
    `PrivilegesRequired=lowest`, installs to `{localappdata}\Programs\TrafficLens`,
    stable AppId, Start-Menu shortcut (desktop icon off by task), `Excludes:
    "*.pdb"` (no PDBs shipped), post-install launch, `[UninstallDelete]` only
    `dirifempty`. Running-app detection via a `[Code]` WMI
    (`Win32_Process` + `WbemObjectSet.Count`) prompt in `wpReady`; never
    force-kills; Inno file-in-use dialog is the second safety net.
  - **Metadata:** `Directory.Build.props` sets `IncludeSourceRevisionInInformationalVersion=false`
    (clean `0.1.0` ProductVersion, no git hash) and
    `AssemblyTitle=TrafficLens Network Monitor` (FileDescription); `app.manifest`
    asInvoker / Win10+ / PerMonitorV2 / longPathAware.
  - **Verification (real Windows):** published-exe smoke
    (`scripts/tl015-smoke.ps1`) and installed-app verification
    (`scripts/tl015-installed-app.ps1`) all PASS (window, dashboard, graph,
    Applications, Connections, History, Settings persistence, floating widget,
    tray, fa-IR + en-US, single-instance, graceful exit, no orphan process, no
    orphan ETW). Install 0.1.0 → uninstall (data preserved) → reinstall (data
    preserved, hashes identical) → upgrade to 0.1.1 (single uninstall entry,
    DisplayVersion 0.1.1) → final 0.1.0 over 0.1.1 all exit 0. Final installer
    SHA-256 `0E9F1186…6D0B`.
  - **User data** (`%LOCALAPPDATA%\TrafficLens\settings.json` + `data\` +
    `logs\`) is installer-untouched by design; manual removal documented,
    install dir and shortcuts fully cleaned on uninstall.
  - Docs: `docs/PACKAGING.md` (full packaging doc), ADR-023. 397 tests;
    Debug + Release 0 warnings / 0 errors.
- TL-016 (localization / Persian UI, M11):
  - **No hard-coded UI strings:** graph "now" label now data-bound — new
    `TrafficGraphControl.NowLabel` DependencyProperty (default `"now"`), bound in
    `MainWindow.xaml` to `Dashboard.GraphNowLabel` (already localized).
  - **Localized status/error detail surfaces:** Applications
    (`PermissionDeniedDetailLabel`, `MonitoringFailedDetailLabel`), Connections
    (`ConnectionsErrorDetailLabel`) and History (`HistoryErrorDetailLabel`) views
    show localized detail text; raw English provider/collector messages (e.g.
    "Elevation required", "boom", "disk full") are never rendered — they continue
    to drive the error state and remain in the app log.
  - **Culture-aware History chart dates:** `ToString("MM-dd",
    CultureInfo.CurrentCulture)` shows Persian-calendar dates under fa-IR.
  - **Key parity enforced:** en + fa-IR resx both at 146 keys, identical key sets;
    `LocalizationResourceTests.RequiredKeys` extended; endonyms (English / فارسی)
    and brand intentionally untranslated.
  - **398 tests** (App 146 / Network 212 / Infrastructure 40); Debug + Release 0
    warnings / 0 errors.
  - **Verified on the published single-file build** (`scripts/tl016-verify.ps1`):
    en-US labels render, fa-IR content scan (nav + dashboard + History page in
    Persian, no English leak), en-US History no-leak, graceful exit, no orphan
    process / ETW. TL-015 smoke regression still passes on this branch build.
  - Docs: ADR-024. Merged into `master` via `--no-ff` (`9dc8335`) after user
    approval; `v0.1.0` tag untouched at `c1f677a`.
- TL-016 continuation (product polish / branding foundation, M11a, on `master`):
  - **Icon pipeline:** `scripts/generate-icons.ps1` draws the brand glyph (rounded
    `#1E1E2E` square, cyan `#4FC3F7` down-arrow, teal `#26A69A` up-arrow — the
    TL-011 tray art) at 16/24/32/48/64/128/256 and writes
    `assets/branding/TrafficLens.ico` (PNG-encoded multi-size ICO, verified by
    `BrandAssetsTests`) + 256/128 PNGs. Replaceable by re-running the generator.
  - **Single wiring point:** `TrafficLens.App.csproj` declares the ICO once
    (`<ApplicationIcon>` + `<Resource>` pack URI). Consumed by MainWindow icon,
    FloatingWidget title glyph (14px), tray icon (`SystemTrayService` loads the
    32px frame with the runtime-drawn fallback preserved), About page and the
    installer (`SetupIconFile={#BrandIcon}`, `VersionInfo*` setup metadata).
    `packaging/placeholder.ico` removed.
  - **About page:** 7th localized nav item (`WrapPanel` nav — no overflow at
    640px min width), `AboutViewModel`/`AboutView`, DI singletons; brand
    identity, version, runtime, OS, display language, data/settings/logs paths.
    Paths render LTR under RTL (existing per-control convention).
  - **Diagnostics:** `DiagnosticsInfo.Build` pure `Label: Value` builder (unit
    tested); Copy-diagnostics via `Clipboard.SetText` never throws; Open-logs
    folder shell action with parent fallback. Version from
    `AssemblyInformationalVersionAttribute` — avoids IL3000 under single-file
    publish.
  - **Localization:** en + fa-IR resx both **161 keys**, identical sets
    (`RequiredKeys` extended); brand name untranslated (ADR-024/025).
  - **Tests:** `AboutViewModelTests`, `MainViewModelTests` (nav incl. About),
    `BrandAssetsTests`; **410 total** (App 158 / Network 212 / Infrastructure 40).
  - **Verified:** Debug + Release 0 warnings / 0 errors; full suite green;
    About reachable in en + fa-IR with no English leak on the published build.
  - Docs: `docs/BRANDING.md`, ADR-025, `PACKAGING.md` branding section,
    `TASKS.md`, `CHANGELOG.md`.
- TL-008 (active connections, M5):
  - Core (`TrafficLens.Core`):
    - `ConnectionInfo` extended — nullable remote endpoint, `ConnectionAddressFamily`,
      process start-time identity (`ProcessStartTimeUtcTicks`), `ExecutablePath`,
      `IconAvailable`, `Timestamp`; new `ConnectionProtocol`, `ConnectionState`,
      `ConnectionAddressFamily` enums.
    - `ConnectionKey` — stable identity `(Protocol, AddressFamily, LocalAddress,
      LocalPort, RemoteAddress?, RemotePort?, ProcessId)` used for in-place row updates.
    - `EndpointFormatter` — culture-safe, always-LTR `address:port` formatting; the
      remote endpoint renders **empty** for a listening/unconnected socket
      (unspecified `0.0.0.0`/`::` + port 0) instead of a misleading peer (ADR-016).
    - `Selection/ConnectionSelection` — pure, non-mutating `ConnectionFilter`
      (All/Established/Listening/Tcp/Udp/Ipv4/Ipv6), `ConnectionFiltering.Matches`/
      `MatchesSearch`, and `ConnectionSort` (Default/Process/ProcessId/Protocol/State/
      Local/Remote) with deterministic tie-breaks (name, PID, local, remote).
  - Native collection (`TrafficLens.Network/Connections`):
    - `NativeConnectionTableReader` — `GetExtendedTcpTable` (`TCP_TABLE_OWNER_PID_ALL`)
      + `GetExtendedUdpTable` (`UDP_TABLE_OWNER_PID`), IPv4 and IPv6; parses the 4-byte
      little-endian entry-count header and per-row layouts (TCPv4 24 B, TCPv6 56 B,
      UDPv4 12 B, UDPv6 28 B); network→host port byte order; 64 KB initial buffer grown
      on `ERROR_INSUFFICIENT_BUFFER`.
    - `ConnectionTableParser` — pure static parsers over the native buffers, unit-tested
      with synthetic payloads (no live table required).
    - `ConnectionProcessResolver` — bounded cache (TTL 3 s, capacity 512, FIFO eviction,
      negative caching) over `IProcessMetadataProvider`, keyed by full
      `ProcessInstanceId`; never throws.
    - `WindowsConnectionProvider` — ~1 s off-UI poll loop; a partial-table failure is a
      warning (successful tables are kept), a total failure keeps the last good snapshot
      and sets `LastError`, and any success clears it; `StopAsync` uses
      `ConfigureAwait(false)` (ADR-015).
  - App:
    - `ConnectionsViewModel` + `ConnectionRowViewModel` — dispatcher-marshalled
      `ConnectionsChanged` handler, in-place row updates (rebuild only when the key
      sequence changes), per-refresh icon budget, error banner, empty state, Filter +
      Address-Family + Sort combo boxes and a search box.
    - `ConnectionSortOption` / `ConnectionFilterOption` (localized option records);
      `ConnectionsView` (XAML + code-behind DI); `MainWindow` Dashboard/Connections
      navigation; `App.xaml.cs` registers the ViewModel/View and starts the provider.
    - Localization: en + fa-IR keys for headers, filters, sort, TCP states, empty/error,
      and unknown process; endpoints remain LTR under RTL.

## Verified

- `dotnet build TrafficLens.sln`: **Success, 0 warnings, 0 errors** (Debug and Release).
- **Automated tests:** 432/432 passed (`TrafficLens.Network.Tests` 212,
  `TrafficLens.App.Tests` 167, `TrafficLens.Infrastructure.Tests` 53).
- **TL-018 rewrite verification** (`scripts/tl018-verify.ps1`, published
  single-file build, real host; settings.json backed up/restored; blocks A–E):
  - **A) en-US History page:** default Today renders "Hourly Traffic" title; all
    5 range buttons (Today/Yesterday/Last 7/30 Days/Lifetime) present; clicking
    Yesterday switches the title to "Daily Traffic" with "Hourly Traffic"
    absent; back to Today restores "Hourly Traffic".
  - **C) Resize:** `MoveWindow` 1280×780 then 900×560 — app healthy, Today
    hourly title survives, no crash.
  - **D) Data chain:** ~40 s of real HTTP downloads (13–20 iterations); after a
    completed minute + one 30 s flush and a graceful WM_CLOSE exit, a
    read-only copy of `%LOCALAPPDATA%\TrafficLens\data\trafficlens.db` showed
    138 `traffic_samples` rows within [local-midnight-UTC, nowUtc) and
    **`daily_usage` Today == Σ(today's traffic_samples) exactly**
    (164,317,489 B down / 28,456,609 B up) — the hourly-capable storage chain
    reconciles.
  - **B) fa-IR History page:** default Today shows the Persian hourly title;
    5 localized range buttons; Yesterday shows the Persian daily title; **no
    English chart titles leaked**.
  - **E) Graceful exit:** WM_CLOSE exits both instances promptly; no orphan
    process.
  - **Honest coverage note:** hourly bar *pixels* (custom `OnRender`) have no
    UIA tree and are not asserted; the hourly aggregation math is unit-tested
    and the underlying data chain is verified exactly at the storage layer.
- **TL-017 performance verification** (`feature/tl017-performance`, Release,
  non-elevated): dedicated Connections 3-min probe BEFORE 15.31%→AFTER 6.65%
  CPU avg (57%+ lower); WS drift +24.29→+9.48 MB/3min; Private drift
  +23.26→+8.00 MB/3min. Benchmarks + GC counters + 60/30-min soak harnesses
  all runnable. `tl015-smoke.ps1` PASS, `tl016-verify.ps1` PASS,
  `tl017-connprobe.ps1` PASS (final). Full report: `docs/PERFORMANCE_AFTER.md`.
- **Elevated ETW long-run profiling is NOT measured** — environment cannot
  automate UAC; remains manual validation (see `docs/PERFORMANCE_AFTER.md`).
- **TL-014 stability & performance harness** (`scripts/tl014-stability.ps1`,
  Release, real host, non-elevated):
  - **F1 warm-up + idle (60 s):** dashboard/connections/history/GC counts pass;
    WS +8 MB, handles +6, threads +9 — PASS.
  - **F2 idle CPU soak (30 min):** max single-core CPU in 30 s windows
    **avg 3.43%, max 9.88%** — far below the 15% threshold — PASS.
    Pre-fix the same soak peaked ~20%+; post-fix the main thread is ~1.9%
    on-CPU (the remaining work is one dashboard Arrange pass per second).
  - **F3 memory leak (10 min soak):** WS +5.8 MB, Private +15.5 MB, handles -3,
    threads -9 — PASS.
  - **F4 interaction stress:** 50 navigation, 25 widget toggle, 25 window show/
    hide, 10 restart cycles — PASS (no crash, no exception, clean exit).
  - **CPU attribution (dotnet-trace Speedscope):** pre-fix main thread 22,449
    on-CPU samples / 45 s (~50%); post-fix 113 samples / 60 s (~1.9%). The
    perf spikes were UI-thread layout/property-changed storms from the per-
    adapter `SpeedSampleReady` events and the connections provider poll loop
    (ADR-022 explains all three root causes and the fixes).
- **TL-012 real Windows alerts GUI verification** (Release build,
  `scripts/tl012-verify.ps1`, real host; settings are read once per process
  startup, so each block relaunches from freshly written settings):
   - **A) Speed alert, threshold 256 KB/s, cooldown 10 s:** a controlled
     `1Gb.dat` download crossed the threshold and produced **exactly one**
     `Alert triggered: HighDownloadSpeed` line; the sustained multi-sample
     download produced no repeat (no spam while above); after traffic dropped
     below and the cooldown elapsed, a second download re-triggered **once**
     (re-arm semantics); the tray path stayed healthy (zero
     "Alert notification dropped", app alive).
   - **B) Daily total usage limit, threshold 10 MB:** a daily trigger fired
     exactly once after Today usage crossed the threshold (history flush
     ~30 s); `alerts.lastTriggered.dailyTotal` was persisted as today's local
     date (`yyyy-MM-dd`); a **same-day restart produced no repeat** (restored
     date holds). Next-local-day re-arm is covered by the engine unit tests.
   - **C) Notification while hidden:** with the main window hidden
     (minimize-to-tray), a speed alert still fired and was delivered to the
     tray (no "notification dropped"); the tray icon remained and the app
     stayed alive. Balloon click → restore routes through the tray
     `OpenRequested` handler already verified live in TL-011 — a real balloon
     click cannot be automated against the Windows 11 XAML tray.
   - **D) Regression:** default (all alerts disabled) launch renders every
     page (dashboard/graph/apps/connections/history/alerts), collectors +
     history run with deltas recorded and nothing fired; `CloseToTray=false`
     close exits gracefully with no orphan ETW session; 2 lifecycle cycles
     clean (no lingering process, no ETW orphans).
   - **Honest coverage notes:** balloon text/click are OS-rendered; delivery is
     asserted via the wired `AlertRaised → ShowAlert` path; once-per-day,
     next-day re-arm, DST day identity, cooldown and no-spam semantics are
     purely logical and covered by `AlertEngine` unit tests.
- **TL-009 real Windows history verification** (`--history` mode, live host, throwaway
   temp DB — never the user's `%LOCALAPPDATA%` DB):
   - Pipeline ran end-to-end: live collector → accumulator → minute buckets →
     SQLite; a real 20 MB `speed.cloudflare.com` download was recorded as
     **Today = 20,182,568 B down / 79,655 B up** (20 MB requested + realistic
     counter-delta overhead), matching the observed peak rate of ~5.78 MB/s.
   - Yesterday / 7d / 30d / Lifetime consistent; `daily_usage` series spans
     exactly 30 local days (2026-08-17 → 2026-09-15) with today's row populated.
   - **Restart idempotency proven:** a second service instance started against the
     same database over a fresh collector reported the identical Lifetime
     (`unchanged: true`) — the `INSERT OR IGNORE` + `changes()==1` guard prevents
     double-counting across restarts/crashes.
   - DB footprint after two sessions + 20 MB of traffic: **16,384 bytes**.
- **TL-010 real Windows floating-widget verification** (Release build, Win32
  `EnumWindows`/`PostMessage`/`SetWindowPos` scripting, 3 launch-close cycles):
  - **Startup restore:** widget window appeared at launch when
    `FloatingWidgetEnabled=true` (3/3 cycles).
  - **Move + position persistence:** `SetWindowPos` moved the widget to
    (400, 300); after hiding the widget and restarting, the saved
    `FloatingWidgetLeft`/`FloatingWidgetTop` matched the moved position.
  - **Widget close = hide only:** WM_CLOSE to the widget hides only the widget
    window; the main window remains visible and responsive.
  - **Main window close exits cleanly:** WM_CLOSE to the main window exits the
    process promptly within 15 s (all 3 cycles); no shutdown hang.
  - **No orphan ETW sessions:** `logman query -ets` confirmed no TrafficLens
    sessions remain after each cycle.
  - **Restart restore:** relaunched with `enabled=true` → 2 windows appeared
    (main + widget).
  - **fa-IR coexistence:** widget launches under fa-IR culture with correct
    titles; culture-set log line confirmed.
- **TL-011 real Windows system-tray GUI verification** (Release build,
  `scripts/tl011-verify.ps1`, `scripts/tl011-debug.ps1` +
  `scripts/tl011-shelldump.ps1`/`tl011-uiaprobe.ps1`/`tl011-dblclk.ps1`/
  `tl011-keynav.ps1` diagnostics):
  - **Tray icon created:** WinForms NotifyIcon message window
    (`WindowsForms10.Window.0.app.<hash>_r3_ad1`, app-owned) present at startup
    and while hidden.
  - **Minimize-to-tray:** `WM_SYSCOMMAND SC_MINIMIZE` → main hidden, process
    alive, tray icon still present, network collector + history service running.
  - **History accumulates while hidden:** with the app hidden, a live download
    produced WAL/mtime activity in `data\trafficlens.db` (SQLite keeps a single
    16 KB page; WAL write confirmed via db-wal growth / mtime change ~25 s).
  - **Singleton restore model:** same main HWND survives minimize/hide (no
    second instance).
  - **Close-to-tray:** `WM_CLOSE` → main hidden, process alive,
    `TrayCloseNoticeShown=True` persisted once; a second close leaves it True and
    keeps the app alive.
  - **CloseToTray=false → real exit:** fresh launch with the flag false →
    `WM_CLOSE` exits promptly (<15 s) through the coordinator; no orphan ETW
    session after exit.
  - **3 lifecycle cycles:** launch → minimize → close-to-tray → teardown × 3 —
    no ghost processes, no ETW orphans, tray icon recreated each run.
  - **Honest coverage note:** tray double-click restore and the tray-menu Exit
    cannot be synthesized against the Windows 11 XAML tray from this session
    (no `SysPager`/`ToolbarWindow32` child, UI Automation finds no
    "TrafficLens" tray element, Win+B tray keyboard nav unreachable); both are
    unit-tested (`TrayBehavior`/`ApplicationExitCoordinator`) and wired through
    the same `OpenRequested`/`ExitRequested` handlers verified here via
    minimize/close and the `CloseToTray=false` pipeline.
  - **Verification caught a real bug:** the `CloseToTray=false` X path merely
    closed the window without calling the exit coordinator, leaving the process
    alive under `OnExplicitShutdown`; fixed by calling
    `RequestApplicationExit()` in the `MainWindow.Closing` exit branch (see
    ADR-019).
- **TL-008 real Windows verification** (`--connections` mode, non-elevated, live host):
  - **TrafficLens `--connections`**: 112 connections (78 TCP / 34 UDP, 99 IPv4 /
    13 IPv6), 26 established / 29 listening; `udpWithRemote = 0` and
    `unknownProcess = 0`. An in-process `TcpListener` on `127.0.0.1` was observed as a
    `Tcp / Ipv4 / Listen` row owned by the verification process; a `curl` download was
    attributed one `Established` `Tcp / Ipv4` row (`local → 162.159.140.220:443`) with
    the correct PID and process name.
  - **Cross-check vs native MIB source (`netstat -ano`)**: TrafficLens TCP state
    histogram (`TimeWait 32 / Listen 29 / Established 21 / CloseWait 5`) matched
    `netstat` (31 / 28 / 21 / 5) at the same moment; TrafficLens UDP count (34) matched
    `netstat` UDP (34) exactly.
  - **`Get-NetTCPConnection` difference explained**: that cmdlet reported ~22 extra
    TCP rows in a synthetic `Bound` state that do **not** appear in `netstat` nor in the
    `TCP_TABLE_OWNER_PID_ALL` MIB table our provider reads — i.e. a cmdlet-side
    convenience state, not data we drop. Targeted listener row matched the native view
    exactly (`127.0.0.1:18888`, remote `0.0.0.0:0`, `Listen`, correct owning PID).
  - **GUI smoke** (non-elevated): log shows
    `Connection provider started (IP Helper tables, poll interval 00:00:01)`, no
    connection-provider exceptions during polling, ETW session absent before/after, and
    no leftover process; the process collector's elevation warning is expected and
    non-fatal.
- **TL-007 Applications UI real Windows GUI verification** (Release build, live
  network traffic, elevated + non-elevated runs):
  - Elevated run (app pid 5048): ETW session `TrafficLensProcessTrace` reported
    **Running** with buffers written; app survived ~20 MB/s-scale transfers
    (curl + PowerShell WebClient artifacts ≈ 14.1 MB, 16.6 MB, 20 MB); log shows
    `Process traffic collector running (elevated ETW kernel network session)`;
    graceful `CloseMainWindow` → `TrafficLens exiting` → `Process traffic
    collector stopped` → ETW session gone from `logman query -ets` (no orphan
    kernel session after graceful shutdown).
  - Non-elevated run (pid 7812): app started, network (dashboard) collector ran,
    process collector logged `cannot start: permission denied (Enabling the ETW
    kernel network provider requires an elevated (Administrator) process.)` →
    `PermissionDenied` banner path — no crash, no auto-UAC, and no ETW session
    created (`logman`: "Data Collector Set was not found").
- **TL-007 per-process real verification** (elevated verification console, live
  traffic against `https://speed.cloudflare.com/__down`, `--process` mode):
  - Collector turned `Running` (elevated) and attributed real traffic to the two
    apps it launched — `curl.exe` (6,236,307 B down / 674 B up) and
    `powershell.exe` (4,013,430 B down / 388 B up), all TCP/IPv4, with the
    protocol totals invariant holding (`Total = Tcp + Udp = IPv4 + IPv6`).
  - Two sequential `curl` instances (distinct PIDs and start times) appeared as
    **two separate buckets** with independent totals (6,236,307 vs 1,555,415 B) —
    per-instance attribution of the same executable, not per-name.
  - Bounded: 11 samples observed (hard cap 4096), ~2.6 MB managed memory growth
    over the whole run; clean `StopAsync` with no orphaned ETW session.
  - Non-elevated path verified separately: `Status = PermissionDenied` with
    `LastError` "Enabling the ETW kernel network provider requires an elevated
    (Administrator) process." — no crash, no forced UAC.
- **TL-006 real Windows GUI verification** (Release build, live network activity against
  `https://speed.cloudflare.com/__down`, UIA snapshots + Win32 resize):
  - App launched cleanly (log: startup → culture en-US → MainWindow shown → collector
    started); no errors/exceptions anywhere in the structured log.
  - "Live Traffic" section rendered with three range buttons (Last 30 seconds / Last 1
    minute / Last 5 minutes), download + upload legend, and the graph control.
  - Under live traffic: rates updated continuously on the cards (0.01 Mbps → 10.68 Mbps
    download / 1.27 MB/s adaptive while downloading 10 MB); after the download finished,
    rates decayed back to idle — the graph follows the aggregate stream.
  - Range buttons invoked via UIA (Last 5 minutes → Last 30 seconds): no crash, no
    exception, history retained.
  - Perssian switch (فارسی) → all graph labels/range strings render in fa-IR, app stays
    responsive; switch back to English restored en strings; settings.json restored to
    `{"language":"en-US"}`.
  - Window resized 800×700 and 1200×500 via Win32 `MoveWindow`: control re-renders,
    app stays `Responding=True`, no exceptions.
  - Clean shutdown: `CloseMainWindow` → log line "TrafficLens exiting", process exited.
- **TL-005 real Windows GUI verification** (Release build, live network activity against
  `https://speed.cloudflare.com/__down`, snapshots via UI Automation):
  - Download card: **0 B/s → 844.31 KB/s (6.92 Mbps)** → decaying to 32.74 KB/s across
    three snapshots while traffic flowed; upload/total changed in lockstep
    (real, changing data — never placeholders).
  - Active adapter section rendered (W-Fi up + gateway selected as default).
  - Adapter list showed all monitored adapters: Wi-Fi, Bluetooth PAN (Ethernet),
    OpenVPN TAP, OpenVPN DCO, Wi-Fi Direct virtuals — VPN/TAP/DCO **visible**.
  - English dashboard: labels "Dashboard / Download / Upload / Total / Active
    Adapter / Network Adapters / Connected / Disconnected" rendered correctly.
  - Persian (fa-IR, launched with `settings.json`: language fa-IR): culture set to
    fa-IR (log), Persian labels rendered, RTL-active; rates show fa decimal
    separator (`27٫68 KB/s`); no crash; clean exit ("TrafficLens exiting" in log).
  - Clean shutdown confirmed: `CloseMainWindow` → process exits, log line
    "TrafficLens exiting". No update storm (1 sample/s, scalar-only re-render).
- **Real Windows rate verification** (TL-003), concurrent native + collector run:
  - Collector window-mean Wi-Fi: **803,647 B/s** down / **18,423 B/s** up (10.02 s).
  - Native `Get-NetAdapterStatistics` delta: **1,120,590 B/s** down / **26,086 B/s**
    up (14.57 s, longer overlapping window).
  - Same magnitude/ordering/adapter; exact equality not expected (ADR-010).
- **TL-004 real verification** (live adapter classifier output):
  - OpenVPN TAP-Windows → **Tunnel** (was Unknown; type 53 + description heuristic).
  - OpenVPN DCO → **Tunnel**.
  - Wi-Fi Direct Virtual Adapter → **Virtual** (was Wireless; now type-accurate).
  - Wi-Fi (Intel AX201, up, gateway) → Wireless, default selected.
  - Bluetooth PAN → Ethernet, down, all adapters visible.
- Prior verification (TL-002): cumulative counters matched native statistics;
  default adapter = Wi-Fi (up + gateway, non-tunnel).

## Build

- .NET 8 SDK 8.0.425 at `C:\dotnet`.
- Command: `C:\dotnet\dotnet.exe build TrafficLens.sln`

## Tests

- `tests/TrafficLens.Network.Tests` — xUnit, 212 tests, all passing (incl. 22
  per-process accounting-engine tests, 11 selection/sort tests, 12 data-size
  formatter cases, metadata-provider tests, the TL-008 connection parser /
  key / selection / endpoint-formatter suites, and the TL-014
  SingleInstanceGuard tests).
- `tests/TrafficLens.App.Tests` — xUnit (net8.0-windows, WPF), 237 tests, all passing
  (incl. 6 dashboard-graph tests, 11 dashboard-insight tests (TL-021), 16 Applications-ViewModel tests, the TL-008
  Connections-ViewModel tests (+3 for HideListeners/EnableReverseDns), 10 TL-018
  History-ViewModel tests, 8 TL-010 FloatingWidget-ViewModel tests, 10 TL-010
  position-clamp tests, 10 TL-011 TrayBehavior tests, 4 TL-011
  ApplicationExitCoordinator tests,
  17 TL-012 AlertEngine tests + 3 alert-buffer tests, 10 AlertService tests,
  4 AlertsViewModel tests, 6 TL-014 regression tests for CPU optimizations,
  the TL-017 Connections in-place-update / allocation regression tests,
  and resource keys).
- `tests/TrafficLens.Infrastructure.Tests` — xUnit, 60 tests, all passing (TL-009:
  HistoryRangeCalculator, TrafficHistoryAccumulator, SqliteTrafficHistoryRepository
  over throwaway temp databases, TrafficHistoryService with fake collector/provider;
  TL-014 FileLoggerProvider retention tests; TL-017 schema v1→v2 migration
  back-fill regression test; **TL-018** `HourlyHistoryBuilder` DST/hourly suite +
  `QuerySamplesAsync` range/plan tests + service hourly-snapshot test;
  **TL-020** `DnsResolverService` bounded cache/concurrency/failure tests;
  **TL-022** `HistoryRangeCalculator` ThisMonth range semantics suite).
- `tests/TrafficLens.Network.Verification` — console harness; run with
  `dotnet run --project tests/TrafficLens.Network.Verification` (adapter),
  `-- --process` (per-process, elevated or non-elevated),
  `-- --connections` (active connections, non-elevated; set `TL_VERIFY_PORT` for a
  fixed listener port), or `-- --history` (TL-009 history, throwaway DB + live traffic).

## Known Issues / Not Started

- A hard process kill (`taskkill /F`) leaves the kernel ETW real-time session
  Running until stopped explicitly (`logman stop "TrafficLensProcessTrace" -ets`);
  graceful close does not leak the session.
- Per-adapter tunnel rates are published; only the system aggregate excludes them
  by default (`includeTunnels: true` to include on VPN-only hosts). Because the
  system history follows that same policy, a **VPN-only host records ~zero history**
  (tunnel bytes are never attributed to the system totals). Documented in
  `docs/NETWORK_COLLECTION.md` + ADR-009/017.
- History chart shows daily bars for 7/30/Lifetime; Today shows hourly bars
  (TL-018); a finer-than-hourly view for the current day remains a possible
  future refinement (raw minute samples are retained 90 days).
- History records system/global totals only; per-process and per-connection history
  are out of scope (TL-007/TL-008 are live-only).
- Active-connection state is live-only (no history) and report raw IP endpoints; no
  reverse DNS in TL-008 (deferred).
- Range buttons do not show an explicit "selected" highlight; the selected range is
  visually implied by the plotted window. (Future polish.)

## Resolved

- **Periodic idle-CPU spikes (TL-014).** The application no longer pegs CPU
  during idle soak: three independent sources were found and fixed (event-per-
  adapter dashboard dispatch storms, always-on ConnectionsViewModel
  dispatches, and unconditional native connection-table polling) plus startup
  default-deactivation of the hidden page. Harness F2 (30-min soak, 15%
  single-core max in 30 s windows) dropped from ~20%+ peaks to
  **avg 3.43% / max 9.88%**. See ADR-022 for full analysis.
- **Lingering `TrafficLens.App` after graceful window Close (TL-007F).** Root
  cause: `WindowsNetworkTrafficCollector` disposes on the WPF dispatcher thread via
  `StopAsync().GetAwaiter().GetResult()`; `await loop;` without
  `ConfigureAwait(false)` captured the `DispatcherSynchronizationContext`, re-posting
  the continuation to a dispatcher blocked in `GetResult()` → `StopAsync` never
  resumed and `App.OnExit` never returned. Fixed with `await
  loop.ConfigureAwait(false)` (ADR-015); verified 3× non-elevated launch→close +
  elevated ETW-running→close, all exiting promptly with the previously-missing
  `Network traffic collector stopped` now logged and no orphaned session/process.

## Git Commit

- TL-018 (current-day hourly history) on `feature/tl018-hourly-history`
  (not merged):
  - `7a972d1` — `feat: add current-day hourly history with DST-safe UTC hour aggregation (TL-018)`
  - `90bd64b` — `docs: document TL-018 current-day hourly history and finalize TL-017 merge status`
- TL-017 (performance audit): merged into `master` at `21dabb9`; the branch
  commits remain listed below for reference: <br>
  - `c709e2a` — baseline: capture pre-optimization performance files
  - `03c47a4` — perf: optimize Connections page with in-place row updates and delta diffing (TL-017 Phase 1)
  - `213a887` — perf: cut per-tick allocations in Connections rows (TL-017 Phase 2)
  - `303288e` — test: Connections per-tick allocation regression tests (TL-017 Phase 2)
  - `cbc8aea` — perf: lazy page Views + startup stage instrumentation (TL-017 Phase 3)
  - `0db764e` — test: dashboard lazy-view regression test (TL-017 Phase 3)
  - `737fb3f` — perf: persistent SQLite writer, prepared commands, lifetime_totals schema v2 (TL-017 Phase 4)
  - `843f343` — perf: gate hidden-surface refresh on SetActive (TL-017 Phase 5)
  - `abaf129` — chore: add TL-017 soak harness (TL-017 Phase 6)
  - `bee93bd` — fix: back-fill lifetime totals on schema v1 to v2 migration
  - `e133866` — fix: repair TL-017 benchmark harnesses
  - `6525f6f` — docs: finalize TL-017 performance results
- TL-016 localization branch merged into `master`: `9dc8335` — `merge: feature/tl016-persian-localization into master (TL-016)` (`--no-ff`; `v0.1.0` tag remains at `c1f677a`).
- TL-016 continuation (branding/About/Diagnostics): code `ce8f6b0` — `feat: complete TL-016 product polish with brand icon pipeline, About page and diagnostics support`; docs `8e7fa00`.
- **v0.1.1 release (first post-TL-016):** version bump `f8d1b97` — `build: bump release version to 0.1.1 for the first post-TL-016 release`; docs `4fc21bf` — `docs: document v0.1.1 release (version bump, verification, upgrade results)`. Tag `v0.1.1` at `4fc21bf` (lightweight, same style as `v0.1.0` at `c1f677a`). Installer `TrafficLens-Setup-0.1.1-win-x64.exe`, SHA-256 `3FB5EE38238D497883755390C4659BFED5E3DDD8F9DD6F477FA776F29E68DE3B`.
- TL-016 (localization / Persian UI): `cbb1dce` — `feat: complete Persian localization with data-bound graph label and localized status/error detail surfaces (TL-016)`; docs `ae67cdb`.
- TL-015 (packaging / installer): `d794480` — `feat: add reproducible release pipeline and per-user Inno Setup installer with single-file win-x64 publish (TL-015)`; docs `077c3e8`.
- TL-014 (stability & performance): `d04fc8d` — `fix: eliminate periodic idle-CPU spikes via event coalescing and page-visibility gating (TL-014)`; docs `1fb8463`.
- TL-013 (settings): `6c0dec5` — `feat: add full Settings page with staged save, alert rule editors, startup registration, and absolute widget always-on-top (TL-013)`; docs `9c03cfe`.
- TL-011 (system tray): `6e4d122` — `feat: add system tray with minimize/close-to-tray, singleton restore, and coordinator-based exit (TL-011)`; docs `8f230ec`.
- TL-012 (alerts): `6512011` — `feat: add local alert engine with speed/daily usage rules, tray balloon notifications, and Alerts page (TL-012)`; docs `b41e33d`.
- TL-010 (floating widget): `f3dc2e9` — `feat: add always-on-top floating widget reusing live aggregate rates with persisted position/topmost (TL-010)`; docs `a527168`.
- TL-009 (SQLite history): `861269c` — `feat: add SQLite traffic history with ranges, History page, and native bar chart (TL-009)`.
- TL-008 (active connections): `c29adf4` — `feat: add active connections provider and Connections view via IP Helper owner-PID tables (TL-008)`.
- TL-007F (shutdown deadlock fix): `e09111e` — `fix: prevent shutdown deadlock by not capturing the SynchronizationContext in collector StopAsync (TL-007F)`; docs `f425dca`.
- TL-007 (per-process traffic, Applications-list UI): `27ca92d` — `feat: add per-process Applications view with sort, search, icons, and permission UX (TL-007)`; docs `b1059c6`.
- TL-007 (per-process traffic collector): `8f080fb` — `feat: add per-process traffic collector via real-time ETW kernel network events (TL-007)`; docs `6f5dc49`.
- TL-005 (dashboard): `bb6deef` — `feat: add live dashboard view with adaptive rate formatting (TL-005)`
- `2bf03c9` — `fix: classify OpenVPN TAP/DCO (type 53) and virtual nics correctly via driver descriptions (TL-004)`
- `6bfa9d6` — `feat: add download/upload rate calculation from counter deltas with monotonic timing (TL-003)`
- `e3bef48` — TL-002 collector; `7f841be` — M0 smoke test; `d8f2933` — TL-001 bootstrap.

## Next Recommended Task

- **TL-018 (current-day hourly history) is complete on
  `feature/tl018-hourly-history`** (432/432 tests, Debug + Release 0W/0E,
  `tl018-verify.ps1` PASS) and ready to merge into `master` on user approval.
  **TL-017 (performance audit) is also complete on `feature/tl017-performance`**
  and remains ready to merge on approval (recall: no merge/tag/version bump is
  performed as part of any TL-XXX task).
- After these merge, typical candidates for a fresh task ID: monitoring
  feature additions, additional localization languages, or a new UX surface. Do
  not rename existing IDs.
- Release v0.1.1 shipped (tag `v0.1.1`); the 0.1.x baseline is current, so no
  further version bump is implied unless a post-TL-017/018 release is approved.