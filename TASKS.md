# TrafficLens — TASKS.md

## How to use

- Optional tasks are marked `[optional]`.
- A task is DONE only when implemented, verified (built/run/tested), and documented.
- Task IDs are stable. Do not renumber existing IDs.

## Backlog

### WUI-010 WinUI 3 Release Candidate v0.1.4 — **BUILT + LAUNCH-VERIFIED / HUMAN + SIGNING PENDING**
- [x] Version `0.1.4` in `Directory.Build.props`, `packaging/TrafficLens.iss` and both artifact names; `ProductVersion`/`InformationalVersion` `0.1.4`, `AssemblyVersion`/`FileVersion` `0.1.4.0`; About resolves the version from `AssemblyInformationalVersion` (no production-code change)
- [x] Release entry point switched to WinUI 3: self-contained `win-x64`, Windows App SDK 2.5.1 **loose** layout, `PublishSingleFile=false`, no trimming/ReadyToRun, no PDBs, **no MSIX**. WPF project retained as rollback/reference and never published (the pipeline fails if `TrafficLens.exe` appears in the output)
- [x] Installer targets `TrafficLens.WinUI.exe` (Start Menu/Desktop shortcuts, post-install launch, WMI process check, uninstall checks); stable `AppId` `{8F0E8A8F-7B1D-4A5E-9C2D-3E5F6A7B8C9D}` unchanged; `[InstallDelete]` removes a stale WPF `TrafficLens.exe` on upgrade
- [x] Signing-ready and unsigned by default: opt-in `-Sign` with thumbprint resolved from the Windows certificate store, SHA-256 + RFC 3161 timestamp, order publish → product binaries → packages → installer → SHA-256; no key material in the repository
- [x] **Signing-pipeline audit (static).** Real defect fixed: the hard-coded 6-name sign list **omitted the shipped Persian satellite `fa-IR/TrafficLens.WinUI.resources.dll`**, and a partially signed app is treated as untrusted, so the release would have been unlaunchable even though every signing call succeeded. The sign set is now discovered (all 7 TrafficLens-owned PE images: the apphost, 5 assemblies and the satellite), the whole set is re-verified after signing (`Valid` **and** an RFC 3161 timestamp countersignature), ECC and expired certificates are rejected up front (Smart App Control is **RSA-only**), `/sm` is passed for `LocalMachine\My`, and the extension test is explicit because `Get-ChildItem -LiteralPath -Recurse -Include` silently ignores `-Include` (it would have tried to sign `.ico`/`.json`). Microsoft/.NET/WinAppSDK binaries are never re-signed. Verified without a certificate
- [x] **Trust-path conclusion (researched, not assumed):** there is no *guaranteed* path to run this release unsigned under enforced Smart App Control — Microsoft app intelligence may allow a file it can classify as safe, or the file must carry an RSA certificate chaining to the Microsoft Trusted Root Program; cloud reputation only accrues through real distribution and is not a reproducible gate, and self-signed is not honoured. **Amended after measurement:** enforcement did not in fact stop v0.1.4 on this host, so "blocked" must not be asserted without a Code Integrity event. EV is no longer a reputation shortcut (same schedule as OV since 2024); OV suffices and Microsoft recommends Azure Artifact Signing for non-Store distribution
- [x] Pipeline correctness fixes: `-p:Platform=x64` required (Windows App SDK self-contained targets fail under AnyCPU), portable ZIP written with spec-compliant forward-slash entry names, `-ResumeFromPublish` for re-packaging without repeating build+tests, executable name derived from the WinUI project's `<AssemblyName>`
- [x] Release run: **571/571 tests PASS** (270 App / 212 Network / 84 Infrastructure / 5 WinUI), Release x64 build **0 warnings / 0 errors**
- [x] **Release-critical publish defect found and fixed — the first v0.1.4 RC could not start.** `dotnet publish` silently omitted `TrafficLens.WinUI.pri`: MRT Core writes the PRI straight to `$(TargetDir)` and only registers it as publishable when `AppxPackage == true` (MSIX), but this app is unpackaged, so it never entered `ResolvedFileToPublish`. The compiled XBF lives **only** in that PRI (the assembly embeds zero XBF), so the RC started, set `fa-IR`, then died with `XamlParseException: XAML parsing failed` in `MainWindow.InitializeComponent()` (Application Error 1000, `Microsoft.UI.Xaml.dll`, `0xc000027b`). Every earlier gate — build, 571 tests, publish, packaging, install, checksums — stayed green, so only a real launch exposed it. Fixed with an `IncludeProjectPriFileInPublish` target (`AfterTargets=ComputeResolvedFilesToPublishList`) using the supported MSBuild publish mechanism, not a copy of `bin`
- [x] **Regression guard:** publish validation now fails the run **before** ZIP/installer creation when the PRI is missing, empty, <256 KB or not a PRI container. MRT emits `mrm_pri2`, so the header is checked as `mrm_` (legacy `PRIC` also accepted) rather than a magic this toolchain never produces. Verified failing on the broken tree and passing on the fixed one
- [x] **Correction — Smart App Control did NOT block the release.** The `0x800711C7` attribution recorded earlier for this artifact was wrong: the broken RC launched normally and wrote **no** Code Integrity event. The crash was purely the incomplete publish output. SAC is still ON (`VerifiedAndReputablePolicyState=1`) and was never modified or bypassed
- [x] **Superseded broken RC (do not distribute):** installer `3E9DEA0E…`, ZIP `660E1D4C…`, 814 entries, retained only as history
- [x] Artifacts: `TrafficLens-Setup-0.1.4-win-x64.exe` (85.78 MB) + `TrafficLens-Portable-0.1.4-win-x64.zip` (123.61 MB, 815 entries) with `.sha256` sidecars; installer `E03C4B00…`, ZIP `0539049C…`; v0.1.3 artifacts preserved unchanged
- [x] Static verification: x64 PE, Windows App SDK runtime payload, no PDBs/test assemblies/WPF exe, branding assets, embedded English + `fa-IR` satellite, clean extraction (815 files), `TrafficLens.WinUI.pri` present in publish **and** ZIP (2,230,712 bytes, `mrm_pri2`, `64DE369D…`), version metadata, `NotSigned`, sidecars match recomputed hashes
- [x] **Runtime verification (one launch, PID 12484):** app starts, stays responsive and opens a real window; `WinUI MainWindow activated`; culture `fa-IR`; 7 nav items with داشبورد selected; live data from the collectors (دانلود 8.55 KB/s, آپلود 666 B/s, مجموع 9.2 KB/s); network, connection and history services started; SQLite schema v2 ready; no new Code Integrity events. The ETW process collector reports `permission denied` without elevation — expected and non-fatal
- [x] Floating Widget running: visible at **340x140** with `Always On Top` active (`FloatingWidgetEnabled=True`, `FloatingWidgetAlwaysOnTop=True` in `settings.json`). The app was left running for inspection and was not restarted
- [x] **Duplicate Settings widget control removed (human screenshot finding).** The Persian Settings page carried a quick widget card at the top *and* the full widget section lower down, so the widget could be enabled from two places on the same page. The top card is deleted outright — card, `WidgetQuickToggle`, its `Toggled` handler and the two code-behind labels — not collapsed, leaving one widget section (enable + Always On Top) with the sections below moved up and no blank card or gap. `WidgetToggleSync.Apply` existed only to feed that pair of switches and went with it; `Resolve` is still shared. Both remaining switches — the shell quick action and the Settings section — are views of the one `FloatingWidgetEnabled` state written only through `SetEnabled`: no second setting, service or lifecycle, still no polling, and Always On Top stays in Settings. No localization key was removed (`FloatingWidgetLabel` / `EnableFloatingWidgetLabel` are still used by the shell control, the tray and the WPF views). Full suite **711/711 PASS** (App 270 / Network 212 / Infrastructure 84 / WinUI 145), Release x64 build **0 warnings / 0 errors**; runtime on one launch, `fa-IR`, PID 11208: one widget section, quick action holding 29 physical px relLeft (12 DIP) through resize / maximize / restore / navigation collapse, and all five live state paths (quick ON, quick OFF, Settings ON, Settings OFF, widget X) agreeing in both directions and persisting
- [ ] Human GUI verification of the WUI-009 widget title bar, the final window/widget icon state, and the Persian RTL layout — **PENDING a person looking at the screen**; machine checks above are not a substitute and no human PASS is claimed
- [ ] Persian title still runs under the caption buttons while `AppWindow.TitleBar.LeftInset`/`RightInset` both report 0 on this host; the DPI-aware zero-inset fallback is a separate follow-up task and is **not** present at this HEAD. The caption reserve is still applied to the title, so this is the pre-existing zero-inset case, not a regression from the Settings cleanup
- [ ] Code signing with a real public CA certificate (RSA), then re-hash the artifacts — future release step
- [ ] Full final release checklist in `docs/PACKAGING.md` — signing, human visual, upgrade, uninstall, soak and clean-machine items all completed
- **Status: release candidate built, statically verified and launch-verified** — not merged, tagged or published; **not approved for public release** while the human visual and signing sections remain unticked

### WUI-009 WinUI 3 Migration — Extended Audit + Hardening — **PARTIAL (stopped intentionally)**
- [x] Phases A-E: build/test baseline, single instance + tray exit/relaunch, 20 navigation cycles, EN/FA language stress, 10/10 tray hide/restore cycles (evidence in `%TEMP%\opencode\wui009-*`)
- [x] Fixes (commit `21ca7c1`): widget title bar (Pin left, native Min/Close, Maximize disabled, minimized restore), second-launch restore of hidden main window, invariant logger/icon-log/History CSV dates, `fa-IR` regression tests
- [x] Final full test run: **571/571 PASS**; Release x64 build: **0 warnings / 0 errors**
- [ ] Long-duration phases (extended performance, long soak, elevated Windows Update toggle) — **NOT TESTED**
- [ ] Human GUI verification of the final widget title bar and `fa-IR` state — **PENDING**
- **Status: partial** — audit stopped on request before all planned phases completed; completed evidence preserved

### WUI-002 WinUI 3 Migration — Dashboard Page + Live Bindings — **AWAITING HUMAN GUI VERIFICATION**
- [x] `ViewModels/DashboardViewModel.cs` — port of WPF dashboard (shared collector/aggregator/formatters/history/process services; coalesced DispatcherQueue refresh; no new timers/collectors)
- [x] `Controls/TrafficGraphView` — Canvas polyline live graph (points, scale, window, reference time; theme brushes; no third-party chart lib)
- [x] `Pages/DashboardPage.xaml(.cs)` — Download/Upload/Total cards, Today at a Glance, Top App Now, Active Adapter, live graph + 30s/1m/5m range buttons
- [x] DI: `AddHistoryServices(AppPaths.DatabaseFile)`; started `IProcessTrafficCollector` + `ITrafficHistoryService` once in App (network collector already started)
- [x] Localization via existing resx keys (EN + fa-IR); numeric values LTR; RTL FlowDirection on page root; responsive reflow &lt;780px stacks cards
- [x] Page lifecycle: Loaded → SetActive(true); Unloaded → SetActive(false) + Dispose (unsubscribes events)
- [x] Release build: 0 warnings / 0 errors (full solution)
- [x] Tests: 542/542 PASS (212 Network + 60 Infrastructure + 270 App)
- [x] Launched WinUI app; process left running
- [ ] GUI verification — **REQUIRES HUMAN INSPECTION** (live rates, cards, graph, EN/FA/RTL, responsive)
- **Status: awaiting visual verification** (branch `feature/winui3-migration`, prior WUI-001 `5677e14`)

### WUI-001 WinUI 3 Migration — Foundation + Application Shell — **AWAITING HUMAN GUI VERIFICATION**
- [x] `docs/WINUI3_MIGRATION.md` — component map, WASDK version rationale, WUI-001…010 plan
- [x] New `src/TrafficLens.WinUI` project — `net8.0-windows10.0.19041.0`, Windows App SDK **2.5.1**, unpackaged, self-contained, x64
- [x] App.xaml composition root — DI (`ISettingsService`, `ILocalizationService`, file logging)
- [x] MainWindow — custom TitleBar (`ExtendsContentIntoTitleBar`), `NavigationView`, dark theme
- [x] Destinations: Dashboard, Applications, Connections, History, Alerts, Settings, About (placeholder pages)
- [x] Localization foundation — linked `Strings.resx`/`Strings.fa-IR.resx`, runtime EN/FA switch, RTL `FlowDirection`, language persisted
- [x] Solution membership with x64 platform mapping
- [x] Release build: 0 warnings / 0 errors (project + full solution)
- [x] Tests: 542/542 PASS (212 Network + 60 Infrastructure + 270 App)
- [x] Launched WinUI shell; process alive and responding
- [x] GUI verification — **HUMAN PASS** (2026-09-23; shell, nav, EN/FA/RTL confirmed)
- **Status: done (human PASS)** — superseded for UI by WUI-002 (branch `feature/winui3-migration`, rollback `f9017f0`)

### TL-026 Release v0.1.3 — Dark Theme & Localization Hotfix — **DONE**
- [x] White main content background removed (MainWindow root Grid)
- [x] Dark ComboBox ControlTemplate with DynamicResource Popup
- [x] Dark WPF ContextMenu/MenuItem implicit styles
- [x] AlertRuleViewModel.Name PropertyChanged fix
- [x] ConnectionsViewModel backing field bypass fix
- [x] FloatingWidgetWindow FlowDirection culture refresh
- [x] CSV dialog localization
- [x] MainViewModel.Dispose CultureChanged unsubscribe
- [x] **Navigation crash fix** — ComboBox template ColumnDefinition Width hardcoded to `17` (was `{DynamicResource SystemParameters.VerticalScrollBarWidthKey}` → `double` → `GridLength` cast failure)
- [x] **Disabled ComboBox Background fix** — SurfaceColor → SurfaceBrush in ComboBox disabled-state trigger
- [x] **Alerts configured rules display** — New "Configured Alert Rules" section on Alerts page showing enabled rules with thresholds
- [x] **Alerts empty state** — "No alerts have been configured yet." / "No alerts have been triggered yet." localized empty states
- [x] **IAlertService.ConfigChanged event** — Alerts page updates immediately when rules are saved on Settings page
- [x] 17 new DarkTheme tests + 8 localization/regression tests
- [x] Tests: 542/542 PASS (212 Network + 60 Infrastructure + 270 App)
- [x] Debug + Release builds: 0 warnings / 0 errors
- [x] Version bump 0.1.2 → 0.1.3
- [x] GUI verification — human visual PASS (navigation crash verified fixed)
- **Status: done**

### Post-0.1.2 UI/UX Hotfix — **DONE**
- [x] Dark theme ComboBox style — implicit ComboBox/ComboBoxItem styles in DarkTheme.xaml
- [x] Floating widget toggle fix — Suspend/Resume lifecycle, no Dispose on hide
- [x] About page icon quality — use TrafficLens-256.png instead of ICO
- [x] Global dark theme controls — implicit TextBox, CheckBox, Button styles
- [x] Widget toggle regression tests — 12 new tests covering Suspend/Resume/toggle cycles
- [x] Tests: 510/510 PASS (212 Network + 60 Infrastructure + 249 App)
- [x] Debug + Release builds: 0 warnings / 0 errors
- [ ] GUI verification — **REQUIRES HUMAN INSPECTION**
- **Status: awaiting visual verification**

### TL-025 Release Candidate 0.1.2 — **DONE**
- [x] Version bump 0.1.1 → 0.1.2 (Directory.Build.props, TrafficLens.iss defaults, lifecycle assertions)
- [x] Release branch created: `release/0.1.2`
- [x] Tests pass: 509/509 (212 Network + 60 Infrastructure + 237 App)
- [x] Debug + Release builds: 0 warnings / 0 errors
- [x] Release candidate built: installer (69 MB), portable ZIP (68.6 MB)
- [x] SHA256: `9FABE44055AA9EE91E537ECFB365B2B5CECAED0D86BB1D3AE2EF230223E0079A`
- [x] Installer metadata: ProductVersion=0.1.2, FileVersion=0.1.2, AppId unchanged
- [x] English installer GUI: PASS (human visual verification)
- [x] Persian installer GUI: PASS (human visual verification)
- [x] Installer title: "TrafficLens 0.1.2" (full version, not truncated)
- [x] Task localization: {cm:} constant syntax for Persian task strings
- [x] Lifecycle validation: 19/19 PASS (tests 1-5: checksum, install, launch, exit, startup)
- [x] WDAC blocked uninstaller/reinstall — accurately reported, not fabricated
- [x] Fix: installer title truncation (AppVersionShort → AppVersion)
- [x] Fix: Persian task localization (CustomMessage() → {cm:} constant syntax)
- [x] Documentation updated (CHANGELOG, TASKS, PROJECT_STATUS, ROADMAP, PACKAGING)
- **Status: done — RC ready for merge/tag/publish approval**

### TL-024 Installer Localization & Packaging Polish — **DONE**
- [x] Persian (Farsi) installer language file (`packaging/Persian.isl`) — RTL layout, LanguageID=$0429, CodePage=1256, translated wizard messages
- [x] English/Persian language selection in installer (`[Languages]` section)
- [x] Custom translated `AppRunningWarning` message per language via `[CustomMessages]`
- [x] Placeholder URLs removed — `AppPublisherURL` and `AppSupportURL` entries removed from `[Setup]`
- [x] Lifecycle validation extended — 5 new assertions (9.5–9.9): no placeholder URLs, stable AppId, English/Persian languages present
- [x] Installer compiles with both languages — 37/37 lifecycle checks PASS
- [x] Regression tests — 509/509 PASS (212 Network + 60 Infrastructure + 237 App)
- [x] Debug + Release builds — 0 warnings / 0 errors
- [x] Docs updated (TASKS, PROJECT_STATUS, CHANGELOG, ROADMAP, PACKAGING)
- **Status: done**

### TL-023 Release Hardening & Clean-Machine Validation — **DONE**
- [x] Release lifecycle validation script (`scripts/tl023-release-lifecycle.ps1`) — 31-point deterministic validation
- [x] SHA256 checksum verification — computed hash matches sidecar file
- [x] Clean install — installer exits 0, install directory created, Start Menu shortcut present
- [x] First launch — installed app launches, window renders, settings directory initializes, database initializes
- [x] Graceful exit — WM_CLOSE terminates process, no orphan processes, no orphan ETW sessions
- [x] Startup registration — no entry by default, write/read with --minimized works, no duplicate on reinstall
- [x] Uninstall + data preservation — program binaries removed, Start Menu removed, settings.json preserved, trafficlens.db preserved
- [x] Reinstall + data loading — reinstall succeeds, settings intact, app launches with existing data
- [x] Portable build — extracted, launches, uses shared LocalAppData settings, no orphan on exit
- [x] Installer metadata — ProductName, ProductVersion, FileVersion correct; placeholder URLs documented
- [x] Release pipeline — build-release.ps1 succeeds: 509/509 tests, 0 warnings/0 errors, installer 69 MB, portable 68.6 MB, SHA256 verified
- [x] Regression tests — 509/509 PASS (212 Network + 60 Infrastructure + 237 App)
- [x] Debug + Release builds — 0 warnings / 0 errors
- [x] Docs updated (TASKS, PROJECT_STATUS, CHANGELOG, ROADMAP)
- **Status: done**

### TL-022 History This Month + Today vs Yesterday + CSV Export — **DONE**
- [x] `HistoryRange.ThisMonth` enum value added — local calendar month semantics (first day of current month through current time), not rolling 30 days
- [x] `HistoryRangeCalculator.ToLocalDateRange` handles `ThisMonth` — half-open `[monthStart, today+1)` using local calendar
- [x] `HistorySnapshot` extended with `ThisMonth` (`TrafficUsage`) and `DayFraction` (`double`) — `For(HistoryRange)` handles `ThisMonth`
- [x] `TrafficHistoryService.BuildSnapshotAsync` computes `ThisMonth` via bounded `SumDaily` over month range; computes `DayFraction` from local midnight UTC span
- [x] **This Month range** in HistoryViewModel — selection, daily series slicing by month/year, chart renders monthly data
- [x] **Today vs Yesterday comparison** — compares Today's usage against Yesterday's usage scaled to equivalent elapsed local-day interval via `DayFraction`; shows absolute difference + percentage; safe zero-denominator handling; hidden when no comparison data
- [x] **CSV export** — `SaveFileDialog`, UTF-8 with BOM, header row (`Period,DownloadBytes,UploadBytes,TotalBytes`), invariant numeric values, deterministic column order; uses same data as selected range (hourly for Today, daily for other ranges); async file IO; IO error surfaces as localized non-crashing message
- [x] Localization — 9 new keys in both `Strings.resx` and `Strings.fa-IR.resx` (ThisMonthLabel, TodayVsYesterdayLabel, YesterdayAtThisTimeLabel, DifferenceLabel, ExportCsvLabel, ExportSuccessfulLabel, ExportFailedLabel, NoComparisonDataLabel)
- [x] HistoryView.xaml — This Month button in range bar, comparison border below chart, Export CSV button with `AutomationProperties.Name`
- [x] Tests (+14 → 509/509): `HistoryRangeCalculatorTests` (+4: ThisMonth range semantics, first-day, end-of-year, not-rolling-30), `HistoryViewModelTests` (+10: ThisMonth selection/series, comparison positive/negative/equal/zero-yesterday/hidden-when-no-data/day-fraction-scaling, localization)
- [x] Build Debug + Release: **0 warnings / 0 errors**
- [x] GUI verification: This Month visible/selectable, comparison values correct, CSV export working, en-US/fa-IR/RTL verified, TL-021/TL-020 regressions verified
- [x] No new timers/polling/ETW sessions — zero hits in HistoryViewModel
- [x] TL-017 through TL-021 regression-free — full test suite 509/509 PASS
- [x] Docs updated (PROJECT_STATUS, TASKS, CHANGELOG)
- **Status: done**

### TL-021 Dashboard "Today at a Glance" + "Top App Now" — **DONE**
- [x] `DashboardViewModel` — added optional `ITrafficHistoryService?` and `IProcessTrafficCollector?` constructor parameters (backward-compatible, null-safe)
- [x] "Today at a Glance" section — reads `HistorySnapshot.Today` (`TrafficUsage.DownloadBytes`/`UploadBytes`/`TotalBytes`), formatted via `DataSizeFormatter.Format()`; reuses existing `HistoryChanged` event, no new polling
- [x] "Top App Now" section — reads `ProcessSampleSelection.TopConsumer()` from `IProcessTrafficCollector` snapshots; reuses existing `SamplesReady` event; shows app name, total rate, download/upload rates; idle/permission-denied/unavailable states
- [x] `MainWindow.xaml` — three-card Today grid + Top App card between tunnel hint and Active Adapter section; all numeric content `FlowDirection="LeftToRight"` for RTL compatibility
- [x] Localization — 12 new keys in both `Strings.resx` and `Strings.fa-IR.resx` (TodayAtGlanceLabel, DownloadTodayLabel, UploadTodayLabel, TotalTodayLabel, TopAppNowLabel, TopAppApplicationLabel, TopAppCurrentLabel, TopAppNoDataLabel, TopAppPermissionDeniedLabel, TopAppUnavailableLabel, TopAppDownloadLabel, TopAppUploadLabel)
- [x] `RefreshToday()` — checks `_historyService.IsAvailable` and `snapshot.IsAvailable` before reading usage data; zero-fills on unavailable
- [x] `RefreshTopApp()` — sets `TopAppStatusText = TopAppNoDataLabel` on idle; clears rate/name text on permission-denied/unavailable; updates from samples on `SamplesReady`
- [x] Event wiring — `HistoryChanged`, `SamplesReady`, `StatusChanged` subscriptions in constructor, unsubscribed in `Dispose()`; gated by `_isActive` for event-driven updates
- [x] Tests (+22 → 495/495): `DashboardInsightTests` — 7 Today tests (A-F: formatted bytes, empty, unavailable, snapshot unavailable, history changed event, localization, medium size), 11 Top App tests (G-P: active process, idle, empty, permission denied, failed, stopped, samples ready event, top consumer selection, recovery, localization, no service), 4 Lifecycle tests (Q-T: set-active gating, dispose unsubscribe, no-history constructor)
- [x] Build Debug + Release: **0 warnings / 0 errors**
- [x] GUI verification: Dashboard layout correct, Today/Top App sections visible, en-US/fa-IR/RTL verified, runtime language switching correct, PermissionDenied/unavailable states verified
- [x] No new timers/polling/ETW sessions/history polling — all data from existing event pipelines
- [x] TL-017 through TL-020 regression-free — full test suite 495/495 PASS
- [x] Docs updated (PROJECT_STATUS, TASKS, CHANGELOG)
- **Status: done**

### TL-020 Connections Readability & Usability — **DONE**
- [x] `ConnectionFilter.HideListeners` (Core/Selection) — hides TCP `Listen` + unconnected UDP (`RemoteAddress` is null); pure function, no provider changes
- [x] `HideListeners` filter option in `ConnectionsViewModel` + persisted via `ISettingsService` (`ConnectionsHideListeners` key, default `false`); toolbar checkbox in Connections view
- [x] Copy actions (local endpoint, remote endpoint, remote IP, process name) via right-click context menu on connection rows; keyboard accessible, localized (en-US + fa-IR)
- [x] `EnableReverseDns` setting (OFF by default, persisted via `ISettingsService` key `ConnectionsEnableReverseDns`); bounded DNS cache (TTL 30 min success / 5 min failure, max 1024 entries, 4 concurrent lookups, failure caching, cancellation-safe); DNS resolver service integrated into `ConnectionsViewModel`
- [x] Settings page: EnableReverseDns checkbox in General section (persisted via existing `ISettingsService`; `ConnectionsEnableReverseDns` key)
- [x] Localization: en-US + fa-IR parity for all new strings (HideListeners, Copy actions, EnableReverseDns); RTL layout correct; runtime culture switch updates UI
- [x] Accessibility: keyboard nav, focus order, Escape dismiss, `AutomationProperties.Name` on context menu items and checkboxes
- [x] Performance: zero background cost when disabled; reuses existing `AdaptersChanged` events; no new timers/polls/workers; preserves TL-017 virtualization/diffing/visibility gating
- [x] Tests (+3 → 191 App tests): `Filter_HideListeners_HidesTcpListenersAndUnconnectedUdp`, `HideListeners_Setting_PersistsAndRestores`, `EnableReverseDns_DefaultFalse_PersistsAndRestores`
- [x] Build Debug + Release: **0 warnings / 0 errors**
- [x] Real Windows GUI verification (published single-file): fresh profile onboarding, HideListeners toggle, EnableReverseDns toggle, copy actions, en-US/fa-IR/RTL, runtime language switch, all pages functional, graceful exit, no orphan process
- [x] Docs updated (PROJECT_STATUS, TASKS, CHANGELOG, ROADMAP)
- **Status: done**

### TL-019 First-Run Get Started + Contextual Clarity — **DONE**
- [x] `ISettingsService.SettingsFileExisted` property + `JsonSettingsService` impl — captures file existence at construction for fresh-vs-existing profile distinction
- [x] `OnboardingSettings` pure helper (App/Services) — `ShouldAutoShow` (fresh profile only), `IsCompleted`, `MarkCompleted` (`HasCompletedOnboarding` key)
- [x] `OnboardingViewModel` — auto-show on fresh profile, dismiss → persist, manual reopen (`Show()`), localized strings (18 keys), keyboard/accessibility, no logic in code-behind
- [x] `OnboardingView` (XAML + code-behind) — overlay panel with backdrop, 6 topic cards (What TrafficLens does, Applications/Admin, VPN & tunnels, System tray, Floating widget, Alerts/Startup/Language), dismiss button, focus on open, Escape dismiss
- [x] `MainViewModel` wiring — `OnboardingViewModel` DI singleton, `AutoShowIfRequired()` called in ctor, `Onboarding` property exposed
- [x] About/Settings manual reopen — `ShowGuideCommand` in both VMs bound to "Get Started" / "راهنمای شروع" buttons in AboutView and SettingsView
- [x] `MainWindow.xaml` — overlay Grid with `Visibility` bound to `Onboarding.IsVisible` (fixed DataContext binding conflict), Escape `KeyBinding` → `Onboarding.DismissCommand`
- [x] Dashboard tunnel hint — `HasTunnelAdapter` + `TunnelAggregateHint` in `DashboardViewModel`, driven by existing `AdaptersChanged`/`NetworkChanged` events, localized text, shown near Total card
- [x] Localization — 18 new keys added to both `Strings.resx` and `Strings.fa-IR.resx` (parity 180 keys), runtime culture switch updates open panel, RTL layout correct
- [x] Tests (+23 → 455/455): `OnboardingViewModelTests` (10), Dashboard tunnel hint (5), About/Settings/MainViewModel reopen (6), `JsonSettingsService` `SettingsFileExisted` (2), resource parity extended
- [x] Build Debug + Release: **0 warnings / 0 errors**
- [x] Real Windows GUI verification (published single-file): fresh profile auto-show, dismiss persistence, restart no auto-show, About/Settings manual reopen, en/fa-IR/RTL, runtime language switch, Dashboard/Applications/History/Alerts/Settings/Widget/Tray functional, graceful exit, no orphan process
- [x] Docs updated (PROJECT_STATUS, TASKS, CHANGELOG, ROADMAP)
- **Status: done**
- [x] `HourlyUsagePoint` (Core) — immutable record struct: `StartUtc`,
      `EndUtcExclusive`, `LocalHour`, download/upload bytes + `TotalBytes`
- [x] `HourlyHistoryBuilder` (Core, pure static) — buckets `TrafficHistoryBucket`
      samples into UTC-hour slots counted from local-midnight-UTC
      (`MidnightUtc(DateOnly, TimeZoneInfo)`); zero-filled slots, slots `start ≥
      now` excluded, current partial hour clamped to `nowUtc`; labels use the
      local hour of the UTC slot start; DST-safe (spring-forward 23 slots, no
      skipped local label; fall-back 25 slots, duplicated local label; +05:30
      half-hour zone → 12 slots via `Math.Floor` of elapsed hours)
- [x] `ITrafficHistoryRepository.QuerySamplesAsync(startUtcInclusive,
      endUtcExclusive, ct)` + `SqliteTrafficHistoryRepository` impl — one bounded
      half-open UTC query over `traffic_samples` ordered by start; no new index
- [x] `EXPLAIN QUERY PLAN` regression test → built-in INTEGER PRIMARY KEY seek
      (no `SCAN`)
- [x] `HistorySnapshot` 9th member `TodayHourly`; `BuildSnapshotAsync` builds it
      from one bounded query (± no extra queries, no SQL in App/VM)
- [x] App: `HistoryChartPoint` + `HistoryBarChartControl.Points` type refresh;
      `HistoryViewModel` daily/hourly mapping (`MM-dd` vs `HH:00` labels),
      `ChartTitleLabel` (hourly on Today, daily otherwise);
      `HistoryView` title bound to `ChartTitleLabel`
- [x] Localization: `HistoryHourlyTrafficLabel` en ("Hourly Traffic") + fa-IR
      ("ترافیک ساعتی"), parity now 162 keys each; `RequiredKeys` extended
- [x] Tests (+16 → **432/432**; App 167 / Network 212 / Infra 53):
      `HourlyHistoryBuilderTests` (8, incl. real CE(S)T DST 23h/25h days),
      repo range/empty/plan (3), service hourly-snapshot (1),
      `HistoryViewModelTests` rewritten to 10; alerts fakes updated
- [x] Build Debug + Release: **0 warnings / 0 errors**
- [x] Real Windows GUI verification (`scripts/tl018-verify.ps1`, published
      single-file build, blocks A–E): en-US hourly/daily titles + 5 range
      buttons; resize; real-traffic data chain with **exact** daily/sample
      reconciliation (138 samples, 164,317,489 B down / 28,456,609 B up);
      fa-IR localized titles, no English leak; graceful exit, no orphans
- [x] Docs updated (PROJECT_STATUS, TASKS, CHANGELOG, ROADMAP M13)
- **Status: done**
- **Commit:** see `docs/PROJECT_STATUS.md` Git Commit section (feat + tests/docs)

### TL-011 System Tray — **DONE**
- [x] Single WinForms `NotifyIcon` via `<FrameworkReference Include=
      "Microsoft.WindowsDesktop.App.WindowsForms" />` (no `UseWindowsForms`,
      no global-using ambiguity)
- [x] One icon created once; disposed only on real exit; tooltip `TrafficLens`;
      runtime-drawn 32×32 icon (dark rounded square + accent chevrons, readable
      16–32 px; `GetHicon`/`FromHandle`/`DestroyIcon`)
- [x] Tray menu: Open TrafficLens / Show-Hide Floating Widget / Always on Top
      (checkable) / separator / Exit; culture-change relabel in place (no icon
      recreation); Always on Top reuses the widget pin state
- [x] Double-click / Open restores the **same** singleton MainWindow (never a
      second instance)
- [x] Minimize-to-tray (`StateChanged` → Normal+Hide) and Close-to-tray
      (`Closing` → HideToTray) with settings defaults true;
      CloseToTray=false ⇒ X real graceful exit via the coordinator
- [x] Single idempotent `ApplicationExitCoordinator.RequestApplicationExit()`
      (latch → dispose tray+widget → `Shutdown()`); no `Environment.Exit`;
      `ShutdownMode="OnExplicitShutdown"`; collectors/history/DI disposed by
      the container
- [x] First close-to-tray balloon once-ever, persisted `TrayCloseNoticeShown`
- [x] Collectors keep running and history keeps accumulating while hidden
- [x] Floating widget pin tooltip bound to `AlwaysOnTopLabel` (TL-010 polish)
- [x] Dispatcher-safe marshaling; thread-safe tray usage
- [x] Localization en + fa-IR for all tray strings (technical name stays LTR)
- [x] 317 tests passing (10 `TrayBehaviorTests`, 4
      `ApplicationExitCoordinatorTests`, localization keys, widget always-on-top
      label); Debug+Release 0 warnings/0 errors
- [x] Real Windows GUI verification (`scripts/tl011-verify.ps1`): tray icon
      present/hidden-tray, minimize→hidden+alive+collectors running, history
      accumulates while hidden, close→hidden+alive+notice once, singleton HWND,
      CloseToTray=false→prompt clean exit+no ETW, 3 lifecycle cycles clean;
      tray-click restore/menu Exit covered by unit tests + same handlers
- [x] Docs updated (PROJECT_STATUS, TASKS, CHANGELOG, ARCHITECTURE, DECISIONS
      ADR-019)
- **Status: done**
- **Commit:** see `docs/PROJECT_STATUS.md` Git Commit section

### TL-012 Alerts — **DONE**
- [x] Core alert domain (`src/TrafficLens.Core/Alerts`, no WPF/OS dependencies):
      `AlertType` (HighDownloadSpeed / HighUploadSpeed / DailyDownloadLimit /
      DailyUploadLimit / DailyTotalLimit with `IsSpeedRule`/`IsDailyUsageRule`),
      `AlertConfig` (immutable record + `Default()`; 5 rules all **disabled by
      default** with suggested thresholds 50 MB/s / 20 MB/s / 50 GB / 20 GB /
      100 GB and 5 min cooldown), `AlertEvent` (type/value/threshold/occurred),
      `AlertSignal`, `AlertEngine` (pure, gate-locked, clock-injected), and
      `AlertHistoryBuffer` (session-only, capacity 100, newest-first)
- [x] Speed semantics (tested): triggers on **upward crossing only**;
      `rate ≥ threshold` consumes the armed crossing and signals only when
      `now ≥ CooldownUntilUtc`; dropping below re-arms but **does not clear the
      cooldown** — remaining above never repeats, flapping yields ≤ 1 alert per
      cooldown window; no spam
- [x] Daily semantics (tested): at most once per **local calendar day**
      (DST-safe via injected `TimeZoneInfo`); the last-triggered local date is
      persisted (`alerts.lastTriggered.…`) on trigger and restored on
      construction so a same-day restart (or crash) never re-fires; next local
      day re-arms
- [x] Alert pipeline never touches SQL: speed evaluated from
      `INetworkTrafficCollector.GetCurrentSamples()` +
      `INetworkAdapterProvider.GetAdapters()` via the existing
      `NetworkTrafficAggregator.AggregateRates` (ADR-009/010 policy) on
      `SpeedSampleReady`; daily evaluated from the cached
      `ITrafficHistoryService.GetSnapshot()` on `HistoryChanged` — history
      unavailable ⇒ daily rules suspended silently, speed continues
- [x] App services (`TrafficLens.App/Services`): `AlertSettings` (flat settings
      keys + Load/Save + Load/SaveTriggeredDates, invariant culture),
      `AlertNotification` + `AlertMessageFormatter` (structured, re-localized on
      culture change), `IAlertService` + `AlertService` (singleton, owns the
      engine, subscribes both pipelines, raises `AlertRaised`, logs every
      trigger, clean `Dispose`)
- [x] Tray delivery: `ISystemTrayService.ShowAlert(title, message)` +
      `SystemTrayService.ShowAlert` (RunOnUi, EnsureCreated, Warning balloon,
      8 s, dropped+logged if tray unavailable); `BalloonTipClicked` →
      `OpenRequested` (same singleton-restore handler verified in TL-011)
- [x] Alerts page (M8 UI surface): `AlertsViewModel` (count formatter,
      TimeText via `ToLocalTime`, newest-first rows), `Views/AlertsView.xaml`
      (+DI code-behind), `MainViewModel` Alerts nav + `ShowAlertsCommand`,
      `MainWindow` Alerts button + `AlertsHost` ContentControl, `App.xaml.cs`
      registrations + `AlertRaised → ShowAlert` wiring
- [x] Localization en + fa-IR: `AlertsNavLabel`, `AlertsTitleLabel`,
      `AlertsNoAlertsLabel`, `AlertsCountFormat`, `AlertTitle`, 6×
      `AlertType*`, 5× `AlertMsg*`; `LocalizationResourceTests.RequiredKeys`
      extended
- [x] Tests: `AlertEngineTests` (17 engine cases — upward-crossing-only,
      cooldown window, no-repeat-while-above, re-arm, once-per-day, DST-safe
      UTC+14 local-day identity, next-day re-arm, restore semantics; 3 buffer
      tests), `AlertServiceTests` (10), `AlertsViewModelTests` (4);
      `Fakes.cs` gained `FakeHistoryService`/`FakeAlertService`/
      `FakeTrayService.ShowAlert`
- [x] Build Debug + Release: **0 warnings / 0 errors**; **349/349 tests**
      (App 116 / Network 206 / Infrastructure 27)
- [x] Real Windows GUI verification (`scripts/tl012-verify.ps1`): (A) speed
      256 KB/s threshold + controlled `1Gb.dat` download → exactly one
      notification, no spam while above, re-arm after drop-below; (B) daily
      total 10 MB → once per local day, `alerts.lastTriggered.dailyTotal`
      persisted, same-day restart → no repeat; (C) notification while hidden
      in tray (no "notification dropped", tray icon alive); (D) full
      regression with default-disabled alerts + graceful exit, no ETW orphans
- [x] Docs updated (PROJECT_STATUS, TASKS, CHANGELOG, ARCHITECTURE, DECISIONS
      ADR-020)
- **Status: done**
- **Commit:** `6512011` (code + tests + script; see `docs/PROJECT_STATUS.md`)

### TL-013 Settings — **DONE**
- [x] Full Settings page (`SettingsView.xaml` + DI code-behind, nav button +
      host in `MainViewModel`/`MainWindow`): General (language combo,
      start-with-Windows, start-minimized, tray minimize/close), Floating
      Widget (enable, always-on-top, show/hide-from-page buttons), Alerts
      (5 rules with enable checkbox + threshold field + unit combo + cooldown
      minutes 1–1440)
- [x] Staged-save model: numeric fields/dropdowns apply on **Save** (validate →
      one logical persist → runtime apply → `RefreshFromSettings`); tray
      checkboxes apply **immediately** (no Save); Reset stages defaults behind
      a Yes/No confirmation dialog; `SavedNotice` + dirty tracking
- [x] `AlertRuleViewModel` per rule: threshold entered in the displayed unit
      (unit combo KB/s / MB/s / GB/s / TB/s for speed; MB/GB/TB for daily),
      converted invariant-bytes on persist, re-parse on load; cooldown
      validation bounds 1–1440 with inline error text
- [x] Save wiring uses `IFloatingWidgetService.SetAlwaysOnTop` (added) so an
      unchecked always-on-top **persists and applies** (the old
      `ToggleAlwaysOnTop` flipped relative to the already-written setting and
      re-enabled topmost); tray menu keeps the toggle
- [x] Startup registration: `IStartupRegistrationService` +
      `StartupRegistrationService` (HKCU `…\Run` value `TrafficLens` = quoted
      exe path, optional ` --minimized`, removes only its own value name);
      `--minimized` CLI arg starts hidden to tray (log marker `Starting hidden
      to system tray`)
- [x] `JsonSettingsService` hardening (tested): partial file merges defaults,
      unknown keys preserved on Save, malformed JSON falls back to defaults
      without crashing
- [x] Localization en + fa-IR for every Settings string (nav/title/sections/
      labels/units/save/reset/confirm/notice); `LocalizationResourceTests.
      RequiredKeys` extended
- [x] Tests: `SettingsViewModelTests` (Save staging/validation/language apply/
      widget state via `SetAlwaysOnTop`/alert config/reset defaults),
      `JsonSettingsServiceTests` (merge/preserve/malformed), fakes +
      localization keys; **382/382** (App 138 / Network 206 / Infrastructure
      38), Debug + Release 0 warnings / 0 errors
- [x] Real Windows GUI verification (`scripts/tl013-verify.ps1`, blocks A–I):
      partial-merge + unknown-key preservation and malformed fallback through
      the real page; en→fa→en combo switch + restart; tray immediate-apply +
      close-to-tray; widget enable/topmost-off/hide/show + restart
      persistence; page-configured 256 KB/s threshold → real alert → Reset with
      native `Reset to Defaults` confirmation (found + answered via Win32)
      → defaults + history DB untouched; HKCU Run create/quoted/--minimized/
      own-value-only removal; `--minimized` hidden start + tray alive; restart
      persistence; graceful exit with no orphan ETW sessions
- [x] Docs updated (PROJECT_STATUS, TASKS, CHANGELOG, ARCHITECTURE, DECISIONS
      ADR-021)
- **Status: done**
- **Commit:** see `docs/PROJECT_STATUS.md` Git Commit section

### TL-001 Project Bootstrap — **DONE**
- [x] Inspect .NET environment (installed .NET 8 SDK 8.0.425)
- [x] Create `TrafficLens.sln` and solution folder structure
- [x] Create `TrafficLens.App` (WPF), `TrafficLens.Core`, `TrafficLens.Network`, `TrafficLens.Infrastructure`
- [x] Configure project references (no circular dependencies)
- [x] Establish DI composition root (`App.xaml.cs`) and MVVM base classes
- [x] Configure structured file logging (Infrastructure `FileLogger`)
- [x] Create required documentation
- [x] Initialize Git repository
- [x] Minimal dark main window with dashboard placeholders
- [x] Localization foundation: `ILocalizationService`, `Strings.resx` (en), `Strings.fa-IR.resx`
- [x] RTL-ready architecture (`FlowDirection` switching, LTR-safe values)
- [x] Build solution: success, 0 warnings
- [x] Verify startup: GUI smoke test passed (start, dark theme, en/fa localization,
      RTL culture, logs, clean shutdown)
- [x] Update `PROJECT_STATUS.md` and `TASKS.md`

### TL-002 Global Network Collector
- [x] Research and select collection mechanism (see `docs/NETWORK_COLLECTION.md`)
- [x] Implement `INetworkTrafficCollector` (`WindowsNetworkTrafficCollector`)
  - [x] Poll cumulative per-adapter counters (received/sent bytes)
  - [x] `CounterSampleReady` event fires per adapter per poll
  - [x] `NetworkChanged` on adapter set change (init + `NetworkChange.NetworkAddressChanged`)
  - [x] Counter reset/wrap detection with re-baseline logging
- [x] Implement `INetworkAdapterProvider` (`WindowsNetworkAdapterProvider`)
- [x] Adapter enumeration, kind mapping, filtering (Ethernet/Wireless/Tunnel/Virtual/Unknown)
- [x] Default adapter detection (up + gateway preferred, never "first adapter")
- [x] Aggregation policy avoiding double-counting (`NetworkTrafficAggregator`)
- [x] Handle adapter connect/disconnect
- [x] Unit tests: 36 tests passing (mapper, filter, default selector, aggregator, collector)
- [x] Real verification: console collector cross-checked vs `Get-NetAdapterStatistics`
- [x] Document accuracy and limitations
- **Status: done**
- **Commit:** `e3bef48`

### TL-003 Download/Upload Calculation
- [x] Compute rate samples from cumulative counter deltas / real monotonic elapsed
- [x] Per-adapter independent baselines (`SpeedRateTracker`) with re-baseline rules
- [x] First sample / counter reset / wrap / reconnect / replacement: no fake spikes
- [x] Raise `SpeedSampleReady`; `GetCurrentSamples()` returns current rate samples
- [x] System aggregate rate (tunnel-excluding policy) via `AggregateRates`
- [x] Per-adapter views keep tunnel/VPN traffic (nothing discarded)
- [x] Unit conversions for UI: B/s, KB/s, MB/s, Kbps, Mbps, Gbps (`DataRateConverter`)
- [x] No UI-thread dependencies, no busy loops, bounded baselines
- [x] Tests: 65 passing (calculator, tracker, conversions, aggregate, collector)
- [x] Real verification: rates plausibly aligned with `Get-NetAdapterStatistics`
- **Status: done**
- **Commit:** `6bfa9d6`

### TL-004 Network Adapter Detection
- [x] Enumerate Ethernet, Wi-Fi, VPN (OpenVPN TAP/DCO, WireGuard), virtual adapters
      (Hyper-V, VMware, Wi-Fi Direct) — all visible via `INetworkAdapterProvider`
- [x] Default adapter detection (up + gateway preferred, never "first adapter"; tunnel/virtual excluded from default)
- [x] Audit passed: ID/name/description/type/status, up/down, gateway awareness,
      all-adapters mode (down adapters kept), per-adapter rates, connect/disconnect,
      no duplicate logical entries
- [x] Gap fixed: OpenVPN TAP/DCO (interface type `HighPerformanceSerialBus`=53)
      were classified `Unknown` → description-aware classification (Tunnel/Virtual)
- [x] Tests: 80 passing (type + description-aware kind mapping, phantom-Unknown
      filter relaxation, TAP default-selection guard)
- **Status: done**
- **Commit:** `2bf03c9` (code + tests), `docs/PROJECT_STATUS.md` hash pointer

### TL-005 Dashboard
- [x] Live Download/Upload/Total cards from real collector rates (`SpeedSampleReady` + `AggregateRates`)
- [x] Adaptive rate formatting (B/s, KB/s, MB/s) + Mbps cards (`DataRateFormatter` in Core)
- [x] Active/preferred adapter card (name, kind, connected/disconnected) via default-selector
- [x] Per-adapter list (name, kind, up/down, current up/down rates); tunnels stay visible
- [x] System cards use the ADR-009/ADR-010 aggregate policy (tunnels excluded, no double count)
- [x] MVVM: DashboardViewModel consumes abstractions via DI, no networking in code-behind,
      UI updates marshalled to Dispatcher, events fully unsubscribed (IDisposable)
- [x] Localization: en + fa-IR resources for Dashboard/Download/Upload/Total/Active Adapter/
      Connected/Disconnected/Network Adapters/No active connection + kind names; RTL-safe layout
- [x] Connection state: no-network, disconnect, reconnect, VPN-only host, no-sample window — no crash
- [x] No polling in VM, no unbounded history, scalar-only rate updates per second
- [x] Tests: 15 formatter cases (Network.Tests) + 8 App.Tests (aggregate→VM, adapter→VM,
      no-network, reconnect, VPN-only honesty, culture switch, resource keys)
- [x] Real Windows GUI verification: dark dashboard renders live rates under real traffic
      (download 0 B/s → 844 KB/s / 6.92 Mbps → decaying), en + fa-IR both render and exit cleanly
- **Status: done**
- **Commit:** `bb6deef` (see `docs/PROJECT_STATUS.md`)

### TL-006 Live Traffic Graph
- [x] Real-time download/upload graph (30s / 1m / 5m ranges) with adaptive Y scale
- [x] Never block UI thread (collection thread writes, ViewModel marshals to
      Dispatcher, `OnRender` drawing only)
- [x] Bounded ring-buffer history (5.5 min retention, 1320 samples max, thread-safe;
      dedupe rejects same-poll duplicates; capacity/time retention enforced)
- [x] Time ranges with localized controls; switching never clears history
  (`TrafficSampleBuffer.Slice` is non-mutating)
- [x] Adaptive shared scale: immediate spike growth, hysteretic shrink (sustained
      2-update low), 2 KB/s floor (no divide-by-zero at zero traffic)
- [x] Native WPF rendering (`TrafficGraphControl` FrameworkElement + `OnRender`),
      two `StreamGeometry` series, no chart library, no per-sample UI elements
- [x] Timeline always oldest-left → newest-right even in RTL (control forces LTR)
- [x] No-network / disconnect / reconnect: no crash, history survives, ages out by
      retention, reconnect resumes; never fabricates non-zero data
- [x] Raw bytes/sec in graph data (Core graph models); formatting only at render
      time via `DataRateFormatter`
- [x] Localization: en + fa-IR for head, range and legend strings
- [x] Tests: Core graph (buffer/scale) + VM graph (append/dedupe/slice/reset/zero/large)
      + localized graph keys
- [x] Real Windows GUI verification: graph renders under live traffic, range buttons
      30/60/300 switch, en + fa-IR switch, resize, clean shutdown — no exceptions
- **Status: done**
- **Commit:** (see `docs/PROJECT_STATUS.md`)

### TL-007 Per-Process Traffic
- [x] ETW research grounded in `KernelTraceEventParser` source: payload PID fixup,
      size field, event IDs; Tcp/Udp + IPv4/IPv6 handlers; retransmit (id 14) excluded
- [x] `WindowsEtwProcessTrafficCollector` — real-time kernel network session
      (TCP/UDP, IPv4/IPv6), PID+size mapped into `NetworkTransferEvent`, ~1 s
      snapshot loop, `SamplesReady`/`GetCurrentSamples`; no payload capture
- [x] `ProcessTrafficAccountingEngine` — per-instance buckets by `(pid, start)`,
      monotonic sliding-window rates, metadata resolve/rekey, PID-reuse isolation,
      unknown processes kept in their own `<unknown pid N>` bucket (never merged),
      120 s idle prune + 4096 cap, no per-event allocation/logging/UI work
- [x] `WindowsProcessMetadataProvider` — guarded `Process` reads, PID-reuse
      detection via start-time mismatch, never throws
- [x] Collector health states: Stopped/Starting/Running/PermissionDenied/Failed;
      non-elevated → `PermissionDenied` + `LastError`, no crash, no forced UAC
- [x] Core contracts: `IProcessTrafficCollector` (+Status/LastError),
      `ProcessTrafficSample` (pid/start/name/path/totals/rates),
      `ProcessTrafficCollectorStatus`
- [x] DI registration (`IProcessTrafficCollector` singleton, Network layer owns the
      provider/session); VPN semantics documented, ADR-009/010 policy unchanged
- [x] Tests: 18 new (13 accounting-engine synthetic-event suite incl. PID reuse,
      rates, process exit, eviction, protocol/IP classification; 4 metadata
      provider; 1 graph count) — engine tested without any real ETW session
- [x] Real Windows verification: non-elevated → `PermissionDenied` evidence;
      elevated live run with curl.exe + powershell.exe → two distinguishable apps,
      two curl instances as distinct buckets, bounded (11 samples, ~2.6 MB growth),
      clean stop; protocol totals invariant `Total = Tcp + Udp = IPv4 + IPv6`
- [x] Applications list UI (M4 UI milestone):
  - [x] `ApplicationsViewModel` + `ProcessRowViewModel` — per-instance rows keyed
        by `ProcessInstanceId` (same PID + new start time = distinct row), in-place
        update (no re-add/flicker), top-consumer cards (now/download/upload),
        localized process status (`Running`/`Exited`/unknown)
  - [x] Sort (7 keys via `ProcessSampleSelection`/`ProcessSortKey`, Core, pure) +
        search (name case-insensitive substring + PID prefix); never mutates
        collector state
  - [x] Permission UX: `PermissionDenied`/`Failed`/`Stopped` banners + monitoring
        actions; explicit Restart-as-Administrator only — no auto-elevation (ADR-014)
  - [x] `ProcessIconResolver` — shell32 `SHGetFileInfo` P/Invoke, frozen fallback,
        bounded FIFO cache, UI-thread only (no System.Drawing)
  - [x] `MainWindow` Dashboard/Applications navigation + `ContentControl` host;
        process collector started at startup (App.xaml.cs)
  - [x] `DataSizeFormatter` (Core binary totals, culture-aware, negatives clamped)
  - [x] Localization: en + fa-IR for all Applications keys
  - [x] Tests: 11 selection/sort + 12 formatter cases (Network) + 16
        ApplicationsViewModel (App) — 45 new tests
  - [x] Real Windows GUI verification: elevated live run (ETW Running, ~20 MB/s
        survived, session closed cleanly on graceful close) and non-elevated run
        (PermissionDenied banner path, no session, no auto-UAC); 192/192 tests;
        0 warnings/errors Debug + Release
- **Status: done (backend + Applications UI)**
- **Commit:** `8f080fb` (code + tests; see `docs/PROJECT_STATUS.md`)

### TL-007F Shutdown deadlock fix (lingering process after graceful close)
- [x] Root cause (live repro + `dotnet-dump`, non-elevated): `App.OnExit` → DI
      disposes `WindowsNetworkTrafficCollector` via `StopAsync().GetAwaiter().GetResult()`
      on the WPF dispatcher thread; `await loop;` captured the
      `DispatcherSynchronizationContext`, so the continuation was posted to the
      blocked dispatcher → `StopAsync` never resumed, `App.OnExit` never returned,
      process lingered (no window, low CPU, one foreground thread)
- [x] Fix: `await loop.ConfigureAwait(false)` — deterministic shutdown from any
      thread/context; no `Environment.Exit`, no forced kill (ADR-015)
- [x] Regression test `Dispose_FromNonPumpingSyncContext_DoesNotDeadlock`
      (non-pumping `SynchronizationContext` + 5 s deadline) — fails (timeout)
      pre-fix, passes post-fix
- [x] Build Debug + Release: 0 warnings / 0 errors; tests **193/193** (163 Network
      + 30 App)
- [x] Real Windows GUI verification (Release):
      - Non-elevated: 3× launch → graceful close, each exits promptly; no residual
        process; no ETW session; log ends `TrafficLens exiting` → `Process traffic
        collector stopped` → `Network traffic collector stopped` (previously
        missing line)
      - Elevated: ETW session `TrafficLensProcessTrace` Running + ~20 MB download →
        graceful close → process exits, session gone from `logman query -ets`
- **Status: done**
- **Commit:** `e09111e` (code + tests; docs `f425dca`)

### TL-008 Active Connections
- [x] Core: extended `ConnectionInfo` (nullable remote endpoint, `ConnectionAddressFamily`,
      process start-time identity, executable path, icon availability, timestamp),
      `ConnectionKey` (protocol + family + local + remote + pid), `ConnectionProtocol`/
      `ConnectionState`/`ConnectionAddressFamily` enums, `EndpointFormatter`
      (LTR, culture-safe; unspecified/absent peer rendered empty — ADR-016)
- [x] `IConnectionProvider` extended (`ConnectionsChanged`, `LastError`,
      `GetCurrentConnections`, `GetActiveConnectionsAsync(ct)`, `StartAsync`,
      `StopAsync`, `IDisposable`)
- [x] Native collection `NativeConnectionTableReader` — `GetExtendedTcpTable`
      (`TCP_TABLE_OWNER_PID_ALL`) + `GetExtendedUdpTable` (`UDP_TABLE_OWNER_PID`),
      IPv4 + IPv6, 4-byte little-endian entry-count header, per-row layouts (TCPv4 24 B,
      TCPv6 56 B, UDPv4 12 B, UDPv6 28 B), network→host port byte order, 64 KB initial
      buffer grown on `ERROR_INSUFFICIENT_BUFFER`; partial-table failure tolerated
- [x] `ConnectionTableParser` — pure static parsers over the native buffers (unit-tested
      with synthetic payloads, no live table needed)
- [x] `WindowsConnectionProvider` — ~1 s off-UI poll loop; partial-table failure is a
      warning (successful tables kept); total failure keeps the last good snapshot + sets
      `LastError`; any success clears `LastError`; `StopAsync` uses `ConfigureAwait(false)`
      (ADR-015)
- [x] `ConnectionProcessResolver` — bounded cache (TTL 3 s, capacity 512, FIFO eviction,
      negative caching) over `IProcessMetadataProvider`, keyed by full `ProcessInstanceId`;
      never throws
- [x] Filter/search/sort (Core, pure, non-mutating): `ConnectionFilter`
      (All/Established/Listening/Tcp/Udp/Ipv4/Ipv6), `ConnectionFiltering`,
      `ConnectionSort` (Default/Process/ProcessId/Protocol/State/Local/Remote) with
      deterministic tie-breaks; UDP remotes are never fabricated, LISTEN is not outbound
- [x] App: `ConnectionsViewModel` + `ConnectionRowViewModel` — dispatcher-marshalled
      `ConnectionsChanged`, in-place row updates (rebuild only when the key sequence
      changes), icon budget per refresh, error banner, empty state, Filter + FamilyFilter
      + Sort + search
- [x] `ConnectionsView` (XAML + code-behind DI); `MainWindow` Dashboard/Connections
      navigation; `App.xaml.cs` registers the VM/View and starts `IConnectionProvider`
- [x] Localization: en + fa-IR keys (column headers, filters, sort, TCP states, empty/
      error, unknown process); endpoints stay LTR under RTL
- [x] Tests: `ConnectionTableParserTests`, `ConnectionKeyTests`, `ConnectionSelectionTests`,
      `EndpointFormatterTests` (Network) + `ConnectionsViewModelTests` (App)
- [x] Real verification (`--connections`): 112 connections (78 TCP / 34 UDP, 99 IPv4 /
      13 IPv6), in-process listener observed as `Listen`, curl download attributed
      `Established` with correct PID/name/remote; cross-checked vs `netstat -ano`
      (TCP state histogram + UDP count match the MIB source — `Get-NetTCPConnection`'s
      `Bound` rows are cmdlet-synthesized, not in the owner-PID table)
- [x] GUI smoke: `Connection provider started (IP Helper tables, poll interval 00:00:01)`,
      no exceptions, clean teardown (no orphan ETW session / leftover process)
- **Status: done**
- **Commit:** `c29adf4` (code + tests; see `docs/PROJECT_STATUS.md`)

### TL-009 SQLite History
- [x] Core history domain (`TrafficLens.Core/History`): `HistoryRange`,
      `TrafficUsage`, `DailyUsagePoint`, `HistorySnapshot` (immutable, `.For(range)`),
      `TrafficHistoryBucket`, `HistoryRangeCalculator` (half-open local-date ranges;
      DST/local-midnight correct via `TimeZoneInfo`), `TrafficHistoryAccumulator`
      (baseline-only first observation, non-negative deltas, per-UTC-minute buckets,
      `DrainCompleted`/`DrainAll`), `ITrafficHistoryRepository`, `ITrafficHistoryService`
- [x] Infrastructure persistence (`TrafficLens.Infrastructure/History`):
      `SqliteTrafficHistoryRepository` (schema v1 via `PRAGMA user_version`; tables
      `traffic_samples` + `daily_usage`; WAL + busy_timeout; `Pooling=false`;
      semaphore-gated writes; `INSERT OR IGNORE` + `changes()==1` → restart can never
      duplicate history; `daily_usage` upsert; 90-day raw prune on startup),
      `TrafficHistoryService` (rides existing `CounterSampleReady` — never a second
      poll loop; adapter-kind map refreshed on `AdaptersChanged`; tunnel exclusion by
      default mirrors ADR-009/010; 30 s flush loop; `StopAsync` drains + flushes;
      cached immutable `HistorySnapshot` so SQL never runs on the UI thread),
      `HistoryServiceCollectionExtensions.AddHistoryServices(dbPath)`
- [x] `AppPaths` — database under `%LOCALAPPDATA%\TrafficLens\data\trafficlens.db`;
      `EnsureDirectories` creates the `data` folder
- [x] App History page: `HistoryViewModel` (five ranges, summary cards, banner when
      unavailable), `HistoryView`, native `HistoryBarChartControl` (FrameworkElement,
      always oldest-left → newest-right under RTL; Today/Yesterday = 1 bar, 7d = 7 bars,
      30d/Lifetime = 30 bars), `MainViewModel.ShowHistoryCommand` + nav label,
      `MainWindow` History host, DI + start in `App.xaml.cs`
- [x] Localization: en + fa-IR keys (HistoryLabel, HistoryDailyTrafficLabel,
      TodayLabel, YesterdayLabel, Last7DaysLabel, Last30DaysLabel, LifetimeLabel,
      HistoryNoDataLabel, HistoryUnavailableLabel); reused download/upload/total labels
- [x] Tests: `TrafficLens.Infrastructure.Tests` (new project, added to solution) —
      range calculator, accumulator, SQLite repository (temp DBs, idempotent append,
      daily/lifetime queries, prune, reopen/schema), service (delta→flush→shutdown,
      restart no-duplication, tunnel exclusion) — 27 tests
- [x] App tests: `HistoryViewModelTests` (6) + localization resource keys (en + fa)
- [x] Real Windows verification (`--history`): live collector + real DB; 20 MB curl
      download recorded as ~20.18 MB Today with peak 5.78 MB/s; second service
      instance against the same DB → Lifetime unchanged (restart idempotency)
- [x] Docs: `DATABASE.md` finalized, `ARCHITECTURE.md`, `PROJECT_STATUS.md`,
      `CHANGELOG.md`, ADR-017
- **Status: done**
- **Commit:** `861269c` (see `docs/PROJECT_STATUS.md`)

### TL-010 Floating Widget
- [x] `FloatingWidgetViewModel` — subscribes to the existing `SpeedSampleReady`/
      `NetworkChanged`/`AdaptersChanged` events (never its own poll loop or timer),
      computes the ADR-009/010 aggregate via `NetworkTrafficAggregator.AggregateRates`,
      formats Download/Upload/Total through `DataRateFormatter` (LTR units), localized
      labels (FloatingWidgetLabel, DownloadLabel, UploadLabel, TotalRateLabel),
      `TogglePinCommand` (raises `PinStateChanged`) + `CloseWidgetCommand` (raises
      `CloseRequested`), UI-thread marshalling like the dashboard, IDisposable
- [x] `FloatingWidgetWindow` (280×110, `WindowStyle=None`, `ResizeMode=NoResize`,
      `ShowInTaskbar=False`, `Topmost` from settings) — dark theme via existing
      DarkTheme.xaml brushes, drag by empty area (`DragMove`, ignores clicks on
      buttons), pin toggle button + hide (✕) button bound to commands, values forced
      `FlowDirection=LeftToRight` under RTL; only necessary code-behind is drag
- [x] `FloatingWidgetService` (singleton) — owns the single widget instance:
      `Show`/`Hide`/`Toggle`/`RestoreIfEnabled`, idempotent re-show (activates, never
      duplicates), widget-close = hide only (`Closing` cancelled), position
      persisted as `FloatingWidgetLeft`/`FloatingWidgetTop`, always-on-top persisted
      as `FloatingWidgetAlwaysOnTop` (default on), startup visibility persisted as
      `FloatingWidgetEnabled`, `Dispose` removes the cancel-handler and closes the
      window for real (shutdown cannot be pinned open)
- [x] `WidgetPositionHelper.Clamp` — pure multi-monitor clamping: union of monitor
      work areas, negative virtual-screen coordinates preserved (secondary monitor
      left of primary), off-screen / monitor-disconnected recovery, window larger
      than work area collapses to top-left; real areas from
      `SystemParameters.VirtualScreen*`
- [x] Main UI: toggle button in the `MainWindow` header
      (`Show Floating Widget` / `Hide Floating Widget` exchange based on
      `IsVisibleChanged`), `MainViewModel.ToggleFloatingWidgetCommand` +
      `FloatingWidgetToggleLabel`, `MainWindow.Closing` → `Hide()` the widget +
      dispose viewmodel; `App.xaml.cs` registers the service + `RestoreIfEnabled()`
      on startup
- [x] Shutdown safety (ADR-015/TL-007F): widget is a secondary window; main-window
      close hides it, then `OnLastWindowClose` shuts the app down normally — no
      `Environment.Exit`, no hidden window holding the process, no new foreground
      thread; no orphan ETW sessions
- [x] Localization: en + fa-IR keys (FloatingWidgetLabel, AlwaysOnTopLabel,
      ShowFloatingWidgetLabel, HideFloatingWidgetLabel); Download/Upload/Total reused
      (`DownloadLabel`/`UploadLabel`/`TotalRateLabel`)
- [x] Tests: `FloatingWidgetViewModelTests` (aggregate→VM mapping, tunnel/down
      exclusion, formatting, culture re-format + labels, pin icon, dispose) +
      `WidgetPositionHelperTests` (inside/off-screen right&bottom/negative
      coords/secondary-monitor-left/multi-monitor union/empty/larger-than-work-area)
      + localization resource keys — 18 new (App 51 → 69; total 284 → 302)
- [x] Build Debug + Release: 0 warnings / 0 errors
- [x] Real Windows GUI verification: show/hide, live rate changes, dashboard
      compatibility, drag, restart → position restored, topmost toggles + persists,
      repeat show no duplicate, main close → widget gone → process exits, no orphan
      ETW session; en + fa-IR render
- **Status: done**
- **Commit:** (see `docs/PROJECT_STATUS.md`)

### TL-011 System Tray — **done** (see the completed block at the top)
### TL-012 Alerts
- [ ] Local alert architecture (usage thresholds, notifications)
- **Status: done** (see the completed block at the top)

### TL-013 Settings
- [ ] Settings UI and persistence
- **Status: done** (see the completed block at the top)

### TL-014 Stability & Performance Audit — **DONE**
- [x] Harness F1: warm-up + idle (60 s) — dashboard/connections/history/GC handles
      pass; WS +8 MB, handles +6, threads +9
- [x] Harness F2: 30-min soak — max CPU ≤15% single-core in 30 s windows
      (achieved avg 3.43%, max 9.88%)
- [x] Fix 1 — DashboardViewModel event coalescing: `_refreshPending` flag +
      `CoalesceRefresh()` merges all per-adapter `SpeedSampleReady` events into
      one `RefreshRates` call per second (N events/s → 1 layout pass/s)
- [x] Fix 2 — ConnectionsViewModel page-visibility gating: `_isActive` flag,
      `SetActive(bool)` wired from `MainViewModel.SelectPage`; inactive state
      stores pending data without dispatching to UI thread
- [x] Fix 3 — WindowsConnectionProvider polling pause: `SetPollingEnabled(bool)`
      on `IConnectionProvider` skips `EnumerateOnce()` (4 native P/Invokes +
      process resolution for ~159 connections) when Connections page hidden
- [x] Fix 4 — Default deactivation: `MainViewModel` constructor calls
      `Connections.SetActive(false)` since Dashboard is default page
- [x] Pre-fix evidence: main thread 50% on-CPU (22449 samples/45 s) via
      dotnet-trace Speedscope; N events/s dispatch storm confirmed
- [x] Post-fix evidence: main thread 1.9% on-CPU (113 samples/60 s); F2 soak
      30 min: avg 3.43%, max 9.88% (all under 15%)
- [x] Harness F3: memory leak (10 min soak) — WS +5.8 MB, Private +15.5 MB,
      handles -3, threads -9 — PASS
- [x] Harness F4: navigation stress — 50 nav, 25 widget, 25 window, 10 restart
      cycles — PASS
- [x] SingleInstanceGuard (6 tests), FileLoggerProvider retention (3 tests),
      SpeedRateTracker sleep-gap test — all green
- [x] Final: 397 tests (App 145, Network 212, Infrastructure 40), 0 warnings,
      0 errors (Debug + Release)
- [x] Docs: ADR-022, TASKS, PROJECT_STATUS, CHANGELOG, PERFORMANCE.md
- **Status: done**
- **Commit:** (see `docs/PROJECT_STATUS.md` Git Commit section)

### TL-015 Packaging / Installer
- [x] x64 packaging and installer
- [x] `scripts/build-release.ps1` one-command release pipeline
- [x] Single-file self-contained win-x64 publish (`TrafficLens.exe`)
- [x] Inno Setup 6 per-user installer (no PDBs, running-app notice, never force-kill)
- [x] Install / uninstall / reinstall / upgrade verification with user-data preservation
- [x] Installer + portable ZIP SHA-256; artifacts gitignored
- [x] Metadata: clean product version 0.1.0, FileDescription, app.manifest
- [x] Docs: PACKAGING.md, ADR-023, PROJECT_STATUS, CHANGELOG
- **Status: done**
- **Commit:** (see `docs/PROJECT_STATUS.md` Git Commit section)

### TL-016 Localization / Persian UI
- [x] Audit: resx key parity (en + fa-IR), `LocalizationService` (RTL, CultureChanged,
      fallback), runtime switcher already in place (`SwitchToEnglishCommand`/
      `SwitchToPersianCommand`, Settings `LanguageOptions`, persisted via settings)
- [x] No hard-coded user-facing strings: graph "now" label now via `NowLabel`
      DependencyProperty (bound to Dashboard `GraphNowLabel`, default "now");
      all UI text through `Strings*.resx` except endonyms (English / فارسی) and brand
- [x] Error/status surfaces localized instead of raw English provider messages:
      Applications (`PermissionDeniedDetailLabel`, `MonitoringFailedDetailLabel`),
      Connections (`ConnectionsErrorDetailLabel`), History (`HistoryErrorDetailLabel`);
      raw detail stays in app log
- [x] Culture-aware history chart date labels (`ToString(..., CultureInfo.CurrentCulture)`
      so fa-IR uses the Persian calendar)
- [x] en + fa-IR key parity maintained: 146 keys each, identical key sets
      (`LocalizationResourceTests.RequiredKeys` extended)
- [x] Tests updated/added: 398 tests (App 146, Network 212, Infrastructure 40) — localized
      detail assertions, raw English never surfaced, fa-IR detail localization
- [x] Build Debug + Release: 0 warnings / 0 errors
- [x] Real GUI verification (`scripts/tl016-verify.ps1` on published single-file build):
      en-US labels render, fa-IR content scan (nav + dashboard + History page in Persian,
      no English leak), graceful exit, no orphan process/ETW; TL-015 smoke regression
      still passes
- [x] **Branding foundation (TL-016 continuation):** `scripts/generate-icons.ps1` icon
      pipeline → `assets/branding/TrafficLens.ico` (16–256 multi-size) + PNGs; glyph mirrors
      the TL-011 tray art. Wired once in `TrafficLens.App.csproj`
      (`<ApplicationIcon>` + `<Resource>` pack URI); MainWindow icon, FloatingWidget title
      glyph, tray icon (embedded ICO with runtime-drawn fallback), installer
      (`SetupIconFile={#BrandIcon}` + `VersionInfo*` metadata) all consume it;
      `packaging/placeholder.ico` removed. `docs/BRANDING.md` added.
- [x] **About page (TL-016 continuation):** 7th nav item (`WrapPanel` nav, AboutViewModel +
      AboutView, DI singletons); brand identity, version, runtime, OS, display language,
      data/settings/logs paths; all localized en + fa-IR; paths render LTR
- [x] **Diagnostics (TL-016 continuation):** `DiagnosticsInfo.Build` pure builder; version read
      from `AssemblyInformationalVersionAttribute` (no `Assembly.Location` → no IL3000 under
      single-file publish); Copy-diagnostics via `Clipboard.SetText` (non-throwing);
      Open-logs-folder shell action with parent fallback
- [x] Tests: `AboutViewModelTests`, `MainViewModelTests` (nav incl. About),
      `BrandAssetsTests` (ICO header/frames, PNG dimensions); resx en/fa both 161 keys
      (identical sets). Total **410 tests** (App 158, Network 212, Infrastructure 40)
- [x] Build Debug + Release: 0 warnings / 0 errors; full suite green
- [x] GUI verification extended: About reachable in en + fa-IR, no English leak
- **Status: done**
- **Commit:** (see `docs/PROJECT_STATUS.md` Git Commit section)

### TL-017 Performance Audit (M12)
- [x] Phase 0 — capture pre-optimization baselines (`docs/PERFORMANCE_BASELINE.md`, `c709e2a`)
- [x] Phase 1 — Connections page optimization: in-place keyed row updates, delta
      diffing, per-refresh icon budget; dedicated probe CPU avg 15.31% → 6.65%,
      WS drift +24.29 → +9.48 MB/3min, Private drift +23.26 → +8.00 MB/3min
      (`03c47a4`)
- [x] Phase 2 — per-tick allocation reduction (~550 allocs/tick: ~450 row
      strings, ~101 double `ConnectionKey`); GC capture Gen0 0.20/s,
      ~1.53 MB/s alloc, ~0.8% time in GC, bounded heap; no CPU claim (`213a887`,
      `303288e`)
- [x] Phase 3 — lazy six page Views + startup stage instrumentation; internal
      mainVM+6-view block ~170 → ~70 ms; wall-clock explicitly NOT claimed
      (`cbc8aea`, `0db764e`)
- [x] Phase 4 — SQLite persistent writer + prepared commands +
      `lifetime_totals` schema v2; flush avg 7.4 → 1.3 ms / p95 9 → 2 ms;
      QueryLifetime O(n) → O(1) (`737fb3f`)
- [x] Phase 5 — `SetActive` gating for hidden widget/tray surfaces +
      dispose/recreate; idle allocation ~2.2 → ~0.5–1.1 MB/s, WS drift
      +4.7 → +2.4 MB/5min, threads 16 → 15; idle CPU avg 1.08 → 1.07 within
      noise (`843f343`)
- [x] Phase 6 — soak harness (`scripts/tl017-soak.ps1`, `abaf129`): 60-min idle
      tray (CPU avg 0.97%, WS ~240 MB flat) + 30-min Connections (WS/Private
      decelerating); "no leak observed in the measured window" (not a 24h soak)
- [x] Correctness preserved: accuracy, byte accounting, tunnels, ETW identity,
      history UTC/DST, alerts, polling intervals, payload policy, monitoring-only
- [x] Elevated ETW long-run **not measured** (non-elevated env; documented as
      remaining manual validation)
- [x] Final verification (2026-09-19): 416/416 tests (App 163 / Network 212 /
      Infrastructure 41), Debug+Release 0W/0E, republished exe, tl015-smoke +
      tl016-verify + tl017-connprobe PASS, packaging pipeline OK at v0.1.1
      (no tag/bump), all benchmark harnesses runnable
- [x] Defects found + fixed during Phase 7: `tl017-db-benchmark.ps1` build-order
      fix (harness); schema v1→v2 migration lost pre-upgrade Lifetime total
      (production data-correctness) — back-fill from `daily_usage` + regression
      test
- [x] Docs: `docs/PERFORMANCE_AFTER.md`, PROJECT_STATUS, TASKS, CHANGELOG,
      link from PERFORMANCE.md, DATABASE.md (schema v2 + migration)
- **Status: done** (merged into `master` at `21dabb9`)
- **Commit:** see `docs/PROJECT_STATUS.md` Git Commit section

### TL-027 Final human UI polish (v0.1.4) — **AUTOMATED PASS, HUMAN PENDING**

Scope limited to the six defects found in human review of `99397a4`. No backend,
collector, monitoring or packaging changes; no new timer, poller or background worker.

- [x] **Title bar / caption buttons** — padding resolved from the live
      `AppWindow.TitleBar.LeftInset`/`RightInset` and the window DPI, reapplied on
      presenter, size and position change. Geometry-driven, so EN and FA RTL both
      work in normal and maximized states with no per-language margin.
- [x] **Widget X button** — native close raises `UserCloseRequested`; the service
      turns it into `SetEnabled(false)`, which persists the existing setting.
      Closes only the widget: main window, tray and monitoring unaffected.
- [x] **Settings quick widget toggle** — new control at the top of the page; both
      switches are views of one idempotent state owner, so they cannot drift.
- [x] **Alerts cards** — five rules as uniform cards in a wrapping grid, cooldown
      in its own section, Accent-style Save. Rule identity, order, thresholds,
      units, validation and save path unchanged.
- [x] **Navigation icons** — all seven items use built-in Segoe Fluent Icons; the
      pane collapses to a native icon-only width; tooltip and accessibility name
      come from the same localized label. No external icon library.
- [x] **Widget movement** — clamped to the work area of the monitor it is on using
      the real DPI-scaled `AppWindow.Size`; saved positions on a disconnected
      monitor recover into the nearest remaining work area; pointer delta scaled so
      DIPs are never mixed with physical pixels.
- [x] Bugs found and fixed while implementing the above: `SelectWorkArea` could let
      a zero-overlap area override a positive-overlap one (a widget near the right
      edge of one monitor would jump to the next); `RestoreIfEnabled` reloaded the
      state and then called the idempotent `SetEnabled(true)`, so an enabled widget
      was never recreated on start-up; the drag delta mixed DIPs with physical
      pixels; `AppWindowChangedEventArgs` has no `DidLayoutChange`, so the caption
      safe area was not re-applied on resize.
- [x] Focused tests: 95 passed, 0 failed (`TrafficLens.WinUI.Tests`)
- [x] Full solution suite: 661 passed, 0 failed (App 270 / Network 212 /
      Infrastructure 84 / WinUI 95)
- [x] Release x64 build: 0 warnings, 0 errors
- [x] Minimum publish check: 815 files, no PDBs, no test assemblies, PRI present
      and valid, canonical icon unchanged, `fa-IR` satellite present
- [ ] **Human verification of all six items — PENDING**
- [ ] Installer/ZIP regeneration — deferred until human verification passes; the
      v0.1.4 artifacts built from `99397a4` are superseded and are not final
- **Status: automated checks pass, awaiting human verification**
- Not merged, tagged, pushed or published.

### TL-028 Alert threshold editing and Floating Widget quick control (v0.1.4) — **AUTOMATED PASS, HUMAN PENDING**

Two human-found functional defects, scoped strictly to those two. No alert backend,
collector, monitoring or packaging changes; no new timer, poller or background worker.

- [x] **Alert thresholds visible and editable** — every rule card renders its numeric
      threshold at a real height again. `ItemsWrapGrid` item height 104 → 120 with
      auto rows above a trailing spacer (the old `*` row above the editor crushed it
      to ~6 px), and the editor is no longer gated on the rule's own enabled state,
      which all five rules ship as disabled. `ThresholdText` and `UnitIndex` remain
      two-way; LTR digits with right alignment; `KB/s, MB/s, GB/s` for the two speed
      rules and `MB, GB, TB` for the three limit rules; parse, validation, range and
      save mapping untouched, so the backend reads the same persisted values.
- [x] **Floating Widget quick control in the shell** — a compact native
      `ToggleButton` (Segoe Fluent Icons glyph + localized label) in the top-left of
      the title bar, available on every page, NOT a navigation item, NOT a separate
      page, and not buried in Settings. The title-bar grid is pinned left-to-right so
      the control stays physically top-left in both directions; the title keeps its
      own direction/alignment so the Persian title still sits against the navigation
      edge and clear of the caption buttons.
- [x] One widget-enabled state: a click calls `SetEnabled` (persists the existing
      setting, shows/hides the window) and the service's `EnabledChanged` /
      `IsVisibleChanged` events push the result back. Settings switches and the
      widget's native X already use the same service, so shell ↔ Settings ↔ widget
      cannot disagree, and restart shows the persisted value. No second flag, no
      second service, no polling. Always On Top deliberately stays in Settings only.
- [x] Human clarification honoured: the `Floating Widget` `NavigationViewItem` and
      the dedicated `FloatingWidgetPage` created for that mistaken UX were removed,
      with the page and its now-unused description resource deleted. Navigation is
      back to Dashboard / Applications / Connections / History / Alerts / Settings /
      About. The widget window, service and settings were not touched.
- [x] Wording aligned: `AlwaysOnTopLabel` is now "Always On Top" /
      "همیشه روی سایر پنجره‌ها" and the quick-control label is "ویجت شناور". The resource
      is shared with the WPF surface, so its pinned test expectation was updated to
      the new casing rather than the value being reverted.
- [x] Focused tests: 144 passed, 0 failed (`TrafficLens.WinUI.Tests`)
- [x] Full solution suite: 710 passed, 0 failed (App 270 / Network 212 /
      Infrastructure 84 / WinUI 144)
- [x] Release x64 build: 0 warnings, 0 errors
- [ ] **Human verification of the two items — PENDING**
- [ ] Installer/ZIP regeneration — deferred until human verification passes; no
      artifact was produced for this change and the v0.1.4 artifacts built from
      `99397a4` remain superseded and are not final
- **Status: automated checks pass, awaiting human verification**
- Not merged, tagged, pushed or published.

## Milestones

| Milestone | Title | Tasks | Status |
|---|---|---|---|
| M0 | Project bootstrap, docs, localization foundation | TL-001 | Done |
| M1 | Global network monitoring | TL-002, TL-003 | Done |
| M2 | Dashboard and live graph | TL-005, TL-006 | Done |
| M3 | Network interfaces | TL-004 | Done |
| M4 | Per-process traffic | TL-007 | Done |
| M5 | Active connections | TL-008 | Done |
| M6 | SQLite history | TL-009 | Done |
| M7 | Tray and widget | TL-010, TL-011 | Done |
| M8 | Alerts and settings | TL-012, TL-013 | Done |
| M9 | Stability & performance | TL-014 | Done |
| M10 | Packaging / installer | TL-015 | Done |
| M11 | Full Persian localization | TL-016 | Done |
| M11a | Product polish / branding & About | TL-016 continuation | Done |
| R1 | Release v0.1.1 (first post-TL-016 release) | — | Done (tag `v0.1.1`) |
| M12 | Performance audit (Connections, allocations, startup, SQLite, idle, soak) | TL-017 | Done (merged into `master` at `21dabb9`) |
| M13 | Current-day hourly history | TL-018 | Done (branch `feature/tl018-hourly-history`, ready to merge on approval) |