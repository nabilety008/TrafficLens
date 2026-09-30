# TrafficLens — CHANGELOG
  
All notable changes are documented here in reverse chronological order.

## [Unreleased] — Post-v0.1.4 batch 4: Windows feature update hold without disabling security updates

The Windows Update control is redefined. The old behavior could broadly disable
automatic Windows updates (`NoAutoUpdate=1`), silently holding back security and
quality fixes. TrafficLens now uses only Microsoft's supported **Target Feature
Update** policy: the machine is held on its current Windows feature version
while security updates, quality updates, Defender updates and all Windows
Update servicing continue uninterrupted.

### Changed

- **Feature-update hold replaces the update kill switch.** Applying the option
  writes exactly `ProductVersion`, `TargetReleaseVersion` (DWORD 1) and
  `TargetReleaseVersionInfo` (the detected current release) under
  `HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate` via one elevated
  `reg.exe` invocation (UAC). Windows Update services, BITS, Defender,
  quality-update pause/deferral and safeguard holds are never touched, and
  `NoAutoUpdate` is never written (asserted in tests).
- **The target release is detected from the live machine, never hardcoded.**
  Product comes from the build number (>= 22000 is Windows 11, ignoring stale
  `ProductName` strings); the release comes from `DisplayVersion` with a
  `ReleaseId` fallback, strictly normalized. Incomplete or unreadable detection
  refuses to apply anything.
- **Ownership, snapshot and rollback.** A schema-versioned record
  (`%LOCALAPPDATA%\TrafficLens\windowsupdate-state.json`) snapshots every
  previous value before any write; releasing restores exactly those owned
  previous states and removes the TrafficLens-created policy key when it is
  empty. A partial apply failure rolls back owned values; a UAC cancel is a
  clean no-op.
- **External policy is respected.** Any pre-existing target-release value
  TrafficLens does not own — including an external `NoAutoUpdate` — is reported
  as organization-managed and never overwritten. Legacy records from the old
  behavior are migrated: releasing removes TrafficLens's old `NoAutoUpdate`
  value by restoring its snapshotted previous state.
- **Reads no longer require elevation** (read-only registry handles); only the
  apply/release mutations prompt via UAC.
- **UI (Settings, EN + fa-IR).** New strings: "Hold feature updates" / "Release
  feature updates", "Held by TrafficLens" / "Released by TrafficLens",
  "Current version: Windows 11 — 25H2"; Persian: "جلوگیری از ارتقای نسخه
  ویندوز" / "اجازه دادن به ارتقای نسخه ویندوز", "نگه داشته شده توسط
  ترافیک‌لنز" / "آزاد شده توسط ترافیک‌لنز", "نسخه فعلی: …". A note states that
  security and quality updates continue while the version is held.

### Verification

- 52 new Infrastructure tests over registry fakes (detector, pure hold planner,
  command builder, and an end-to-end service suite that executes the generated
  `reg.exe` command strings against a fake registry); full solution suite
  **832 passed, 0 failed** (270 App / 237 Network / 136 Infrastructure /
  189 WinUI); Release x64 build **0 warnings, 0 errors**.
- Runtime verified non-mutating on the Release build: the Settings page shows
  the detected target ("Windows 11 — 25H2" on build 26200 despite a stale
  "Windows 10 Pro" `ProductName`), correct fa-IR state strings and the hold
  action. The live elevated hold/release round-trip was **not performed**
  (elevation approval was not granted); the machine's registry is untouched.

## [Unreleased] — Post-v0.1.4 polish batch 3: native caption-button colors and Alerts layout (human verification pending)

Post-release polish scoped to the real Windows caption buttons and the Alerts
page layout. No collector, polling, alert-behavior, packaging or artifact
changes, and no new timer, poller or background worker. Title-bar geometry is
untouched.

### Fixed

- **The native Minimize / Maximize / Close glyphs could be unreadable over the
  custom title bar.** The window draws its own background behind the real
  caption buttons (extends-content-into-title-bar), so the system default
  button colors no longer match — in a dark window the glyphs could render
  black on the dark title bar. The buttons are still the native ones; their
  per-state colors are now resolved from one theme-aware palette: transparent
  backgrounds, resting glyphs matching the title text (white in dark,
  near-black in light), dimmed glyphs on an inactive window, and hover/pressed
  as non-opaque overlay plates that step stronger. Close keeps Windows' own
  treatment. The palette re-applies when the theme or the activation state
  changes (event-driven only).

- **The Alerts page's cooldown and Save area felt detached.** The cooldown
  fields, validation, saved feedback and Save button sat in a tall, mostly
  empty card of their own, so Save appeared to float. They now form one
  compact settings/action bar: cooldown field and unit beside a status column
  that shows the validation error, the allowed range or the saved notice, and
  the Save button at the end of the same row. Narrow windows wrap the bar into
  stacked rows. No alert rule, threshold, toggle, validation rule, persistence
  path or localization key changed — this is a XAML re-layout.

- **Alerts settings regrouped into one configuration container after the human
  visual review failed.** The intermediate bar still let Save read as belonging
  to the cooldown subsection and left the five rule cards floating ungrouped.
  The page now has two clear containers: one main "Alerts configuration" card
  that owns the five rule sections (as smaller internal sub-cards), a divider,
  the cooldown subsection as one setting among many with no Save control of
  its own, another divider, and a full-width footer holding the validation
  error, the saved notice and the single global Save action at the page-flow
  edge (mirrors naturally in fa-IR RTL); and a separate informational
  "Triggered alerts" container below with its own empty state. Pure XAML
  re-layout — behavior, bindings and persistence unchanged.

### Verification

- Focused tests: 6 new `CaptionButtonPaletteTests` (dark/light × active/
  inactive), updated Alerts markup guards pinning the regrouped layout (Save
  inside the global footer of the main configuration card, not the cooldown
  subsection; triggered alerts in their own container), and 1 new container
  guard; full solution suite **780 passed, 0 failed** (270 App / 237 Network /
  84 Infrastructure / 189 WinUI); Release x64 build **0 warnings, 0 errors**.
- Runtime on the real Release build (dark theme): Alerts verified at normal,
  narrow and maximized widths in fa-IR and en-US with all five rules; Save
  through the new bar persists through the existing settings path; caption
  buttons show the recolored states with no title overlap and no geometry
  regression; whole-run log 0 Error. Caption glyph readability in light theme
  and on hover/pressed is pending human visual review.

## [Unreleased] — Post-v0.1.4 polish batch 2: live graph measurement scale and hover details (human verification pending)

Post-release polish scoped to the live traffic graph and the history bar chart.
No collector, polling, alert, packaging or artifact changes, and no new timer,
poller or background worker. The adaptive scale algorithm is reused unchanged.

### Added

- **Y-axis measurement scale on the live traffic graph.** Three reference levels
  (0, half and full scale) are labelled on the left of the plot in compact rate
  units (B/s → KB/s → MB/s → GB/s, binary 1024) with subtle guide lines at the
  middle and top levels. Labels are derived from the same `ScaleMax` the graph
  already draws with, so they always agree with the plot and follow range
  changes (30s/1m/5m) and the adaptive scale's hysteresis automatically.

- **Hover values on the live graph.** Moving the pointer over the plot shows the
  nearest measured sample (never an interpolation): local time (HH:mm:ss),
  download and upload, in a callout that flips sides near the right edge, with a
  dashed vertical indicator at the sample's time position. Leaving the plot
  clears it; hovering never touches storage.

- **Hover values on the history bar chart.** Moving the pointer over a bar slot
  shows the period (hourly `HH:00–HH:00`, daily `yyyy-MM-dd`), download, upload
  and total for the nearest bar, with a subtle highlight over the slot. Hover
  uses the already-loaded series only — no database queries on pointer move.

### Fixed

- **Graph controls mirrored in the Persian (RTL) layout.** Both graph controls
  now force a left-to-right flow direction, so the timeline stays chronological
  (00:00 on the left), the scale labels sit on the left of the plot and the
  numerals stay in technical LTR notation; only the decimal separator follows
  the active culture.

### Verification

- Focused tests: 25 new Network tests (`GraphScaleAndHoverTests`) and 1 new
  WinUI test (`Series_PopulatesFullPeriodLabels_ForHover`).
- Full solution suite **771 passed, 0 failed** (270 App / 237 Network /
  84 Infrastructure / 180 WinUI); Release x64 build **0 warnings, 0 errors**.
- Runtime verified on the real Release build in fa-IR (dark) and en-US: scale
  labels render correctly in both cultures with LTR numerals and the culture
  decimal separator, track the adaptive scale live (4,88 KB/s at low traffic,
  1,91 MB/s after a spike), the timeline stays chronological in the RTL page,
  and the history page renders hourly bars; whole-run log 0 Error. Hover
  callouts are pending human verification: the agent harness could not deliver
  real pointer-move events to the XAML input pipeline on this machine.

## [Unreleased] — Post-v0.1.4 polish batch 1: Connections defaults, de-duplicated Show filter, reverse DNS removed, CSV export made diagnosable (human verification pending)

Post-release polish scoped to four user-visible items on the Connections page and
the History CSV export. No collector, polling, alert, packaging or artifact
changes, and no new timer, poller or background worker.

### Fixed

- **Connections filter/sort ComboBoxes could show a blank selection after a
  runtime culture switch.** The option collections are cleared and repopulated on
  every language change, which drops a `SelectedValue` ComboBox selection to null,
  and the re-announced enum value cannot re-resolve while the list it refers into
  is being replaced. The three ComboBoxes now bind `SelectedIndex` over
  fixed-order key lists owned by the view model (`FilterIndex`,
  `FamilyFilterIndex`, `SortIndex`), which survives the rebuild because the list
  order is fixed; defaults remain Show = All, Address Family = All, Sort =
  Default, and the enum state stays the single source of truth in both
  directions. No filter or sort architecture changed.

- **"Hide listeners" appeared twice on the Connections page** — once as an entry
  in the Show ComboBox and once as the dedicated persisted checkbox. The Show
  entry is removed (the list is exactly All / Established / Listening / TCP / UDP
  now); the dedicated checkbox is kept with its behavior, `ConnectionsHideListeners`
  persistence and filtering. The Core `ConnectionFilter.HideListeners` member and
  its filter logic are unchanged and still serve the checkbox path.

- **The optional reverse-DNS feature is removed from the shipped WinUI
  Connections page.** The checkbox, the resolved-hostname secondary line in each
  row, the `EnableReverseDns` state and label, the `ConnectionsEnableReverseDns`
  settings handling, the DNS refresh/clear path, the `DnsResolverService`
  constructor dependency and its WinUI DI registration are all gone from the
  shipped surface; a stale value in settings.json is simply ignored. Legacy WPF
  code and `DnsResolverService` remain (still compile-referenced, never
  published) and their localization keys stay in both resx files.

- **History CSV export failures were undiagnosable.** The whole picker/write
  sequence was wrapped in a bare catch that discarded the exception, so the
  generic "check file permissions" banner was the only signal. The write step is
  now separately instrumented and the outer catch logs exception type and HRESULT
  through the existing structured file logger (no sensitive data) before showing
  the localized failure state; cancel is confirmed non-error. Runtime evidence
  collected during verification: the file picker does open and a completed export
  writes a correct UTF-8 file with invariant timestamps and raw byte values; a
  successful export produces no Error entries.

### Verification

- Full solution suite **745 passed, 0 failed** (270 App / 212 Network /
  84 Infrastructure / 179 WinUI); Release x64 build **0 warnings, 0 errors**.
- Runtime UIA-driven verification on the real Release build: fa-IR → en-US →
  fa-IR round trip keeps all three ComboBox selections valid and localized; Show
  list = 5 items; dedicated checkbox present; reverse-DNS control absent. CSV:
  Today and Last-7-days exports to Documents verified on disk (correct header,
  raw byte values, ASCII digits — no fa-IR locale corruption); picker cancel
  leaves no error status; whole-run log 0 Error.

## [0.1.4] — Final release: one widget enable control, caption-safe title bar, shell quick action anchored (human verification passed; final artifacts built from `fd7c176`)

A human screenshot review of the Persian Settings page found that the Floating
Widget could be enabled from two places on the same page. Scoped to that finding and
to the positioning defect it exposed: no alert, backend, collector, monitoring or
packaging changes, and no new timer, poller or background worker.

### Fixed

- **Settings showed a duplicate Floating Widget control.** The page carried a quick
  widget card at the top (heading, label and enable switch) *and* the full widget
  section lower down (enable plus Always On Top). With the persistent shell quick
  action in the physical top-left corner, the top card is redundant, so it has been
  removed outright — not collapsed — along with its own toggle, its `Toggled`
  handler and the two labels the code-behind filled in. The remaining widget section
  is unchanged and still carries `فعال‌سازی ویجت شناور` and
  `همیشه روی سایر پنجره‌ها`; the sections below it moved up and no blank card or gap
  is left behind. `FloatingWidgetLabel` and `EnableFloatingWidgetLabel` are still
  used by the shell control, the tray and the WPF views, so no resource was removed.

- **The shell quick action drifted away from the corner it is anchored to.** The
  caption safe area was applied as padding on the whole title bar grid, so in a
  right-to-left window — where the shell reports the caption inset on the leading
  edge, which is physically the right — that reserve landed on the physical left and
  pushed the control inwards; its position also depended on which side the shell
  reported the caption buttons on. The reserve is now applied to the title as a
  margin on the caption side and the title bar grid keeps no horizontal padding, so
  the control's position depends only on the window's own left edge. Measured at
  150% DPI: constant 29 physical px from the window edge (12 DIP shell margin) while
  resizing, maximized, restored, and with the navigation pane collapsed or expanded,
  and it does not mirror to the right in Persian.
  `TitleBarCaptionLayout.ResolvePadding` is untouched and the title is still held
  clear of the caption buttons.

- **The title ran under Minimize/Maximize/Close when the shell reported no usable
  caption inset.** `AppWindow.TitleBar.LeftInset`/`RightInset` are not reliable
  ground on this host: they have been seen reporting nothing at all for a window
  that still draws the three controls, and reporting only the resize frame, which
  reserves a strip far narrower than the controls. Either way the title was left
  underneath them, overlapping by up to 183 physical px in Persian and 133 px in
  English at a 640 px width. The caption region is now read back off the live window
  with a bounded `WM_NCHITTEST` probe — the region Windows itself gives the controls
  for clicks and drag — and the shell insets are still used wherever they already
  cover it, so a valid inset keeps the native path. `DWMWA_CAPTION_BUTTON_BOUNDS`
  was tried first and rejected: it answers `S_OK` with a zero-width rectangle in both
  the restored and the maximized state. The probe is bounded on both axes: each edge
  is searched over the resize-frame thickness only and each boundary is then found by
  halving, costing a few dozen messages and one extra step per doubling of the window
  width. It reports the physical side the controls are on, so the reserve needs no
  per-language handling, and nothing is hardcoded — it returned 218 physical px at
  150% DPI, identically in the restored and the maximized state. Measured at 150% DPI
  across normal, wider, narrower, maximized and restored: the gap from the title's
  right edge to the caption group is now positive everywhere (**+35** in Persian,
  **+227** and **+44** in English, the latter at 640 px), where the previous build
  measured **-183** and **-133**. The shell quick action still holds 29 physical px
  from the window edge in every state, and the title bar keeps no horizontal padding.
  Because the compositor publishes the caption zones one frame after a resize, the
  measurement is retaken once on the next rendered frame through a handler that
  removes itself, capped at three attempts: no timer, poller, worker or positioning
  loop.

### Changed

- `WidgetToggleSync.Apply`, which existed only to push one value into the *pair* of
  Settings switches, is gone; the shared helper keeps `Resolve`, which both windows
  still use. The two remaining switches — the shell quick action and the Settings
  widget section — are views of the one `FloatingWidgetEnabled` state owned by
  `WidgetEnabledState` and written only through `IFloatingWidgetService.SetEnabled`.

### Verification

- Focused tests: 165 passed, 0 failed (`TrafficLens.WinUI.Tests`).
- Full solution suite: 731 passed, 0 failed
  (App 270 / Network 212 / Infrastructure 84 / WinUI 165).
- Release x64 build: 0 warnings, 0 errors.
- Runtime, one launch, `fa-IR`, PID 5772: the Settings page exposes exactly one
  widget section with both switches and no `SettingsWidgetQuickToggle`; the shell
  control holds 29 physical px relLeft through resize, maximize, restore and pane
  collapse. All five state paths checked live — quick action ON/OFF, Settings
  ON/OFF and the widget's own close button — with the quick action and the Settings
  switch agreeing every time and the setting persisted each time. Navigation remains
  at seven pages with no widget destination.
- Caption geometry re-measured live at 150% DPI in both languages across normal,
  wider, narrower, maximized and restored, with the three native controls at 207
  physical px wide: the title's right edge to caption-group gap is **+35** in
  Persian in every state and **+227** / **+527** / **+44** / **+1745** in English
  (normal / wider / 640 px / maximized), against **-183** and **-133** before the
  fix. The quick action held 29 physical px relLeft in every one of those states.

### Release status

**Human verification passed.** This section is the release entry for v0.1.4; the
three `0.1.4` sections below it record earlier drafts of the same version and are
superseded. The v0.1.4 artifacts generated from `99397a4` are superseded too and must
not be treated as final — the final pair is the one below, built from `fd7c176`.

Final source commit: `fd7c1762361d93c8dff5adb44d091c9dd07c3ec9`.

- `artifacts/installer/TrafficLens-Setup-0.1.4-win-x64.exe` — 89,953,854 bytes,
  SHA-256 `A982E268E13AB03DD36BBEA335947CC886FF50170E255391D1D935F00E52D70E`.
- `artifacts/portable/TrafficLens-Portable-0.1.4-win-x64.zip` — 129,618,364 bytes,
  SHA-256 `03A41E32F5A76837C8E58A1AC6D1EC557E4471A9D0F979669C511AC2BFA61D24`.
- Both hashes were recomputed from the final files and match their `.sha256`
  sidecars exactly.
- `TrafficLens.WinUI.pri` 2,231,888 bytes, `mrm_pri2`, SHA-256
  `5F286940DDC6AE04C8904DB78597A0664164B344CB8AF9308728EBCDC95756C6`.
- Publish and ZIP both contain 815 files: no PDBs, no test assemblies, no `.cs`/`.xaml`
  sources, no `obj`/`bin` leftovers, and no superseded WPF `TrafficLens.exe` payload.
  All 815 ZIP entries use forward slashes. The `fa-IR` satellite, the canonical icon
  (`9F8DD468D671D648367E345109118EEF6C3B2E78A6F4B07054D292B12F8C60BE`) and the
  `Assets\TrafficLens-256.png` logo are byte-identical to their repository sources.
- Installer configuration unchanged: AppId
  `{8F0E8A8F-7B1D-4A5E-9C2D-3E5F6A7B8C9D}`, entry point `TrafficLens.WinUI.exe`, and
  `VersionInfoProductVersion`/`VersionInfoVersion` both `0.1.4`. The line that deletes
  `{app}\TrafficLens.exe` is the intended upgrade-time removal of the superseded WPF
  build, not a shipped payload.

**Runtime smoke test, run against the extracted final portable ZIP** (not a
`bin\Release` build), `fa-IR`, one launch: the window appeared, culture resolved to
`fa-IR` (`PageTitleText` = `داشبورد`), the NavigationView rendered all seven items,
and the shell quick action measured 29 physical px relLeft / 13 relTop at 150% DPI.
The title-to-caption gap measured **+35** px, clear of the three native controls. The
Floating Widget opened as a 510x210 window at the persisted (24, 281) and its values
were live — total 11.14 KB/s on the first sample, 2.82 KB/s ten seconds later — then
it disabled cleanly, its window closing and the toggle returning to `Off`. Three
pages were navigated without failure and the process was still alive at the end. The
run's own log block is clean: shell start, `MainWindow activated` (so the PRI loaded
and `InitializeComponent` succeeded), all services started, SQLite schema v2 ready; the
whole day logs at 95 Information / 5 Warning / 0 Error, the warnings being the
documented non-elevated ETW per-process collector. `settings.json` was byte-identical
to its pre-test backup afterwards.

**Installer runtime test was not performed.** An earlier TrafficLens installation is
present at `%LOCALAPPDATA%\Programs\TrafficLens` and already holds this release's
AppId uninstall registration, so a silent install of the final installer would
overwrite that directory and rewrite the same registration rather than test in
isolation. The installer was verified statically and by compilation instead.

**Unsigned.** Both the installer and the published `TrafficLens.WinUI.exe` report
`NotSigned`; no code-signing certificate is available. The published checksums
describe this unsigned build and must be recomputed after any signing. Not merged,
tagged or published — no git remote is configured.

## [0.1.4] — Final human UI polish (superseded draft; human verification since passed — see the topmost 0.1.4 section)

A polish pass over the approved baseline `99397a4` addressing six defects found by
human review. Scoped strictly to those six items: no backend, collector, monitoring
or packaging changes, and no new timer, poller or background worker.

### Fixed

- **Right-to-left title bar still collided with the caption buttons.** The first
  implementation read `LeftInset` and `RightInset` as physical sides. Launching the
  Persian build showed they are reported in *flow* order: on a 150% DPI Persian
  window the shell reported the non-zero inset as `LeftInset` (207 px) with
  `RightInset` 0, while the caption buttons sat on the physical right
  (x 796..1003). The safe area therefore landed on the wrong edge and the
  right-aligned title ran under Minimize/Maximize/Close. `ResolvePadding` now takes
  the insets in flow order plus the layout direction and returns the padding for the
  physical left and right, and the call site passes
  `RootGrid.FlowDirection == FlowDirection.RightToLeft`. The unit test that had
  encoded the wrong assumption was replaced with the measured 207/0 at 1.5 scale
  case and a regression guard that the safe area is on the physical right.

- **Window title collided with the caption buttons.** The custom title bar grid had
  no awareness of the caption-button area. `TitleBarCaptionLayout` now resolves the
  padding from the live `AppWindow.TitleBar.LeftInset`/`RightInset` reported by the
  shell, converted from physical pixels to DIPs using the window DPI, and reapplied
  on presenter, size and position changes. Because it follows caption geometry and
  not the title string, English and Persian right-to-left both work in normal and
  maximized states with no language-specific margin. The insets arrive in flow
  order, not as physical left and right, so the layout direction is passed to the
  helper and decides which physical side each inset belongs to; see the follow-up
  below.
- **Closing the widget did not disable it.** The widget window cancelled its own
  close and hid itself directly, bypassing the service, so the enabled flag was
  never persisted and the widget came back on the next start. The native close
  button now raises `UserCloseRequested`, which the service turns into the same
  `SetEnabled(false)` path the Settings switches use. Closing the widget no longer
  affects the main window, tray or monitoring.
- **No quick way to toggle the widget.** Settings had the widget switch only inside
  the full widget section. A quick control now sits at the top of the page. Both
  switches are views of one state owner (`WidgetEnabledState`), so they cannot drift
  apart; the write is idempotent, which is what prevents the close path from
  looping back through itself.
- **Alert rules were one long vertical form.** The five rules are now uniform cards
  in a wrapping grid, so they are equal width and height, evenly spaced and reflow
  to fewer or more columns instead of scrolling horizontally. Cooldown is kept in
  its own separate section. Rule identity, order, thresholds, units, validation and
  the save path are unchanged.
- **Collapsed navigation clipped its labels.** All seven items now have built-in
  `Segoe Fluent Icons` glyphs, the pane collapses to a native icon-only width, and
  the tooltip and accessibility name are set from the same localized label as the
  expanded text. No external icon library is referenced.
- **The widget could be dragged off-screen.** Pointer movement now clamps the window
  to the work area of the monitor it is on, using the real DPI-scaled
  `AppWindow.Size`, and recovers a saved position from a disconnected monitor into
  the nearest remaining work area instead of an arbitrary one. The pointer delta is
  scaled before being applied, so DIPs are never mixed with physical pixels.

### Verification

- Focused tests: 100 passed, 0 failed (`TrafficLens.WinUI.Tests`).
- Full solution suite: 666 passed, 0 failed.
- Release x64 build: 0 warnings, 0 errors.
- Minimum publish check: 815 files, no PDBs, no test assemblies,
  `TrafficLens.WinUI.pri` present and valid, canonical icon unchanged
  (`9F8DD468D671D648367E345109118EEF6C3B2E78A6F4B07054D292B12F8C60BE`),
  `fa-IR` satellite present.

**Superseded draft of v0.1.4.** Human verification has since passed and the final
artifacts are recorded in the topmost `0.1.4` section. Nothing should be built from
this intermediate state.

## [0.1.4] - Alert threshold editing and Floating Widget quick control

Two human-found functional defects, addressed strictly on their own. No alert
backend, collector, monitoring or packaging changes, and no new timer, poller or
background worker.

### Fixed

- **Alert rule thresholds were invisible and unusable.** Every rule card rendered
  its numeric threshold collapsed to roughly six physical pixels, and the editor was
  disabled whenever the rule itself was off. Because all five rules ship disabled by
  default, none of the thresholds could be seen or edited. `ItemsWrapGrid` now uses
  an item height of 120 with auto-sized rows above a trailing spacer, so the numeric
  field and unit selector keep their real height, and the editor is no longer gated
  on the rule's enabled state. The enable switch and the threshold editor are now
  independent controls. Bindings are unchanged: `ThresholdText` and `UnitIndex`
  remain two-way, the field keeps left-to-right digits with right alignment, units
  stay `KB/s, MB/s, GB/s` for the two speed rules and `MB, GB, TB` for the three
  limit rules, and the existing parse, validation, range and save mapping are
  untouched, so the alert backend still reads the same persisted values.

- **The Floating Widget could only be reached indirectly.** It is now a quick
  show/hide control in the persistent shell: a compact native `ToggleButton` with a
  Segoe Fluent Icons glyph and the localized label, in the top-left corner of the
  title bar, so it is available on every page instead of being buried in Settings.
  The title bar grid is pinned to left-to-right so the control stays physically
  top-left in both layout directions, which in Persian is the side opposite the
  navigation pane, while the title keeps its own direction and alignment so the
  right-to-left title still sits against the navigation edge and clear of the
  caption buttons.

  The control is a view onto the existing `IFloatingWidgetService`, not a second
  state owner: a click calls `SetEnabled`, which persists the existing setting and
  shows or hides the window, and the service's `EnabledChanged` and
  `IsVisibleChanged` events push the result back into the control. The Settings
  switches and the widget's own close button already go through the same service, so
  the shell control, Settings and the widget cannot disagree, and the persisted
  value is what the control shows after a restart. There is no second flag, no
  second service and no polling. Always On Top deliberately stays a detailed option
  in Settings and is not duplicated in the shell.

### Changed

- `AlwaysOnTopLabel` now reads "Always On Top" in English and
  "همیشه روی سایر پنجره‌ها" in Persian, and the widget quick-control label is
  "ویجت شناور" in Persian. The shared resource is also used by the WPF surface, so
  its pinned test expectation was updated to the new casing.

### Verification

- Focused tests: 144 passed, 0 failed (`TrafficLens.WinUI.Tests`).
- Full solution suite: 710 passed, 0 failed.
- Release x64 build: 0 warnings, 0 errors.

**Superseded draft of v0.1.4.** Human verification has since passed and the final
artifacts are recorded in the topmost `0.1.4` section. Nothing should be built from
this intermediate state.

## [0.1.4] — WinUI 3 Release Candidate

Release candidate built on `feature/winui3-migration` with
`scripts/build-release.ps1`. The WPF project is retained as rollback
(`f9017f0`) but is no longer the release entry point. This build is **unsigned**
(no code-signing certificate is available). Not merged, tagged or published.

### Fixed (release-critical: the first v0.1.4 RC could not start)

- **`TrafficLens.WinUI.pri` was missing from the published output, so the app
  crashed at startup.** MRT Core (`Microsoft.Windows.SDK.BuildTools.MSIX.MrtCore.PriGen.targets`)
  writes the project PRI straight to `$(TargetDir)` and only registers it as a
  publishable item when `AppxPackage == true`, i.e. for MSIX. This app is
  unpackaged (`WindowsPackageType=None`), so `dotnet publish -o` silently omitted
  it. Because the compiled XAML (XBF) is packaged *only* inside that PRI — the
  managed assembly embeds zero XBF resources — the release started, set its
  culture to `fa-IR`, and then died with
  `Microsoft.UI.Xaml.Markup.XamlParseException: XAML parsing failed` in
  `MainWindow.InitializeComponent()` (Windows Application Error 1000, faulting
  module `Microsoft.UI.Xaml.dll`, exception `0xc000027b`). It built, tested,
  published, packaged, installed and checksummed cleanly, so no earlier gate
  caught it. The first v0.1.4 RC (installer `3E9DEA0E…`, ZIP `660E1D4C…`) is
  **invalid and must not be distributed**.
- **Fixed with an explicit publish hook.** `IncludeProjectPriFileInPublish` adds
  the MRT-generated PRI to `ResolvedFileToPublish` after
  `ComputeResolvedFilesToPublishList`. `Build` (and therefore `PrepareForRun` →
  `_GenerateProjectPriFile`) runs before the publish file list is computed, so
  the file is always present. This uses the supported MSBuild publish mechanism
  rather than copying arbitrary `bin` contents.
- **Regression guard added to the release pipeline.** Publish validation now
  fails the run **before** the ZIP and installer are created when the PRI is
  missing, empty, implausibly small (<256 KB), or not a valid PRI container. MRT
  Core emits the `mrm_pri2` container, so the header is validated as `mrm_`
  (accepting legacy `PRIC` too) rather than assuming a magic that this
  toolchain never produces. The guard was verified to fail on the broken tree
  and to pass on the fixed one.

### Clarified (previously reported incorrectly)

- **Smart App Control did not block this release.** The single launch of the
  broken RC started normally and wrote **no Code Integrity event**; the earlier
  `0x800711C7` attribution for this artifact was wrong. The crash was entirely
  the incomplete publish output. SAC is still ON and was not modified.

### Verified after the fix (one launch, PID 12484)

- `WinUI MainWindow activated`; culture `fa-IR`; 7 nav items with داشبورد
  selected; live dashboard figures (دانلود 8.55 KB/s, آپلود 666 B/s,
  مجموع 9.2 KB/s); network, connection and history services started; SQLite
  schema v2 ready. Floating Widget visible at 340x140 with Always On Top.
- New publish tree: 815 files, includes `TrafficLens.WinUI.pri`
  (2,230,712 bytes, `mrm_pri2`, SHA-256 `64DE369D…`), no PDBs, no test
  assemblies, no WPF executable, `fa-IR` satellite and branding intact.
- New RC supersedes the broken one: installer `E03C4B00…` (85.78 MB), ZIP
  `0539049C…` (123.61 MB).
- Icon, widget and Persian **human** visual verification is still PENDING.

### Fixed (signing-pipeline audit)

- **The sign set no longer misses the Persian satellite assembly.** The opt-in
  `-Sign` path used a hard-coded list of six binaries that omitted the shipped
  `fa-IR/TrafficLens.WinUI.resources.dll`. Because a *partially* signed
  application is treated as untrusted, a signed release would have been
  unlaunchable even though every individual signing call succeeded. The set is
  now discovered from the publish output and covers all seven TrafficLens-owned
  PE images.
- **The whole sign set is re-verified after signing.** Each file must report
  `Valid` *and* carry an RFC 3161 timestamp countersignature, so a partial
  signature fails the release instead of shipping.
- **ECC certificates are rejected up front.** Smart App Control's signature
  check accepts RSA certificates only and does not support ECC, so an ECC
  certificate would have produced a correctly signed build that still could not
  launch. Expired certificates are rejected too.
- **Certificate store handling corrected.** A certificate in
  `LocalMachine\My` is now found and signed with `/sm`; previously the pipeline
  searched the machine store but then asked signtool to look only in the
  current user's store.
- **Signing no longer attempts to sign non-executable files.**
  `Get-ChildItem -LiteralPath -Recurse -Include` silently ignores `-Include`, so
  the extension test is now explicit; otherwise `.ico`, `.png`, `.deps.json` and
  `.runtimeconfig.json` would have been selected.
- Microsoft/.NET/Windows App SDK binaries are still never re-signed, and signing
  remains opt-in with no key material in the repository.

### Changed

- **Release entry point switched to WinUI 3** — the pipeline publishes
  `src/TrafficLens.WinUI` (unpackaged `TrafficLens.WinUI.exe`, Windows App SDK
  2.5.1). The release fails if a WPF `TrafficLens.exe` appears in the output.
- **Self-contained loose layout, no single-file** — `win-x64` self-contained
  publish that ships the Windows App SDK runtime payload next to the executable,
  so the app runs on a machine without the Windows App Runtime installed.
  `PublishSingleFile=false`, trimming and ReadyToRun off, no PDBs. Still an
  unpackaged EXE + Inno Setup installer: **no MSIX**.
- **Installer targets the WinUI executable** — shortcuts, post-install launch,
  the WMI process check and uninstall checks all use `{#AppExeName}`
  (`TrafficLens.WinUI.exe`); the stable `AppId`
  `{8F0E8A8F-7B1D-4A5E-9C2D-3E5F6A7B8C9D}` is unchanged. `[InstallDelete]`
  removes a stale WPF `TrafficLens.exe` on upgrade.
- **Signing-ready, unsigned by default** — optional `-Sign` performs SHA-256 +
  RFC 3161 timestamp signing in the required order (publish → product binaries →
  ZIP/installer → installer → SHA-256). The thumbprint is read from the Windows
  certificate store at run time; no key material is stored in the repository.
- **Version 0.1.4** in `Directory.Build.props`, the Inno script and both artifact
  names. `ProductVersion`/`InformationalVersion` = `0.1.4`,
  `AssemblyVersion`/`FileVersion` = `0.1.4.0`; About resolves the version from
  `AssemblyInformationalVersion`.
- **Release pipeline fixes** — `-p:Platform=x64` is now required for build,
  test and publish (the Windows App SDK self-contained targets refuse to
  evaluate under AnyCPU, and x64-only keeps the native payload x64-only); the
  portable ZIP is written with spec-compliant forward-slash entry names instead
  of `Compress-Archive` backslashes; `-ResumeFromPublish` re-packages an already
  validated publish tree without repeating the build and test run; the
  executable name is derived from the WinUI project's `<AssemblyName>` instead
  of being hard-coded.
- **Lifecycle harness** (`scripts/tl023-release-lifecycle.ps1`) follows the new
  executable name and version.

### Added

- **WinUI 3 shell (WUI-001)** — unpackaged `TrafficLens.WinUI` (Windows App SDK 2.5.1), custom title bar, NavigationView, 7 destinations, EN/FA localization + RTL, floating widget, system tray (WinForms host project), single-instance guard, settings language selector.
- **WinUI Dashboard (WUI-002)** — live Download/Upload/Total cards, Today at a Glance, Top App Now, Active Adapter, and a lightweight Canvas live graph (30s / 1m / 5m ranges) reusing shared `INetworkTrafficCollector`, aggregator, formatters, `ITrafficHistoryService`, and `IProcessTrafficCollector` — no duplicate collectors, ETW sessions, or polling timers.
- Process traffic collector and traffic history service startup wired once in WinUI `App` (mirrors WPF composition root).
- Release validation: publish output is checked for the executable, x64 PE
  machine type, version metadata, Windows App SDK runtime payload, branding
  assets, embedded English resources + `fa-IR` satellite, and the absence of
  PDBs, test assemblies and the WPF executable.

### Fixed

- **WUI-009 audit fixes** - Floating Widget title bar: Pin moved to the left, native Minimize/Close caption group preserved, Maximize disabled (`WS_MAXIMIZE`/`WS_MAXIMIZEBOX` cleared) and a minimized widget restored on show; size (`340x140`), Pin/AOT behavior, live Download/Upload/Total, widget backend and page lifecycle unchanged, no new timer/poller/collector. Second launch now restores a hidden main window (UI-thread `DispatcherQueue` captured at launch). Log, icon-log and History CSV file names/dates are culture-invariant (`fa-IR` no longer yields `1405-*`). Added `fa-IR` regression tests for logger file names and History CSV output.
- **WUI-009 verification state** - the extended audit was intentionally stopped before every planned long-duration phase completed. Completed evidence is preserved in `%TEMP%\opencode\wui009-*`. Final automated run: **571/571 tests PASS**, Release x64 build **0 warnings / 0 errors**. Long performance and soak phases: **NOT TESTED**. Human GUI verification of the widget title bar: **PENDING**.
- **Window and widget icons** - a per-size HICON cache (`WindowIcon`) is shared
  by the main window, the floating widget and the tray host, and `WM_SETICON` is
  re-applied to the correct HWNDs. A widget window created after startup is
  covered by a one-shot deferred `DispatcherQueue` reapply, and the post-show
  reapply runs once per show. Persian widget layout keeps all rows visible at
  `340x140` DIP and the title bar / pin behavior is unchanged. Human
  verification of the final icon state: **PENDING**.

### Verification (v0.1.4)

- Release pipeline: **571/571 tests PASS** (270 App / 212 Network / 84
  Infrastructure / 5 WinUI), Release x64 build **0 warnings / 0 errors**.
- Installer `TrafficLens-Setup-0.1.4-win-x64.exe` (85.50 MB,
  `ProductVersion 0.1.4`) and portable `TrafficLens-Portable-0.1.4-win-x64.zip`
  (123.09 MB, 814 entries), each with a `.sha256` sidecar. Installer SHA-256
  `3E9DEA0EB6594DE7055C6FAF73746467AA40D9256C672FF9685A77FD7269146A`, ZIP
  SHA-256 `660E1D4C9F502F179CB249E7A36AAF9F4A0943583E37C4A66E8A7973E96AF7FE`.
- Publish output (814 files / 323.5 MB): `TrafficLens.WinUI.exe` 0.28 MB valid
  x64 PE, `ProductVersion 0.1.4` / `FileVersion 0.1.4.0`, Windows App SDK
  runtime payload present, no PDBs, no test assemblies, no WPF executable,
  branding assets and both localizations present.
- **Unsigned:** the installer reports `NotSigned`; the published checksums
  describe this unsigned build and must be recomputed after any signing.
- **Runtime verification not possible on the release host:** Smart App Control
  is ON (policy `{0283ac0f-fff1-49ae-ada1-8a933130cad6}`,
  `VerifiedAndReputablePolicyState=1`, no enterprise-authored policy) and
  blocks fresh unsigned binaries with `0x800711C7`. No security setting was
  changed. Install/launch/reputation validation is **PENDING** a signed or
  otherwise trusted build; v0.1.4 was verified statically (archive structure,
  entry names, extraction, version metadata, signature status, checksums,
  payload contents).
- The v0.1.3 artifacts were left unchanged (installer SHA-256 `55EBCCD8…`).

---

## [0.1.3] — Dark Theme & Localization Hotfix

Post-v0.1.2 UI polish and runtime language switching fixes.

### Fixed

- **ComboBox navigation crash** — ComboBox template `ColumnDefinition Width` used `{DynamicResource {x:Static SystemParameters.VerticalScrollBarWidthKey}}` which returns a boxed `double` at runtime. WPF cannot implicitly convert `double` → `GridLength` during template application, causing `InvalidCastException` and process termination. Fixed by using a hardcoded literal `Width="17"`.
- **Disabled ComboBox Background** — ComboBox template disabled-state trigger used `{DynamicResource SurfaceColor}` (a `Color` object) where `Background` expects a `Brush`. Fixed to `{DynamicResource SurfaceBrush}`.
- **White main content background** — removed bright white background behind all page content by applying `BackgroundBrush` to MainWindow root Grid.
- **Dark ComboBox dropdown** — full ComboBox ControlTemplate with dark Popup background (`DynamicResource`), removed `SystemDropShadowChrome`. Eliminates default white dropdown surface.
- **Dark WPF ContextMenu/MenuItem** — implicit styles for ContextMenu and MenuItem with dark backgrounds, hover highlights, and full ControlTemplate.
- **Runtime English/Persian switching** — fixed incomplete UI refresh: AlertRuleViewModel.Name now raises PropertyChanged, ConnectionsViewModel backing field bypass fixed, FloatingWidgetWindow FlowDirection updates on culture change.
- **Floating Widget lifecycle** — Suspend/Resume instead of Dispose/Recreate prevents disposed ViewModel and duplicate instances.
- **About page icon** — switched from ICO to TrafficLens-256.png for sharper 48x48 rendering.
- **CSV dialog localization** — SaveFileDialog filter now uses localized resource string.
- **CultureChanged cleanup** — MainViewModel.Dispose unsubscribes from CultureChanged.
- **Alerts page configured rules display** — Alerts page now shows configured alert rules (enabled rules with thresholds) above the triggered alerts section. Rules update immediately after saving on the Settings page without restart.
- **Alerts page empty state** — Shows "No alerts have been configured yet." when no rules are enabled; "No alerts have been triggered yet." when no alerts have fired. Empty states update dynamically.

### Added

- `IAlertService.CurrentConfig` property exposing the current `AlertConfig`.
- `IAlertService.ConfigChanged` event fired when alert configuration is refreshed.
- `ConfiguredAlertRuleViewModel` displaying each enabled alert rule with name and threshold.
- 7 new localization strings for configured rules section and empty states (English + Persian).
- 4 new regression tests for Alerts configured rules display, culture switching, and empty state.
- 13 new DarkTheme structural tests (brush resources, ComboBox/ComboBoxItem/ContextMenu/MenuItem style existence, no SystemColors references).
- 4 new localization regression tests (ConnectionsViewModel label refresh, AlertRuleViewModel name refresh and PropertyChanged).

---

## [Post-0.1.2] — UI/UX Hotfix

Post-v0.1.2 UI polish fixes. These are NOT part of the tagged v0.1.2 release.

### Fixed

- **Dark theme ComboBox dropdown** — added implicit ComboBox/ComboBoxItem styles to `DarkTheme.xaml`. Dropdown popup, hover, selected, and disabled states now use dark theme colors instead of WPF default white.
- **Floating widget toggle** — fixed lifecycle bug where toggling widget off/on could leave a disposed ViewModel bound to the window. Now uses Suspend/Resume instead of Dispose/Recreate. Widget window is reused across toggle cycles. No duplicate instances, no application exit, no lost events.
- **About page icon** — switched from `TrafficLens.ico` to `TrafficLens-256.png` for sharper rendering at 48x48 display size. Added `RenderOptions.BitmapScalingMode="HighQuality"`.
- **Global dark theme controls** — added implicit styles for TextBox, CheckBox, Button to ensure consistent dark theme across all controls.

### Added

- 12 new widget lifecycle regression tests covering: Suspend stops events, Resume restarts events, repeated Suspend/Resume cycles, culture refresh on Resume, FakeWidgetService toggle cycles, multiple toggles, hide-does-not-exit, dispose-on-exit, persisted setting sync.

---

## [0.1.2] — Release Candidate (TL-025)

Release candidate 0.1.2 — first controlled release from the verified TL-024 baseline.

### What's New Since v0.1.1

- **Performance optimization** — reduced CPU and memory overhead for continuous network monitoring (TL-017)
- **Current-Day hourly History** — granular per-hour traffic history for today (TL-018)
- **First-run onboarding** — guided setup for new users with contextual tips (TL-019)
- **Connections readability** — improved process name display, bounded reverse DNS lookups (TL-020)
- **Dashboard insights** — Today at a Glance + Top App Now panels (TL-021)
- **This Month History** — monthly traffic summary with Today vs Yesterday comparison (TL-022)
- **CSV export** — export history data to CSV for external analysis (TL-022)
- **Release lifecycle hardening** — 37-point deterministic validation covering install, launch, exit, startup, uninstall, reinstall, portable, and metadata (TL-023)
- **Bilingual installer** — English and Persian (Farsi) language support with RTL layout (TL-024)
- **Packaging metadata fixes** — ProductVersion bug fixed, placeholder URLs removed (TL-023, TL-024)

### Known Limitations

- **Unsigned build** — no code signing certificate; enterprise WDAC policies may block installed copies (confirmed: WDAC blocked uninstaller/reinstall lifecycle tests)
- **Some uncommon Inno Setup messages fall back to English** in the Persian installer
- **Upgrade lifecycle NOT TESTED** — authentic previous v0.1.1 installer artifact unavailable
- **RC installer SHA256**: `9FABE44055AA9EE91E537ECFB365B2B5CECAED0D86BB1D3AE2EF230223E0079A`

---

## [Unreleased] — TL-024 (Installer Localization & Packaging Polish)

### Added (TL-024)

- **Bilingual installer** — English and Persian (Farsi) language support in the Inno Setup installer. Language selection dialog at startup. Persian RTL layout enabled via `RightToLeft=yes` in custom `Persian.isl` language file.

- **Custom translated installer messages** — User-facing installer strings (welcome, destination, install, completion, etc.) translated to Persian. Custom `AppRunningWarning` message translated per language via Inno Setup `[CustomMessages]` section.

- **Lifecycle validation extensions** — 5 new assertions (9.5–9.9): AppPublisherURL absent, AppSupportURL absent, stable AppId unchanged, English language present, Persian language present.

### Fixed (TL-024)

- **Placeholder metadata removed** — `AppPublisherURL` and `AppSupportURL` placeholder entries (`https://github.com/`) removed from `packaging/TrafficLens.iss`. No valid official URL exists; placeholders are worse than absent.

### Known Limitations (TL-024)

- **Persian installer fallback messages** — Some uncommon Inno Setup messages (e.g., `ArchiveIncorrectPassword`, `ErrorRegCreateKey`) fall back to English when not defined in `Persian.isl`. These are edge-case error messages rarely seen by users. All primary wizard-flow messages are translated.
- **Unsigned build**: No code signing certificate.
- **Upgrade test**: Authentic previous v0.1.1 installer artifact unavailable; upgrade lifecycle remains NOT TESTED.

---

### Added (TL-023)

- **Release lifecycle validation script** (`scripts/tl023-release-lifecycle.ps1`) — deterministic 32-point validation covering: SHA256 checksum verification, clean install, first launch, settings/DB initialization, graceful exit, no orphan processes/ETW sessions, startup registration behavior (default-off, write/read, no-duplicate-on-reinstall), uninstall with user data preservation, reinstall with data loading, portable build validation, installer metadata verification (ProductName, ProductVersion, FileVersion, version consistency assertion).

- **Full release lifecycle verified**: install → launch → exit → startup registration → uninstall (data preserved) → reinstall (data loads) → portable build → checksum — all 32 checks pass.

### Fixed (TL-023)

- **Installer ProductVersion metadata** — `VersionInfoProductVersion` in `packaging/TrafficLens.iss` was set to `AppVersionShort` (which strips `0.1.1` → `0.1`). Changed to use `AppVersion` (`0.1.1`) so installer PE metadata consistently represents the full version. Added lifecycle test assertion (9.4) to verify ProductVersion matches FileVersion.

### Known Limitations (TL-023)

- **Installer placeholder URLs**: `AppPublisherURL` and `AppSupportURL` in `packaging/TrafficLens.iss` are `https://github.com/` (no specific repo). Marketing/final branding will provide real URLs. Not a release blocker.
- **Installer English-only**: The Inno Setup installer UI is English-only. Persian installer language file is a separate task (TL-024).
- **Unsigned build**: No code signing certificate. The installer and executable are unsigned. Real code signing is a future release-checklist item.
- **WDAC enterprise environments**: The installed copy (from Inno Setup) may be blocked by enterprise Application Control policies on unsigned executables. The published build from the artifacts directory works normally. This is an environment constraint, not a product defect.
- **Clean-VM not tested**: No disposable clean Windows VM available. Lifecycle validation approximates clean-machine behavior using isolated temp directories.

### Constraints (TL-023)

- Zero new product polling loops
- Zero new collectors
- Zero new ETW sessions
- Zero DB/schema changes
- Zero monitoring/accounting changes
- No TL-017 optimizations modified

---

Branch `feature/tl022-history-insights` off master; implementation complete.

### Added (TL-022)

- **This Month history range** — New `HistoryRange.ThisMonth` using local calendar month semantics (first day of current month through current local time). NOT rolling 30 days. Half-open `[monthStart, today+1)` date range. Daily chart series filtered to current month.

- **Today vs Yesterday comparison** — Compares Today's usage so far against Yesterday's usage scaled to the equivalent elapsed local-day interval via `DayFraction`. Shows Today total, Yesterday-at-this-time equivalent, absolute difference, and percentage. Safe zero-denominator handling. Hidden when no comparison data available.

- **CSV export** — Export CSV button on History page. Uses `SaveFileDialog`. UTF-8 with BOM, header row (`Period,DownloadBytes,UploadBytes,TotalBytes`), invariant numeric values, deterministic column order. Hourly data for Today range, daily data for all other ranges. IO errors surfaced as localized non-crashing message.

- **Localization** — 9 new keys in both `Strings.resx` and `Strings.fa-IR.resx` (ThisMonthLabel, TodayVsYesterdayLabel, YesterdayAtThisTimeLabel, DifferenceLabel, ExportCsvLabel, ExportSuccessfulLabel, ExportFailedLabel, NoComparisonDataLabel).

- **Tests (+14 → 509/509)** — `HistoryRangeCalculatorTests` (+4: ThisMonth range semantics), `HistoryViewModelTests` (+10: ThisMonth selection, comparison positive/negative/equal/zero-yesterday/hidden/day-fraction-scaling, localization).

### Constraints (TL-022)

- No new timers/polling/ETW sessions
- No new DB queries or schema changes
- No monitoring/accounting changes
- Reuses existing `TrafficHistoryService` snapshot pipeline

---

## [Unreleased] — TL-021 (Dashboard Insights: Today + Top App)

Branch `feature/tl021-dashboard-insights` off master; implementation complete.

### Added (TL-021)

- **Dashboard "Today at a Glance" section** — Three-card grid showing Download/Upload/Total bytes used today. Reads from `ITrafficHistoryService.HistoryChanged` → `HistorySnapshot.Today` (`TrafficUsage`). Formatted via `DataSizeFormatter.Format()`. Handles unavailable history service and snapshots (zero-fills).

- **Dashboard "Top App Now" section** — Card showing the process with highest current throughput (`ProcessSampleSelection.TopConsumer()`). Reads from `IProcessTrafficCollector.SamplesReady` event. Shows app name, total rate, download/upload rates. Idle state shows localized "No active traffic" text. Permission-denied and unavailable states handled with localized warnings.

- **DashboardViewModel extension** — Constructor extended with optional `ITrafficHistoryService?` and `IProcessTrafficCollector?` parameters (backward-compatible, DI-resolved). Event subscriptions in constructor, unsubscribed in `Dispose()`. All updates gated by `_isActive`.

- **Localization** — 12 new keys in both `Strings.resx` and `Strings.fa-IR.resx` (TodayAtGlanceLabel, DownloadTodayLabel, UploadTodayLabel, TotalTodayLabel, TopAppNowLabel, TopAppApplicationLabel, TopAppCurrentLabel, TopAppNoDataLabel, TopAppPermissionDeniedLabel, TopAppUnavailableLabel, TopAppDownloadLabel, TopAppUploadLabel).

- **Tests (+22 → 495/495)** — `DashboardInsightTests`: 7 Today tests, 11 Top App tests, 4 Lifecycle tests. All use fakes, no live DNS or DB.

### Constraints (TL-021)

- No new timers/polling/ETW sessions/history polling
- No new DB queries or schema changes
- No per-process metadata queries in hot path
- All data from existing event pipelines (`HistoryChanged`, `SamplesReady`, `StatusChanged`)

---

## [Unreleased] — TL-020 (Connections Readability & Usability)

Branch `feature/tl020-connections-readability` off master; implementation complete.

### Added (TL-020)

- **Hide Listeners filter** (`ConnectionFilter.HideListeners`). Hides TCP listeners (`State == Listen`) and unconnected UDP entries (no remote endpoint) from the Connections list. Pure function, no provider changes. Toggle via new toolbar checkbox in Connections view; persisted via `ISettingsService` (`ConnectionsHideListeners` key, default `false`).

- **Copy actions** (local endpoint, remote endpoint, remote IP, process name). Right-click context menu on connection rows with keyboard-accessible copy commands. Localized (en-US + fa-IR).

- **Optional reverse DNS resolution** (`EnableReverseDns` setting). OFF by default, user-enabled, persisted via `ISettingsService` (`ConnectionsEnableReverseDns` key). Bounded DNS cache (TTL 30 min success / 5 min failure, max 1024 entries, 4 concurrent lookups, failure caching, cancellation-safe). Resolves remote IP → hostname for display as secondary info; raw IP always visible. Setting in Settings page under General section. `DnsResolverService` with bounded cache, concurrency limiting, failure caching, cancellation safety.

- **Localization (en-US + fa-IR).** Exact resource parity (new keys for HideListeners, Copy actions, EnableReverseDns); RTL layout correct; runtime language switch updates UI.

- **Accessibility.** Keyboard navigation, focus order, Escape dismiss, `AutomationProperties.Name` on context menu items and checkboxes.

- **Tests (+3 → 191 App tests).** `Filter_HideListeners_HidesTcpListenersAndUnconnectedUdp`, `HideListeners_Setting_PersistsAndRestores`, `EnableReverseDns_DefaultFalse_PersistsAndRestores`.

### Changed (TL-020)

- `ConnectionFilter` + `ConnectionFiltering.Matches`: added `HideListeners` filter (excludes TCP listeners + unconnected UDP).
- `ConnectionsViewModel`: added `HideListeners` + `EnableReverseDns` properties (persisted via `ISettingsService`), `RefreshDnsForVisibleRows` + `ClearResolvedHostnames` for DNS integration.
- `ConnectionRowViewModel`: added `ResolvedHostname` property + `SetResolvedHostname` method + copy commands (`CopyLocalEndpoint`, `CopyRemoteEndpoint`, `CopyRemoteIp`, `CopyProcessName`).
- `ConnectionsView.xaml`: added context menu with copy actions, HideListeners checkbox, EnableReverseDns checkbox.
- `SettingsViewModel` + `SettingsView.xaml`: added `EnableReverseDns` checkbox in General section (persisted via `ISettingsService`).
- `DnsResolverService` (new): bounded DNS cache with TTL, concurrency limiting, failure caching, cancellation safety.
- `SettingsViewModel.RefreshFromSettings`: loads `EnableReverseDns` from settings.

### Scope (TL-020)

- No new timers/polls/workers, no duplicate event subscriptions (reuses existing `AdaptersChanged`).
- No monitoring/accounting behavior changes, no include-tunnels setting.
- No auto-elevation, no ETW session changes, no fake process data.
- Zero continuous background cost when disabled; reuses existing adapter events.

### Verified (2026-09-20, branch source)

- `dotnet build TrafficLens.sln` Debug + Release: **0 warnings / 0 errors**.
- **458/458 tests pass** (App 191 / Network 212 / Infrastructure 55).
- Published single-file build GUI verified: fresh profile onboarding, HideListeners toggle, EnableReverseDns toggle, copy actions, en-US/fa-IR/RTL, runtime language switch, all pages functional, graceful exit, no orphan process.

## [Unreleased] — TL-019 (first-run Get Started + contextual clarity)
 
Branch `feature/tl019-get-started` off master; implementation complete.
 
### Added (TL-019)
 
- **First-run Get Started overlay.** A lightweight, dismissible panel that auto-shows
  on a brand-new profile (no prior `settings.json`). Explains TrafficLens in concise
  skimmable sections: What TrafficLens does, Applications/Administrator privilege
  (ETW kernel provider requires elevation, no auto-elevation), VPN & tunnels
  (tunnel adapters shown individually but excluded from system total to avoid
  double-counting), System tray behavior, Floating widget, Alerts, Start with
  Windows, Language switcher. Completion persisted via existing `ISettingsService`
  (`HasCompletedOnboarding` key). Manual reopen from About ("Get Started") and
  Settings ("Get Started") never resets persisted state.
- **Contextual tunnel hint.** Small non-alarming hint near the Dashboard system
  total ("System total excludes tunnel adapters to avoid double-counting.") that
  appears only when an active tunnel adapter (`NetworkAdapterKind.Tunnel` with
  `IsUp=true`) is present. Driven entirely by existing `AdaptersChanged` events —
  no new timers, no polling, no collector, no monitoring/accounting changes.
- **Localization (en-US + fa-IR).** Exact resource parity (18 new keys added to both
  `Strings.resx` and `Strings.fa-IR.resx`, 180 total); RTL layout correct; runtime
  language switch updates open onboarding panel.
- **Accessibility.** Keyboard navigation, focus order, Escape dismiss,
  `AutomationProperties.Name` on all interactive elements; panel focuses dismiss
  button on open.
- **Tests (+23 → 455/455).** `OnboardingViewModelTests` (10), Dashboard tunnel
  hint (5), About/Settings/MainViewModel reopen (6), `JsonSettingsService`
  `SettingsFileExisted` (2), resource parity extended.
 
### Changed (TL-019)
 
- `ISettingsService` + `JsonSettingsService`: added `SettingsFileExisted` property
  to distinguish fresh profiles from upgrading ones.
- `DashboardViewModel`: `HasTunnelAdapter` + `TunnelAggregateHint` properties
  driven by existing adapter events.
- `MainWindow.xaml`: onboarding overlay Grid with proper DataContext scoping,
  Escape key binding to `Onboarding.DismissCommand`.
- AboutView/SettingsView: "Get Started" / "راهنمای شروع" manual reopen buttons.
 
### Scope (TL-019)
 
- No new timers/polls/workers, no duplicate event subscriptions.
- No monitoring/accounting behavior changes, no include-tunnels setting.
- No auto-elevation, no ETW session changes, no fake process data.
- Zero continuous background cost; reuses existing adapter events.
 
### Verified (2026-09-20, branch source)
 
- `dotnet build TrafficLens.sln` Debug + Release: **0 warnings / 0 errors**.
- **458/458 tests pass** (App 191 / Network 212 / Infrastructure 55).
- Published single-file build GUI verified: fresh profile auto-show, dismiss
  persistence, restart no auto-show, About/Settings manual reopen, en/fa-IR/RTL,
  runtime language switch while panel open, Dashboard/Applications/History/Alerts/
  Settings/Widget/Tray functional, graceful exit, no orphan process.
 
## [Unreleased] — TL-018 (current-day hourly history, M13)

Branch `feature/tl018-hourly-history` off master; ready to merge on approval.

### Added (TL-018)

- **Today renders an hourly bar series.** `HistoryViewModel` now maps a
  per-hour series sourced from `HistorySnapshot.TodayHourly` when Today is
  selected; the chart title switches between a new localized `HourlyTrafficLabel`
  ("Hourly Traffic" / "ترافیک ساعتی") on Today and the existing
  `HistoryDailyTrafficLabel` otherwise.
- **`HourlyUsagePoint` + `HourlyHistoryBuilder` (Core).** Pure DST-safe UTC-slot
  aggregation: slots are counted from local-midnight-UTC, zero-filled, clamped
  to "now", and labelled by local hour. Spring-forward days yield 23 slots,
  fall-back days 25 (duplicated local label over distinct UTC slots), half-hour
  offset zones (e.g. +05:30) 12 slots.
- **`QuerySamplesAsync` repository query.** One bounded half-open UTC range over
  `traffic_samples` (built-in PK seek, no new index, verified by an
  `EXPLAIN QUERY PLAN` test); the snapshot build issues exactly one such query.
- **Tests (+16 → 432/432).** `HourlyHistoryBuilderTests` (8), repo range/empty/
  query-plan (3), service hourly-snapshot integrity (1), `HistoryViewModelTests`
  rewritten to 10.

### Changed (TL-018)

- `HistoryBarChartControl.Points` now carries `HistoryChartPoint` (label string +
  bytes) so the same native control draws both daily (`MM-dd`) and hourly
  (`HH:00`) labels; alerts fakes/tests updated for the extended snapshot.

### Scope (TL-018)

- Purely additive and read-time: no new timers (the flush-driven
  `HistoryChanged` refresh is reused), no schema change, no migration, no new
  collectors/write-path changes, `lifetime_totals` semantics untouched.

### Verified (2026-09-19, branch source)

- `dotnet build TrafficLens.sln` Debug + Release: **0 warnings / 0 errors**.
- **432/432 tests pass** (App 167 / Network 212 / Infrastructure 53).
- `scripts/tl018-verify.ps1` PASS (en-US + fa-IR chart titles and range
  buttons, resize, real-traffic data chain with exact daily/sample
  reconciliation, graceful exit, no orphans).

## [Unreleased] — TL-017 (performance audit, M12)

Branch `feature/tl017-performance` off master v0.1.1; **merged into `master` at
`21dabb9`.** Verbatim changelog of the audit.

### Changed (TL-017)

- **Connections page optimization (Phase 1).** In-place
  `ConnectionRowViewModel` updates keyed by the stable `ConnectionKey`, delta
  diffing over the native table snapshots, and a per-refresh icon budget — the
  row collection is no longer rebuilt every poll when only counters change.
  Dedicated 3-minute comparable probe: CPU avg **15.31% → 6.65%** (~57% lower),
  CPU max **41.41% → 16.81%** (~59% lower), WS drift **+24.29 → +9.48
  MB/3min** (~61% lower), Private drift **+23.26 → +8.00 MB/3min** (~66%
  lower).
- **Per-tick allocation reduction (Phase 2).** ~550 avoidable allocations per
  poll tick removed (~450 short-lived per-row strings, ~101 `ConnectionKey`
  records created twice per row update). GC capture: Gen0 0.20/s, Gen1/Gen2 0,
  ~1.53 MB/s allocation, ~0.8% time in GC, bounded heap. No CPU improvement
  claimed (within noise).
- **Lazy page instantiation (Phase 3).** The six page Views are created on
  first navigation instead of eagerly in `MainViewModel`/`MainWindow`; the
  internal mainVM + 6-view block dropped ~170 ms → ~70 ms. Wall-clock launch is
  **not** claimed faster (environment-dominated on this host).
- **SQLite history optimization (Phase 4).** Persistent background writer with
  prepared commands and a dedicated flush queue plus `lifetime_totals` schema
  v2: flush avg **7.4 → 1.3 ms**, p95 **9 → 2 ms** (~5.7x), and the Lifetime
  query is now O(1). `docs/DATABASE.md` updated for schema v2 + migration.
- **Idle tray optimization (Phase 5).** `SetActive` gating so hidden
  widget/tray surfaces stop re-rendering, and a widget dispose/recreate path.
  Idle allocation ~2.2 → ~0.5–1.1 MB/s, WS drift +4.7 → +2.4 MB/5min, threads
  16 → 15. Idle CPU avg 1.08% → 1.07% is explicitly within noise (not claimed).
- **Soak harness (Phase 6)** — `scripts/tl017-soak.ps1`. 60-min idle tray:
  CPU avg 0.97%, WS ~240 MB flat (**+0.2 MB** drift after warm-up), Private
  ~120 MB, handles 468–496. 30-min Connections: WS +14.5 first 15min → +2.5
  second 15min, Private +12.7 → +5.0 (deceleration). "No leak was observed
  during the measured soak window" — 60/30-minute soak, not a 24h soak.

### Added

- `docs/PERFORMANCE_AFTER.md` — authoritative TL-017 BEFORE vs AFTER report
  (measurement rules, per-phase results, summary table, correctness regression,
  known limitations, remaining manual validation). Linked from
  `docs/PERFORMANCE.md`.

### Fixed (found during Phase 7 final verification)

- **Schema v1→v2 migration lost the pre-upgrade Lifetime total (production
  data-correctness).** `Migrate()` created `lifetime_totals` but never seeded
  it, so upgrading a real v0.1.1 database (schema v1) reported a Lifetime
  history of 0 until new buckets arrived. The migration now back-fills
  `lifetime_totals` from the existing `daily_usage` sum (`INSERT OR IGNORE …
  SELECT SUM(…) FROM daily_usage`). New regression test
  `InitializeAsync_MigratesV1History_BackFillsLifetimeTotal`; `DATABASE.md`
  describes the migration.
- **`scripts/tl017-db-benchmark.ps1` (harness).** The generated benchmark
  console project was never built before `dotnet run --no-build`, and the same
  source was written to two files (`DbBenchmark.cs` + `Program.cs`), so a clean
  run could not start `DbBenchmark.exe`. Removed the duplicate source write and
  let `dotnet run` build the project; verified end-to-end (flush/query/prune).

### Known limitations (TL-017)

- **Elevated ETW long-run profiling was NOT measured** (non-elevated
  environment; UAC not automatable). Only the `PermissionDenied` fallback,
  stability, no-crash and no-orphan-session paths were verified. Documented as
  remaining manual validation — ETW profiling is not marked complete.
- Wall-clock startup and per-phase idle-CPU deltas are within environment/
  run-to-run noise and are explicitly not claimed.

### Verified (2026-09-19, branch source)

- `dotnet build TrafficLens.sln` Debug + Release: **0 warnings / 0 errors**.
- **416/416 tests pass** (App 163 / Network 212 / Infrastructure 41).
- `tl015-smoke.ps1` PASS, `tl016-verify.ps1` PASS, `tl017-connprobe.ps1` PASS
  (on retry — first attempt was a transient UIA timing flake, diagnosed and
  re-run clean).
- `build-release.ps1` full packaging pipeline succeeds at version 0.1.1
  (no tag created, no version bump).
- All TL-017 benchmark harnesses remain runnable.

## [0.1.1] — 2026-09-17 (first release after TL-016)

### Changed
- **Release version 0.1.0 → 0.1.1** — first released build that ships the
  TL-016 branding/About/Diagnostics work end-to-end. Version stays centralized in
  `Directory.Build.props` (`Version`/`AssemblyVersion`/`FileVersion`/
  `InformationalVersion`); the About page reads it at runtime, and
  `scripts/build-release.ps1` feeds it to the installer (`/DAppVersion`/
  `/DAppVersionShort`; the `.iss` fallback defines match).
- Installer `TrafficLens-Setup-0.1.1-win-x64.exe` built (Inno Setup 6.7.3):
  `SetupIconFile` brand icon + `VersionInfo*` metadata (ProductVersion 0.1.1,
  FileVersion 0.1.1).

### Verified
- `dotnet build TrafficLens.sln` Debug + Release: **0 warnings / 0 errors**.
- 410/410 tests pass (App 158 / Network 212 / Infrastructure 40).
- Published single-file win-x64 EXE metadata: ProductVersion 0.1.1, FileVersion
  0.1.1, FileDescription "TrafficLens Network Monitor".
- Installer SHA-256 `3FB5EE38238D497883755390C4659BFED5E3DDD8F9DD6F477FA776F29E68DE3B`.
- **Upgrade v0.1.0 → v0.1.1 in place:** `settings.json`, `data\trafficlens.db`
  and all logs preserved byte-identical; start-menu shortcut not duplicated;
  exactly one uninstall entry (DisplayVersion 0.1.1); HKCU Run untouched.
- Installed-app smoke (Dashboard, graph, Applications, Connections, History,
  Settings, widget, tray, en/fa switch, single-instance, graceful exit, no ETW
  orphan) PASS.
- About page shows **0.1.1**; brand icon on the EXE matches
  `assets/branding/TrafficLens.ico` at the 32 px frame (100% pixel match).
- `tl016-verify.ps1` PASS on the published 0.1.1 build (en-US labels, fa-IR
  content scan, History page, About page en + fa-IR, no English/Persian leaks,
  graceful exit, no orphan process/ETW).

## [Unreleased] — TL-016 (full Persian localization / M11, + product polish / M11a)

### Changed
- **No hard-coded user-facing strings remain.** The dashboard graph "now" label is
  now data-bound via `TrafficGraphControl.NowLabel` (a `DependencyProperty`,
  default `"now"`) to the localized `Dashboard.GraphNowLabel`, so it renders
  «اکنون» under fa-IR.
- **Status/error detail surfaces are localized.** Applications
  (`PermissionDeniedDetailLabel`, `MonitoringFailedDetailLabel`), Connections
  (`ConnectionsErrorDetailLabel`) and History (`HistoryErrorDetailLabel`)
  banners show localized Persian/English detail text instead of raw English
  provider messages. Raw detail (e.g. `_collector.LastError`) is no longer
  rendered; it stays in the application log. (ADR-024.)
- **Culture-aware History chart dates.** `ToString("MM-dd",
  CultureInfo.CurrentCulture)` — fa-IR renders Persian-calendar dates.
- Language endonyms (`English` / «فارسی») and the TrafficLens brand remain
  intentionally untranslated.
- **Navigation bar is now a `WrapPanel`** so the 7th (About) item cannot overflow
  the 640px minimum window width.
- **Tray/window/installer share one generated brand icon** (`assets/branding/`);
  the tray icon prefers the embedded 32px frame with the TL-011 runtime-drawn
  glyph as a fallback.

### Added
- **Brand icon pipeline (TL-016 continuation).**
  `scripts/generate-icons.ps1` renders the brand glyph (rounded dark square, cyan
  down / teal up arrow — the tray art) at 16–256 px and writes a multi-size ICO +
  PNGs under `assets/branding/`. Wired once in `TrafficLens.App.csproj`
  (`<ApplicationIcon>` + `<Resource>` pack URI) and consumed by the main window,
  floating-widget title glyph, tray icon, About page and installer
  (`SetupIconFile={#BrandIcon}` + `VersionInfo*` setup metadata). The old
  `packaging/placeholder.ico` is gone. See `docs/BRANDING.md` (ADR-025).
- **About page.** Localized 7th nav page: brand identity, version, runtime, OS,
  display language, data/settings/logs paths.
- **Diagnostics support.** `DiagnosticsInfo.Build` (pure, tested); Copy
  diagnostics to clipboard (never throws); Open logs folder. Version is read from
  `AssemblyInformationalVersionAttribute` so it stays correct under single-file
  publish (no IL3000).
- `scripts/tl016-verify.ps1` — GUI verification of the published build: en-US
  label render, fa-IR content scan (nav + dashboard + History page in Persian,
  no English leak), graceful exit, no orphan process/ETW.
- ADR-024 (localized detail surfaces) and ADR-025 (branding foundation / About /
  Diagnostics).

### Verified
- en + fa-IR resx both 161 keys, identical key sets (`RequiredKeys` test).
- 410 tests (App 158 / Network 212 / Infrastructure 40); Debug + Release 0
  warnings / 0 errors.
- `tl016-verify.ps1` PASS on the published single-file win-x64 build (en-US,
  fa-IR, About page, no English leak); TL-015 smoke regression PASS.
- Localization landed in `master` via merge `9dc8335`; the branding/About
  continuation is committed directly on `master`.

## [0.1.0] — 2026-09-17 (TL-015 complete — packaging / installer)

Versioning note: 0.0.1–0.0.17 are in-repo development milestones. **0.1.0 is the
first installer-driveable release** and the version baked into the produced
artifacts; it is centralised in `Directory.Build.props`.

### Added
- **Release pipeline** `scripts/build-release.ps1` — one command does restore →
  Release build (0 Warnings/0 Errors) → all tests → single-file self-contained
  win-x64 publish → artifact validation (size, PE x64, product/file version) →
  portable ZIP → Inno Setup compile → SHA-256 checksum. Artifacts land in
  `artifacts\` (gitignored); nothing binary is committed.
- **Single-file publish** — `PublishSingleFile=true`, native-libs self-extract +
  compression, `InvariantGlobalization=false` (the `fa-IR` satellite is bundled
  inside the exe), `AssemblyName=TrafficLens` so the process/exe/reg-entry is
  `TrafficLens`.
- **Per-user Inno Setup installer** (`packaging/TrafficLens.iss`, Inno Setup
  6.7.3) — `PrivilegesRequired=lowest`, installs to
  `{localappdata}\Programs\TrafficLens`, stable AppId, Start-Menu shortcut
  (desktop icon off by task), `Excludes: "*.pdb"` (no PDBs shipped),
  post-install launch, `[UninstallDelete]` only `dirifempty`. Running-app prompt
  (`[Code]`, WMI `Win32_Process` + `WbemObjectSet.Count`) shown in `wpReady` —
  the installer never force-kills; Inno's file-in-use dialog is the second
  safety net.
- **Clean product metadata** — `Directory.Build.props`:
  `IncludeSourceRevisionInInformationalVersion=false` (ProductVersion/FileVersion
  = 0.1.0, no git hash) and `AssemblyTitle=TrafficLens Network Monitor`
  (FileDescription); `app.manifest`: asInvoker, Windows 10/11, PerMonitorV2,
  longPathAware.
- `docs/PACKAGING.md` — full packaging documentation (decisions, per-user
  scope, release command, artifact naming, upgrade/uninstall user-data policy,
  checksum, security/untrusted-build caveats).
- ADR-023 (packaging: pipeline, single-file vs MSIX/WiX, per-user Inno, no PDBs,
  never-kill policy, user-data preservation).

### Verified
- Published-exe smoke (`scripts/tl015-smoke.ps1`) and installed-app
  verification (`scripts/tl015-installed-app.ps1`) on real Windows — all PASS:
  window render, dashboard + live rate, graph section, Applications,
  Connections, History, Settings persistence, floating widget, tray, fa-IR
  Persian + en-US fallback, single-instance, graceful exit, no orphan process,
  no orphan ETW session (`TrafficLens.SingleInstance` / up-again event naming).
- Install 0.1.0 → Start-Menu shortcut + HKCU uninstall entry; uninstall removes
  install dir + shortcuts + uninstall entry; reinstall and upgrade (0.1.0 →
  0.1.1 → 0.1.0) all exit 0 with **user data
  (`%LOCALAPPDATA%\TrafficLens\settings.json` + `data\` + `logs\`) preserved
  hash-identically**; single uninstall entry / single .lnk throughout; no PDBs
  in the install dir.
- Final 0.1.0 installer SHA-256
  `0E9F11865700342E4F49EBF6B77C18C9FCAD640E56E9773FD0D826BC803D6D0B`
  (`artifacts\installer\TrafficLens-Setup-0.1.0-win-x64.exe`, checksum sidecar
  `.exe.sha256`).
- Regression: **397 tests** (App 145 / Network 212 / Infrastructure 40), Debug +
  Release 0 warnings / 0 errors.

## [0.0.17] — 2026-09-16 (TL-014 stability & performance audit complete)

### Fixed
- **Periodic idle-CPU spikes eliminated (TL-014).** The 30-min soak harness
  (max CPU ≤15% single-core in 30 s windows) was failing with ~20%+ periodic
  peaks. Three independent sources were found (via dotnet-trace Speedscope)
  and fixed, with no new user-facing features and no weakened thresholds:
  - **DashboardViewModel dispatch storm:** `SpeedSampleReady` fires once per
    adapter per second; with 5 adapters, `OnSpeedSample` called `RefreshRates`
    5×/s, producing N layout passes, N `INotifyPropertyChanged` storms, and N
    graph-buffer appends per second. Added a `_refreshPending` flag +
    `CoalesceRefresh()` (single `Dispatcher.BeginInvoke` per second) so all
    per-adapter events merge into one `RefreshRates` call.
  - **ConnectionsViewModel always-on dispatch:** every ~1 s poll dispatched
    `OnConnectionsChanged` to the UI thread and rebuilt rows even while the
    Dashboard page was active. Added `_isActive` + `SetActive(bool)`, called
    from `MainViewModel.SelectPage`; while inactive the VM buffers the data
    without dispatching and refreshes immediately on activation.
  - **WindowsConnectionProvider native polling pause:** `RunLoopAsync` called
    `EnumerateOnce()` every second unconditionally (four `GetExtendedTcpTable`
    / `GetExtendedUdpTable` P/Invokes + process resolution for ~159 connections).
    Added `IConnectionProvider.SetPollingEnabled(bool)` +
    `WindowsConnectionProvider.SetPollingEnabled` so the loop is skipped
    entirely when the Connections page is hidden; `GetCurrentConnections`
    still returns the last good snapshot.
  - **Default deactivation:** the `MainViewModel` constructor calls
    `Connections.SetActive(false)` since Dashboard is the default page.
- **Result:** harness F2 soak **avg 3.43%, max 9.88%** (well under 15%);
  main thread idle on-CPU dropped from ~50% (22,449 samples/45 s) to ~1.9%
  (113 samples/60 s) — the only remaining work is one dashboard Arrange per
  second. F1/F3/F4 phases also pass; full harness log:
  `%TEMP%\opencode\tl014-v2.log`.

### Added
- `IConnectionProvider.SetPollingEnabled(bool)` (+ `FakeConnectionProvider`
  no-op stub in tests).
- Regression tests: SingleInstanceGuard (6), FileLoggerProvider retention (3),
  SpeedRateTracker sleep-gap.
- `docs/PERFORMANCE.md` — CPU-optimization findings, measurement methodology,
  and evidence.
- ADR-022 (idle CPU optimization: coalescing, page-visibility gating, native
  polling pause, default deactivation).

### Tests
- Total **397 tests** (App 145 / Network 212 / Infrastructure 40), Debug +
  Release 0 warnings / 0 errors.

### Verified
- `scripts/tl014-stability.ps1` (Release, real host): F2 30-min soak passes
  (avg 3.43%, max 9.88%); F1 idle, F3 memory (WS +5.8 MB, Private +15.5 MB),
  F4 navigation/widget/window/restart stress all green; no exceptions in the
  structured log; graceful exit with no orphan ETW sessions.

## [0.0.16] — 2026-09-16 (TL-013 complete — settings)

### Added
- Full Settings page (`SettingsView.xaml` + `SettingsViewModel` + DI
  code-behind), replacing the minimal TL-011 tray-options popup: General
  (language, start with Windows, start minimized, minimize/close to tray),
  Floating Widget (enable, always-on-top, show/hide buttons), and Alerts (all
  5 rule rows with enable checkbox, threshold field, unit combo, and a bounded
  cooldown 1–1440 minutes with inline validation).
- Staged-save model: numeric fields and dropdowns apply on **Save** (validate →
  one logical persist → runtime apply → `RefreshFromSettings`), tray toggles
  apply **immediately**, Reset stages defaults behind a Yes/No confirmation,
  with `SavedNotice` feedback and dirty tracking.
- `AlertRuleViewModel` per rule — threshold is typed in the displayed unit and
  persisted as invariant bytes; unit-switch breaks (KB/s, MB/s, GB/s, TB/s for
  speed; MB, GB, TB for daily).
- `IFloatingWidgetService.SetAlwaysOnTop(bool)` added; `SettingsViewModel.Save`
  now sets (not toggles) the widget topmost state so unchecking it actually
  persists and applies, while the tray menu keeps `ToggleAlwaysOnTop`.
- `IStartupRegistrationService`/`StartupRegistrationService` — HKCU Run value
  `TrafficLens` = quoted exe path (+ optional ` --minimized`), removes only its
  own value; `--minimized` CLI starts hidden to tray with a dedicated log
  marker.
- `JsonSettingsService` hardening: partial settings files merge with defaults,
  unknown keys are preserved on Save, malformed JSON falls back to defaults
  without crashing.
- Localization en + fa-IR for the entire Settings surface (nav/title/sections/
  labels/units/save/reset/confirm/change-saved); required-keys test extended.
- `scripts/tl013-verify.ps1` — real-Windows Settings GUI verification (A–I):
  partial-merge/unknown-key/malformed recovery, en→fa→en switch with restart,
  tray immediate-apply + close-to-tray, widget enable/topmost-off/hide/show +
  restart persistence, page-configured 256 KB/s threshold → real alert → Reset
  with native confirmation → defaults + history DB intact, HKCU Run create/
  quoted/`--minimized`/own-value-only removal, hidden `--minimized` start,
  restart persistence, graceful exit with no orphan ETW sessions.
- ADR-021 (settings architecture: staged-save vs immediate-apply, unit-of-
  entry storage, widget always-on-top set-vs-toggle semantics).

### Tests
- `SettingsViewModelTests` — Save staging, validation (cooldown bounds,
  threshold > 0), language apply, widget state via `SetAlwaysOnTop`, alert
  config persistence, reset-to-defaults. `JsonSettingsServiceTests` —
  default-merge, unknown-key preservation, malformed fallback.
  `Fakes.cs` gained `SetAlwaysOnTop` on the fake widget service; localization
  resource keys extended.
- Total **382 tests** (App 138 / Network 206 / Infrastructure 38), Debug +
  Release 0 warnings / 0 errors.

### Verified
- Real Windows GUI verification (`scripts/tl013-verify.ps1`, Release) green
  end-to-end (blocks A–I), including a native `Reset to Defaults` MessageBox
  discovered, answered via Win32 `WM_COMMAND`, and dismissed, with the traffic
  history SQLite DB untouched (same mtime) and defaults restored on Save.

## [0.0.15] — 2026-09-15 (TL-012 complete — alerts)

### Added
- Core alert domain (`TrafficLens.Core/Alerts`, no WPF/OS dependencies):
  `AlertType` (HighDownloadSpeed / HighUploadSpeed / DailyDownloadLimit /
  DailyUploadLimit / DailyTotalLimit), `AlertConfig` (immutable record;
  all 5 rules **disabled by default** with suggested thresholds 50 MB/s /
  20 MB/s / 50 GB / 20 GB / 100 GB and a 5 min cooldown), `AlertEvent`,
  `AlertSignal`, `AlertEngine` (pure, gate-locked, clock + `TimeZoneInfo`
  injected), `AlertHistoryBuffer` (session-only, capacity 100, newest-first).
- Speed rule semantics: triggers on **upward crossing only**; remaining above
  the threshold never repeats; dropping below re-arms but does not clear the
  cooldown, so flapping yields ≤ 1 alert per cooldown window (no spam).
- Daily usage rule semantics: at most once per **local calendar day**
  (DST-safe), with the last-triggered local date persisted and restored on
  startup so a same-day restart (or crash) never re-fires; the next day
  re-arms.
- `AlertService`/`IAlertService` (App, singleton) — subscribes the existing
  `SpeedSampleReady` (evaluated via `NetworkTrafficAggregator.AggregateRates`,
  ADR-009/010 policy) and the cached history snapshot on `HistoryChanged`
  (never touches SQL); raises `AlertRaised`, logs every trigger, clean
  `Dispose`. `AlertSettings` (flat settings keys + Load/Save plus persisted
  `alerts.lastTriggered.*` dates), `AlertNotification` +
  `AlertMessageFormatter`.
- Tray balloons: `ISystemTrayService.ShowAlert` / `SystemTrayService.ShowAlert`
  (Warning balloon, 8 s; dropped+logged only when the tray is unavailable);
  `BalloonTipClicked → OpenRequested` reuses the TL-011 singleton-restore
  handler.
- Alerts page: `AlertsViewModel` (count + TimeText + newest-first rows),
  `Views/AlertsView.xaml` (+ DI code-behind), Alerts nav button + host in
  `MainViewModel`/`MainWindow`, DI registrations and `AlertRaised → ShowAlert`
  wiring in `App.xaml.cs`.
- Localization en + fa-IR: `AlertsNavLabel`, `AlertsTitleLabel`,
  `AlertsNoAlertsLabel`, `AlertsCountFormat`, `AlertTitle`, `AlertType*` ×6,
  `AlertMsg*` ×5.
- `scripts/tl012-verify.ps1` — real-Windows alert verification (speed
  crossing/no-spam/re-arm, daily once-per-day + same-day-restart persistence,
  notification-while-hidden, regression + graceful exit).
- ADR-020 (local alert architecture: no SQL in the alert path, crossing-gated
  speed rules, once-per-local-day usage rules, session buffer).

### Tests
- `AlertEngineTests` (17 engine cases + 3 buffer tests) — upward-crossing-
  only, cooldown window, no-repeat-while-above, re-arm, once-per-day,
  DST-safe UTC+14 local-day identity, next-day re-arm, restore semantics.
  `AlertServiceTests` (10) — pipeline wiring, daily suspension when history is
  unavailable, persistence, dispose. `AlertsViewModelTests` (4). `Fakes.cs`
  gained `FakeHistoryService`/`FakeAlertService`/`FakeTrayService.ShowAlert`;
  localization resource keys extended.
- Total 349 tests (App 116 / Network 206 / Infrastructure 27), Debug + Release
  0 warnings / 0 errors.

### Verified
- Real Windows GUI verification (`scripts/tl012-verify.ps1`, Release): speed
  alert threshold 256 KB/s + controlled download → exactly one notification,
  no spam while above, re-arm after drop; daily total 10 MB → once per local
  day, persisted `alerts.lastTriggered.dailyTotal`, same-day restart → no
  repeat; notification delivered while hidden with the tray alive; default
  (alerts disabled) regression + graceful exit with no ETW orphans.

## [0.0.14] — 2026-09-15 (TL-011 complete — system tray)

### Added
- `ISystemTrayService` + `SystemTrayService` (App) — one WinForms `NotifyIcon`
  via `<FrameworkReference Include="Microsoft.WindowsDesktop.App.WindowsForms" />`
  (`UseWPF` kept; no `UseWindowsForms` global usings). Single icon created once,
  disposed only on real exit; tooltip `TrafficLens`; hidden
  `WindowsForms10..._ad1` message window as the in-process verification proxy.
- Runtime-drawn 32×32 tray icon — dark rounded square with `#4FC3F7`/`#26A69A`
  chevrons on `#1E1E2E`, readable 16–32 px (`GetHicon`+`Icon.FromHandle`,
  `DestroyIcon` on dispose).
- Tray menu: Open TrafficLens / Show-Hide Floating Widget / Always on Top
  (checkable) / separator / Exit. Relabeled in place on culture change (never
  recreated). Always on Top drives the same widget pin state; widget pin
  tooltip/accessibility binds the shared `AlwaysOnTopLabel`.
- `TrayBehavior` (pure) + settings `MinimizeToTray` / `CloseToTray` (defaults
  true) and persisted once-only `TrayCloseNoticeShown`; first close-to-tray
  balloon "TrafficLens is still running in the system tray."
- `ApplicationExitCoordinator` — single idempotent `RequestApplicationExit()`
  (latch → dispose tray+widget → `Application.Current.Shutdown()`); no
  `Environment.Exit`/`Process.Kill`; collectors/history/DI disposed via the
  container. `App.xaml` `ShutdownMode="OnExplicitShutdown"`.
- `MainWindow` — `Closing` routed via `TrayBehavior` (Exit path calls the
  coordinator; HideToTray cancels + hides + balloon), `StateChanged`
  minimize→hidden when `MinimizeToTray`, `OpenRequested` restores the same
  singleton window. Minimal `…` options popup with the two tray checkboxes in
  `MainViewModel` (TL-013 owns the full settings page).
- Localization en + fa-IR: `OpenTrafficLensLabel`, `ExitLabel`,
  `MinimizeToTrayLabel`, `CloseToTrayLabel`, `TrayCloseNoticeBalloon`; tray
  technical name stays LTR.
- `scripts/tl011-verify.ps1` (final), `tl011-debug.ps1`, `tl011-shelldump.ps1`,
  `tl011-uiaprobe.ps1`, `tl011-dblclk.ps1`, `tl011-keynav.ps1` (diagnostics).

### Fixes
- `CloseToTray=false` was closing the window without exiting (process stayed
  alive under `OnExplicitShutdown`); the `MainWindow.Closing` exit branch now
  calls `RequestApplicationExit()` (caught by real-Windows verification, see
  ADR-019).

### Tests
- `TrayBehaviorTests` (10) — close-action resolution, minimize/close flags,
  once-only notice, idempotent coordination; `ApplicationExitCoordinatorTests`
  (4) — exit latching + disposal ordering with fakes. `Fakes.cs` gained
  `FakeSettingsService`/`FakeTrayService`/`FakeFloatingWidgetService`;
  FloatingWidgetViewModel acquires `AlwaysOnTopLabel` localization test;
  localization resource keys extended.
- Total 317 tests (App 84 / Network 206 / Infrastructure 27), Debug + Release
  0 warnings / 0 errors.

## [0.0.13] — 2026-09-15 (TL-010 complete — floating widget)

### Added
- `FloatingWidgetViewModel` (App) — rides the existing live rate pipeline
  (`SpeedSampleReady`/`NetworkChanged`/`AdaptersChanged`; no second poll loop or
  timer), computes the ADR-009/010 aggregate via `NetworkTrafficAggregator`,
  formats Download/Upload/Total through `DataRateFormatter`, localized
  title + labels, `TogglePinCommand`/`CloseWidgetCommand`, dispatcher-marshalled
  updates, IDisposable (same pattern as `DashboardViewModel`).
- `FloatingWidgetWindow` (App) — 280×110 frameless always-on-top widget
  (`WindowStyle=None`, `ResizeMode=NoResize`, `ShowInTaskbar=False`), dark theme
  from shared `DarkTheme.xaml`, drag by empty area, pin toggle (📌/📍) + hide (✕)
  buttons, rate values forced LTR under RTL.
- `FloatingWidgetService` (App, singleton) — single widget instance;
  `Show`/`Hide`/`Toggle`/`RestoreIfEnabled`; repeat show activates (no
  duplicates); widget close hides only; `Dispose` really closes the window on
  shutdown (never keeps the app alive).
- `WidgetPositionHelper.Clamp` (App) — pure multi-monitor position recovery:
  union of monitor work areas, negative virtual-screen coords preserved
  (secondary monitor left of primary), off-screen/disconnected-monitor clamping;
  real areas from `SystemParameters.VirtualScreen*`.
- Settings via existing `ISettingsService` (`settings.json`):
  `FloatingWidgetEnabled`, `FloatingWidgetAlwaysOnTop` (default on),
  `FloatingWidgetLeft`, `FloatingWidgetTop`.
- Main UI: header toggle button with `Show`/`Hide Floating Widget` labels,
  `MainViewModel.ToggleFloatingWidgetCommand` + `FloatingWidgetToggleLabel`;
  `MainWindow.Closing` hides the widget so normal shutdown is unaffected
  (ADR-015/TL-007F); `App.xaml.cs` registers the service and calls
  `RestoreIfEnabled()` at startup.
- Localization: `FloatingWidgetLabel`, `AlwaysOnTopLabel`,
  `ShowFloatingWidgetLabel`, `HideFloatingWidgetLabel` in en + fa-IR.

### Tests
- `FloatingWidgetViewModelTests` (8) — aggregate→VM mapping, tunnel/down-adapter
  exclusion, rate formatting, culture re-format + localized labels, pin icon,
  dispose. `WidgetPositionHelperTests` (10) — in-bounds, off-screen right/bottom,
  negative coords, secondary-monitor-left (kept vs clamped), multi-monitor union,
  empty areas, oversized window. Localization resource keys extended.
- Total 302 tests (App 69 / Network 206 / Infrastructure 27), Debug + Release
  0 warnings / 0 errors.

## [0.0.12] — 2026-09-15 (TL-009 complete — SQLite history)

### Added
- Core history domain (`TrafficLens.Core/History`, no WPF/OS dependencies):
  - `HistoryRange` (Today / Yesterday / Last 7 Days / Last 30 Days / Lifetime),
    `TrafficUsage`, `DailyUsagePoint`, and immutable `HistorySnapshot`
    (`For(range)` derives per-range totals; `Unavailable(lastError)`).
  - `HistoryRangeCalculator` — half-open local-date ranges via
    `TimeZoneInfo.ConvertTimeFromUtc` → `DateOnly` (DST / local-midnight correct;
    Last7Days `(today-6, today+1)`, Last30Days `(today-29, today+1)`).
  - `TrafficHistoryAccumulator` — counter-sample DELTAS → per-UTC-minute buckets;
    first observation per adapter is baseline-only; non-negative deltas only;
    `DrainCompleted` (full minutes) vs `DrainAll` (open minute clamped 1..60).
  - Contracts `ITrafficHistoryRepository` + `ITrafficHistoryService`.
- Infrastructure (`TrafficLens.Infrastructure/History`):
  - `SqliteTrafficHistoryRepository` — schema v1 (`PRAGMA user_version`), WAL,
    `busy_timeout`, `Pooling=false`; tables `traffic_samples` +
    `daily_usage`; appends are single transactions guarded by
    `INSERT OR IGNORE` + `changes()==1` so **restarts/crashes can never
    duplicate history**; `daily_usage` kept forever, raw samples pruned after
    90 days on startup.
  - `TrafficHistoryService` — rides the existing `CounterSampleReady` events
    (never a second NIC polling loop), tunnel-excluding like the ADR-009/010
    aggregate, re-baselines on resets/reconnects/reboots (nothing fabricated),
    30 s background flush of completed minutes, drains + flushes on stop, and
    exposes a cached immutable `HistorySnapshot` (SQL never on the UI thread).
  - `HistoryServiceCollectionExtensions.AddHistoryServices(dbPath)`.
- `AppPaths` — DB at `%LOCALAPPDATA%\TrafficLens\data\trafficlens.db`;
  `EnsureDirectories` creates the `data` folder.
- App History page:
  - `HistoryViewModel` — five-range selector button row, Download/Upload/Total
    summary cards, "No history yet" overlay when Lifetime is zero, storage
    failure banner (`HistoryUnavailableLabel` + `LastError`).
  - `HistoryView` + native `HistoryBarChartControl` (`FrameworkElement`,
    `OnRender`, no chart library) — bars always oldest-left → newest-right under
    RTL; Today/Yesterday = 1 bar, 7d = 7 bars, 30d/Lifetime = 30 daily bars;
    tooltips show date → `DataSizeFormatter` totals.
  - `MainViewModel.ShowHistoryCommand` + localized nav label; `MainWindow`
    History host (Dashboard / Applications / Connections / History);
    `App.xaml.cs` registers the ViewModel/View and starts the history service.
- Localization: `HistoryLabel`, `HistoryDailyTrafficLabel`, `TodayLabel`,
  `YesterdayLabel`, `Last7DaysLabel`, `Last30DaysLabel`, `LifetimeLabel`,
  `HistoryNoDataLabel`, `HistoryUnavailableLabel` added to `Strings.resx` (en)
  and `Strings.fa-IR.resx`.
- Tests: new `TrafficLens.Infrastructure.Tests` project (27 tests: range
  calculator, accumulator, SQLite repository incl. restart-idempotent append,
  service) added to the solution; `HistoryViewModelTests` (6) and localization
  resource-key tests in `TrafficLens.App.Tests`.
- Verification: `--history` mode added to `TrafficLens.Network.Verification`
  (real collector + throwaway DB + real downloads + restart idempotency check).
- ADR-017 (durable aggregated history; minute buckets + daily rollup; restart
  idempotency; 90-day raw retention).

### Verified
- Build Debug + Release: **0 warnings, 0 errors**.
- Tests: **284/284 passing** (206 Network + 51 App + 27 Infrastructure).
- Real Windows verification (`--history`, live host, temp DB): a real 20 MB
  `speed.cloudflare.com` download recorded as Today = 20,182,568 B down /
  79,655 B up (peak ~5.78 MB/s observed); 30-day daily series correct; a second
  service instance over the same DB reported the identical Lifetime
  (`unchanged: true`) — restart cannot double-count. DB = 16,384 bytes after the
  runtime; `daily_usage` aggregates keep steady-state bounded even as raw minute
  samples archive.

## [0.0.11] — 2026-09-15 (TL-008 complete — active connections)

### Added
- Core connection model and selection (presentation-safe, non-mutating):
  - `ConnectionInfo` extended — nullable remote endpoint, `ConnectionAddressFamily`,
    process start-time identity (`ProcessStartTimeUtcTicks`), `ExecutablePath`,
    `IconAvailable`, `Timestamp`; new `ConnectionProtocol`, `ConnectionState`, and
    `ConnectionAddressFamily` enums.
  - `ConnectionKey` — stable identity `(Protocol, AddressFamily, LocalAddress,
    LocalPort, RemoteAddress?, RemotePort?, ProcessId)` for in-place row updates.
  - `EndpointFormatter` — culture-safe, always-LTR `address:port` formatting; the
    remote endpoint renders empty for a listening/unconnected socket (unspecified
    `0.0.0.0`/`::` + port 0) instead of a misleading peer (ADR-016).
  - `ConnectionFilter` (All/Established/Listening/Tcp/Udp/Ipv4/Ipv6),
    `ConnectionFiltering` (match + search), `ConnectionSort` (Default/Process/
    ProcessId/Protocol/State/Local/Remote) with deterministic tie-breaks.
  - `IConnectionProvider` extended — `ConnectionsChanged`, `LastError`,
    `GetCurrentConnections`, `GetActiveConnectionsAsync(ct)`, `StartAsync`,
    `StopAsync`, `IDisposable`.
- Network collection (`TrafficLens.Network/Connections`):
  - `NativeConnectionTableReader` — `GetExtendedTcpTable` (`TCP_TABLE_OWNER_PID_ALL`)
    + `GetExtendedUdpTable` (`UDP_TABLE_OWNER_PID`), IPv4 and IPv6; 4-byte
    little-endian entry-count header, per-row layouts (TCPv4 24 B, TCPv6 56 B,
    UDPv4 12 B, UDPv6 28 B), network→host port byte order, 64 KB buffer grown on
    `ERROR_INSUFFICIENT_BUFFER`.
  - `ConnectionTableParser` — pure static parsers over the native buffers
    (unit-tested with synthetic payloads; no live table required).
  - `ConnectionProcessResolver` — bounded cache (TTL 3 s, capacity 512, FIFO,
    negative caching) over `IProcessMetadataProvider`, keyed by full
    `ProcessInstanceId`; never throws.
  - `WindowsConnectionProvider` — ~1 s off-UI poll loop; partial-table failure is a
    warning (successful tables kept), total failure keeps the last good snapshot and
    sets `LastError`, any success clears it; `StopAsync` uses `ConfigureAwait(false)`
    (ADR-015).
- App Connections page and navigation:
  - `ConnectionsViewModel` + `ConnectionRowViewModel` — dispatcher-marshalled
    `ConnectionsChanged`, in-place row updates (rebuild only when the key sequence
    changes), per-refresh icon budget, error banner, empty state, Filter +
    Address-Family + Sort combo boxes and a search box.
  - `ConnectionSortOption` / `ConnectionFilterOption`; `ConnectionsView` (XAML +
    code-behind DI); `MainWindow` Dashboard/Connections navigation; `App.xaml.cs`
    registers the ViewModel/View and starts `IConnectionProvider`.
- Localization: en + fa-IR keys for column headers, filters, sort keys, TCP states,
  empty/error, and unknown process; endpoints stay LTR under RTL.
- Verification: `--connections` mode added to `TrafficLens.Network.Verification`
  (non-elevated; optional `TL_VERIFY_PORT` fixed listener port).
- Tests: `ConnectionTableParserTests`, `ConnectionKeyTests`,
  `ConnectionSelectionTests`, `EndpointFormatterTests` (Network) and
  `ConnectionsViewModelTests` (App).

### Verified
- Build Debug: **0 warnings, 0 errors**.
- Tests: **251/251 passing** (206 Network + 45 App).
- Real Windows verification (`--connections`, live host): 112 connections (78 TCP /
  34 UDP, 99 IPv4 / 13 IPv6); in-process listener observed as `Listen`; curl download
  attributed `Established` with correct PID/process name; cross-checked vs
  `netstat -ano` (TCP state histogram + UDP count match the MIB source;
  `Get-NetTCPConnection`'s `Bound` rows are cmdlet-synthesized, not in the table).
- GUI smoke (non-elevated): provider started (1 s poll), no exceptions, clean
  teardown (no orphan ETW session / leftover process).

## [0.0.10] — 2026-09-15 (TL-007F complete — shutdown deadlock fix)

### Fixed
- **Lingering `TrafficLens.App` after a graceful window close (TL-007F).** Root
  cause (proven via live repro + `dotnet-dump`): `WindowsNetworkTrafficCollector`
  is disposed on the WPF dispatcher thread during `App.OnExit` →
  `StopAsync().GetAwaiter().GetResult()`; `StopAsync`'s `await loop;` captured the
  `DispatcherSynchronizationContext`, so the continuation was posted to a
  dispatcher blocked in `GetResult()` — `StopAsync` never resumed, `Dispose`
  never returned, and the process stayed alive (no window, low CPU, single
  foreground thread). Fix: `await loop.ConfigureAwait(false)` so shutdown is
  deterministic from any thread/context (ADR-015); no `Environment.Exit`, no
  forced kill.
- Regression test `Dispose_FromNonPumpingSyncContext_DoesNotDeadlock` added
  (non-pumping `SynchronizationContext` + 5 s deadline) — fails (timeout) on the
  pre-fix code, passes with the fix.

### Verified
- Build Debug + Release: **0 warnings, 0 errors**.
- Tests: **193/193 passing** (163 Network + 30 App).
- Real Windows GUI (Release), after the fix:
  - **Non-elevated:** 3 consecutive launch → graceful close cycles; every cycle the
    process exited promptly (no residual process), no ETW session; log ends with
    `TrafficLens exiting` → `Process traffic collector stopped` → `Network traffic
    collector stopped` (this last line was the previously-missing/blocked one).
  - **Elevated:** ETW session `TrafficLensProcessTrace` Running → ~20 MB download
    while monitoring → graceful close → process exited, session gone from
    `logman query -ets`, clean shutdown log.
- The lingering-process known issue from 0.0.9 is resolved.

## [0.0.9] — 2026-09-15 (TL-007 complete — Applications-list UI)

### Added
- Core selection/formatting (presentation, no collector mutation):
  - `ProcessSampleSelection` + `ProcessSortKey` — filtering + seven sort keys
    (total/download/upload rate, downloaded/uploaded/total bytes, name) with
    deterministic tie-breaking (name, start time, PID); top-consumer helpers.
  - `DataSizeFormatter` — binary-unit byte totals (B/KB/MB/GB), culture-aware
    decimal separator, negatives clamped to 0 B (ADR-011 technical notation).
- App Applications page:
  - `ApplicationsViewModel` + `ProcessRowViewModel` — per-instance rows keyed by
    `ProcessInstanceId` (reused PID with a new start time = distinct row), in-place
    updates (no re-add/flicker), top-consumer cards, sort + search (name
    case-insensitive substring + PID prefix), localized status text for
    `Running`/`Exited`/unknown.
  - `ApplicationSortOption` + per-row state from the sample's `IsRunning` flag
    (supported by the engine's `Running-until-exit` revalidation semantics).
  - `ProcessIconResolver` — shell32 `SHGetFileInfo` P/Invoke
    (`SHGFI_ICON | SHGFI_LARGEICON`) + `CreateBitmapSourceFromHIcon` +
    `DestroyIcon`, frozen fallback, bounded FIFO cache (128), max 8 extractions
    per refresh; UI-thread only; no `System.Drawing` dependency.
  - `ApplicationsView` (XAML + code-behind DI): status banner (permission-deny +
    start-monitoring actions), top-consumer cards, sort ComboBox + search box,
    list header + `ItemsControl` rows (icon, name, PID, status, rates, totals).
- Navigation: `MainWindow` Dashboard / Applications nav buttons + `ContentControl`
  host switched by `MainViewModel.ShowDashboardCommand`/`ShowApplicationsCommand`.
- Startup: `App.xaml.cs` registers `ProcessIconResolver`, `ApplicationsViewModel`,
  `ApplicationsView`; starts `IProcessTrafficCollector` after the network
  collector (fire-and-forget with try/catch).
- Localization: Applications keys added to `Strings.resx` (en) and
  `Strings.fa-IR.resx` (fa, valid UTF-8) — per-process labels, sort/search,
  status texts, permission banner, and explicit Restart-as-Administrator action.
- Tests: 45 new (`ProcessSampleSelectionTests` 11, `DataSizeFormatterTests` 12
  cases, 4 new engine liveness tests — resolved-runs, exit-after-revalidation,
  PID-reuse, unknown-PID; `ApplicationsViewModelTests` 16; localized resource
  keys extended).
- ADR-014 (no automatic elevation; explicit restart-as-administrator).

### Verified
- Build Debug + Release: **0 warnings, 0 errors**.
- Tests: **192/192 passing** (162 Network + 30 App).
- Real Windows GUI (Release):
  - Elevated: ETW session `TrafficLensProcessTrace` Running with buffers; app
    survived ~20 MB/s-scale transfers; graceful `CloseMainWindow` →
    "TrafficLens exiting" → "Process traffic collector stopped" → ETW session
    closed cleanly (no orphan).
  - Non-elevated: permission-denied path logged; app usable; dashboard works; no
    ETW session; no crash; no auto-UAC.

### Known Issue
- After a graceful window close the `TrafficLens.App` process can linger (no
  window handle, low CPU) even though shutdown logs and the ETW session shutdown
  are clean. Reproduced elevated and non-elevated; investigation queued for the
  next milestone.

## [0.0.8] — 2026-09-14 (TL-007 Per-Process Traffic — collector milestone)

### Added
- `TrafficLens.Core` per-process contracts:
  - `ProcessTrafficCollectorStatus` — Stopped / Starting / Running / PermissionDenied / Failed.
  - `IProcessTrafficCollector` extended with `Status` and `LastError`.
  - `ProcessTrafficSample` — pid, process-start-time identity, name, executable path,
    icon-availability, cumulative byte totals, monotonic-window rates, timestamp.
- `TrafficLens.Network/Process/` — real ETW-backed collector:
  - `WindowsEtwProcessTrafficCollector` — real-time kernel session
    (`TraceEventSession` + `NetworkTCPIP`); eight Tcp/Udp IPv4/IPv6 handlers map the
    **payload PID** + size into `NetworkTransferEvent`; ~1 s snapshot loop raises
    `SamplesReady`; non-elevated run reports `PermissionDenied` + `LastError`
    without crashing or forcing UAC; clean `StopAsync` (session + consume + loop).
  - `ProcessTrafficAccountingEngine` — per-instance buckets
    (`ProcessInstanceId = pid + start time`), monotonic sliding-window rates,
    metadata resolve/rekey, PID-reuse isolation, `<unknown pid N>` bucket for
    unresolvable processes (never merged), idle-prune 120 s, 4096 cap, next-check
    throttling (metadata revalidation 15 s / unresolved retry 10 s);
    allocation-free hot path.
  - `WindowsProcessMetadataProvider` — guarded `Process` reads; PID-reuse detection
    via start-time mismatch (2 s tolerance); never throws.
  - Supporting models: `NetworkTransferEvent`, `ProcessInstanceId`,
    `ProcessMetadata`/`ProcessMetadataResult`, `ProcessProtocolTotals`
    (Tcp/Udp × Received/Sent, IPv4/IPv6 × Received/Sent).
- DI: `IProcessTrafficCollector` registered as a singleton in
  `NetworkServiceCollectionExtensions`.
- Verification console `--process` mode (per-process live check).
- ADR-013 (ETW mechanism, privilege behavior, PID-reuse, VPN semantics) and
  `NETWORK_COLLECTION.md` TL-007 section.
- Tests: 18 new (`ProcessTrafficAccountingEngineTests` + `WindowsProcessMetadataProviderTests`).

### Verified
- Build Debug + Release: **0 warnings, 0 errors**.
- Tests: **147/147 passing** (133 Network + 14 App).
- Real elevated verification (curl.exe + powershell.exe): two distinguishable
  apps; two curl instances as distinct (pid, start) buckets; protocol-totals
  invariant holds; bounded (11 samples, ~2.6 MB growth); clean stop.
- Non-elevated verification: `PermissionDenied` + `LastError`, no crash.

## [0.0.7] — 2026-09-14 (TL-006 Live Traffic Graph)

### Added
- `TrafficLens.Core/Graph/` — no-WPF graph layer over raw bytes/second:
  - `TrafficGraphPoint` — timestamped raw download/upload rates (no formatted strings
    in graph data; formatting is render-only).
  - `TrafficSampleBuffer` — bounded (5.5 min retention / 1320 samples) thread-safe ring
    buffer; duplicate same-poll timestamps rejected (one sample per poll regardless of
    per-adapter events); wall-clock age-out; non-mutating `Slice(window, now)` so range
    switching never clears history; real timestamps preserved (gaps drawn honestly).
  - `AdaptiveGraphScale` — single shared Y max for both series; immediate spike growth;
    hysteretic shrink (consecutive sustained lows < 35% only) to prevent flicker; 2 KB/s
    floor prevents divide-by-zero at zero traffic (ADR-012).
  - `GraphTimeRange` — 30 s / 60 s / 300 s.
- `TrafficGraphControl` (App `Controls/`) — lightweight native WPF `FrameworkElement`;
  renders both series as `StreamGeometry` in `OnRender` over a 4-line grid with adaptive
  axis labels (`DataRateFormatter`); forces LTR so the timeline is always oldest-left →
  newest-right even under fa-IR; no chart library, no per-sample UI elements.
- `DashboardViewModel` graph support: feeds the existing ADR-009/010 `AggregateRates`
  aggregate into the buffer once per poll (dedupe absorbs multi-adapter burst events);
  `SelectGraphRangeCommand`; localized graph labels; culture-change re-render.
- `MainWindow` "Live Traffic" section: range buttons (30 s / 1 m / 5 m), download/upload
  legend swatches, graph control (height 190, scrollable with the dashboard).
- en + fa-IR resources: `GraphLiveTrafficLabel`, `GraphLast30SecondsLabel`,
  `GraphLast1MinuteLabel`, `GraphLast5MinutesLabel`, `GraphNowLabel`,
  `GraphDownloadSeriesLabel`, `GraphUploadSeriesLabel`.
- Tests: `TrafficSampleBufferTests` + `AdaptiveGraphScaleTests` (20) in
  `TrafficLens.Network.Tests`; dashboard graph tests (append/dedupe/range-slice/
  no-network/zero/large, 6) in `TrafficLens.App.Tests`; graph keys added to the
  localization resource test.
- ADR-012 (native WPF rendering + documented scale hysteresis).

### Verified
- Build Debug + Release: **0 warnings, 0 errors**.
- Tests: **129/129 passing** (115 Network + 14 App).
- Real Windows GUI (Release, live traffic): graph section renders; ranges 30 s / 1 m / 5 m
  switch without clearing history; rates tracked live traffic (→ 10.68 Mbps download);
  en ↔ fa-IR switch re-localized all graph strings without crashing; resize stays
  responsive; clean close logged "TrafficLens exiting". No exceptions in the log.

## [0.0.6] — 2026-09-14 (TL-005 Dashboard)

### Added
- `TrafficLens.Core/Conversion/DataRateFormatter` — presentation formatting for
  live rates: adaptive B/s / KB/s / MB/s and Mbps; culture-aware decimal
  separator; unit symbols kept as technical notation (untranslated).
- `DashboardViewModel` + `AdapterListItemViewModel` (App) — MVVM dashboard over
  the existing collector/provider abstractions, bound to `MainViewModel.Dashboard`.
- `MainWindow` dashboard layout: Download / Upload / Total cards, Active Adapter
  card, Network Adapters list; scrollable, stays intact at smaller window sizes.
- English (en) and Persian (fa-IR) resources for dashboard labels, adapter kinds
  and connection states.
- `tests/TrafficLens.App.Tests` (xUnit, net8.0-windows) for ViewModel mapping,
  no-network/reconnect/VPN-only states, culture switch, and resource existence.
- Formatter tests (15 cases) in `tests/TrafficLens.Network.Tests`.
- ADR-011 (thin event-driven dashboard; Core formatter; aggregate policy reuse).

### Changed
- `App.xaml.cs` — registers `AddNetworkServices()`, `DashboardViewModel`; starts
  the collector on startup; logs "TrafficLens exiting" on clean shutdown; DI now
  also disposes collector (stops the poll loop).
- `MainViewModel` — reduced to window chrome (title, status, language switch);
  dashboard state moved into the injected `Dashboard` property.
- VM updates are marshalled to the WPF Dispatcher (`InvokeAsync`); all collector/
  provider/localization event subscriptions are released via `IDisposable`.
- Adapter-list automation names via `ToString()` so screen readers see adapter
  names, not ViewModel type names.

### Verified
- Build Debug + Release: **0 warnings, 0 errors**.
- Tests: **103/103 passing** (95 Network + 8 App).
- Real Windows GUI (Release, live traffic): Download card moved
  0 B/s → 844.31 KB/s (6.92 Mbps) → decaying to 32.74 KB/s as traffic flowed;
  adapter list showed Wi-Fi + OpenVPN TAP/DCO + Wi-Fi Direct virtuals + Bluetooth;
  en and fa-IR dashboards both rendered without crashing; clean close logged
  "TrafficLens exiting".
- ADR-009/010 policy holds: peak ~6.92 Mbps on the system cards matched the
  non-tunnel aggregate while tunnel rows keep their own rates; no double counting.

## [0.0.5] — 2026-09-14 (TL-004 audit + gap fix)

### Changed
- `NetworkAdapterKindMapper.Map(type, description)`: description-aware classification
  so OpenVPN TAP/DCO (`HighPerformanceSerialBus`/53) are now `Tunnel`, and
  virtual nics (Hyper-V/VMware/Wi-Fi Direct) are correctly `Virtual`.
- `AdapterFilter.IsMonitored` relaxed: Unknown-type adapters with recognized
  tunnel/virtual driver descriptions remain visible (prevents WireGuard hidden
  on machines where it reports Unknown type).
- `DefaultAdapterSelector` uses the description-aware overload so TAP stays
  non-default; no change to live default (Wi-Fi) on this machine.

### Added
- Tests: description-aware kind mapping (VPN drivers, virtual nics),
  Unknown+description filter guard, TAP default-selection regression test.

### Verified
- Build (Debug + Release): 0 warnings, 0 errors.
- Tests: 80/80 passed.
- Live enumeration confirmed: OpenVPN TAP/DCO → Tunnel; Wi-Fi Direct → Virtual;
  Wi-Fi → Wireless (default); Bluetooth PAN → Ethernet; all adapters visible.

### Notes
- No new classes; all changes are small refinements in existing TL-002 files.
- No rewrite performed; only the identified gap (TAP/DCO misclassification) was
  fixed per the TL-004 audit instruction.

## [0.0.4] — 2026-09-14 (M1, TL-003)

### Added
- Rate calculation (`NetworkSpeedCalculator`): cumulative-counter deltas divided by
  actual monotonic elapsed time (QPC via `Stopwatch`), no 1 s assumption.
- `SpeedRateTracker`: per-adapter independent baselines with re-baseline rules
  (first sample, counter reset/wrap/decrease, zero/invalid elapsed, adapter
  disappearance/replacement) — never emits a fake spike.
- `SpeedSampleReady` is now raised with real per-adapter rates; `GetCurrentSamples()`
  returns current rate samples (`NetworkSpeedSample`).
- `NetworkTrafficAggregator.AggregateRates` — system "Internet Total" using the
  tunnel-excluding non-overlapping policy; per-adapter views keep tunnel/VPN traffic.
- `DataRateConverter` (Core): B/s, KB/s, MB/s, Kbps, Mbps, Gbps numeric conversions
  for the UI; raw values remain bytes/second (ADR-010).
- Tests: `NetworkSpeedCalculatorTests`, `SpeedRateTrackerTests`, `DataRateConverterTests`,
  aggregate-rate and collector rate tests.

### Changed
- `WindowsNetworkTrafficCollector.RunLoopAsync` computes and raises rate samples each
  poll (bounded one-baseline-per-adapter state; background thread only).

### Verified
- Build (Debug + Release): 0 warnings, 0 errors.
- Tests: 65/65 passed.
- Real Windows: window-mean Wi-Fi rate 803,647 B/s down / 18,423 B/s up vs native
  `Get-NetAdapterStatistics` 1,120,590 / 26,086 over a longer overlapping window —
  plausibly aligned (see `docs/NETWORK_COLLECTION.md`).

### Notes
- String formatting of rates is deferred to UI (TL-005).
- Dashboard/graph/per-process/connections not touched (scope restriction).

## [0.0.3] — 2026-09-14 (M1, TL-002)

### Added
- `TrafficLens.Network`: global network collector
  - `WindowsNetworkTrafficCollector` (implements `INetworkTrafficCollector`):
    poll loop, per-adapter cumulative counters, `CounterSampleReady`, `NetworkChanged`,
    `NetworkChange` address/availability hooks, counter reset/wrap re-baseline.
  - `WindowsNetworkAdapterProvider` (implements `INetworkAdapterProvider`).
  - `NetworkInterfaceSource`, `RawAdapterSnapshot`, `NetworkAdapterKindMapper`,
    `AdapterFilter`, `DefaultAdapterSelector` (very thorough adapter detection).
  - `NetworkTrafficAggregator` (totals + tunnel-exclusion policy, ADR-009).
  - `NetworkServiceCollectionExtensions.AddNetworkServices()` DI registration.
- Core: `NetworkCounterSample` model; `CounterSampleReady` +
  `GetCurrentCounterSamples()` on `INetworkTrafficCollector` (ADR-007).
- Tests: `TrafficLens.Network.Tests` — 36 xUnit tests, all passing.
- Verification console: `TrafficLens.Network.Verification` (real collector + JSON dump).
- Docs: `docs/NETWORK_COLLECTION.md` decision + verification evidence; ADR-007/008/009
  in `docs/DECISIONS.md`.

### Changed
- `TrafficLens.Network.csproj`: added `Microsoft.Extensions.DependencyInjection.Abstractions`.
- Build now includes tests + verification projects.

### Verified
- Build (Debug + Release): 0 warnings, 0 errors.
- Tests: 36/36 passed.
- Real Windows cross-check vs `Get-NetAdapterStatistics` — counters match
  (Wi-Fi 487.0 MB / 80.8 MB at capture), see `docs/NETWORK_COLLECTION.md`.

### Notes
- Rates (`NetworkSpeedSample`, `SpeedSampleReady`) intentionally left unimplemented;
  produced by TL-003.

## [0.0.2] — 2026-09-14 (M0 verification)

### Added
- Startup logging in `App.xaml.cs` (startup, culture, MainWindow shown) to confirm
  boot sequence in structured logs.

### Fixed
- Nothing required at runtime; all TL-001 startup/localization checks passed.

### Verified
- GUI smoke test passed on Windows: app starts cleanly, dark theme loads,
  English and Persian (fa-IR) resources resolve, RTL culture applies without crash,
  structured logs written, clean shutdown.

## [0.0.1] — 2026-09-14 (M0)

### Added
- Solution `TrafficLens.sln` with four projects:
  - `TrafficLens.App` (WPF UI, `net8.0-windows`)
  - `TrafficLens.Core` (models, interfaces)
  - `TrafficLens.Network` (empty placeholder project)
  - `TrafficLens.Infrastructure` (settings, logging)
- Project references configured (App -> Core/Network/Infrastructure; Network/Infrastructure -> Core).
- MVVM base: `ViewModelBase`, `RelayCommand`, `MainViewModel`.
- DI composition root in `App.xaml.cs` (Microsoft.Extensions.DependencyInjection).
- Structured JSON file logging (`Infrastructure/Logging/FileLogger`), logs under
  `%LOCALAPPDATA%\TrafficLens\logs`.
- JSON settings store (`JsonSettingsService`), persists language.
- Localization foundation:
  - Core `ILocalizationService`
  - App `LocalizationService` with resource-based lookup and English fallback
  - `Resources/Strings.resx` (English, neutral)
  - `Resources/Strings.fa-IR.resx` (Persian)
  - RTL support via `FlowDirection` tied to culture
- Minimal dark theme (`Themes/DarkTheme.xaml`) and dark main window with dashboard placeholders.
- Core domain contracts for later milestones:
  - `INetworkTrafficCollector`, `IProcessTrafficCollector`, `IConnectionProvider`,
    `INetworkAdapterProvider`, `ISettingsService`, `ILocalizationService`
  - Models: `NetworkSpeedSample`, `NetworkAdapterInfo`, `ProcessTrafficSample`,
    `ConnectionInfo`
- Repository documentation (`AGENTS.md`, `README.md`, `TASKS.md`, docs/...).

### Notes
- .NET 8 SDK 8.0.425 installed locally at `C:\dotnet` (system had no SDK).
- No network monitoring implemented in this milestone (by design).