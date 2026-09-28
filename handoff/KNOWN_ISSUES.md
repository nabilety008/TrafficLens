# KNOWN ISSUES — TrafficLens

Three separate sections. Do **not** confuse a fixed trap with an active bug.

---

# 1. CURRENT LIMITATIONS

Real, present-tense limitations of the v0.1.4 build.

## `AGENTS.md` is stale — it still describes a WPF app

**Read this before trusting `AGENTS.md`.** It is the mandated first read for every
agent, and it predates the WinUI 3 migration. Confirmed drift as of the closeout
(71 lines):

| `AGENTS.md` says | Reality |
|---|---|
| L17: "network monitoring **WPF desktop application**" | Shipping UI is **WinUI 3** (`UseWinUI=true`) |
| L21: "Stack: C#, **WPF**, …" | C#, **WinUI 3**, MVVM, DI, SQLite, file logging |
| L29–32: layout lists 4 `src` projects | The solution has **6** — `TrafficLens.WinUI` and `TrafficLens.WinUI.Tray` are missing from the layout |
| L70: `Build: dotnet build TrafficLens.sln` | That command **fails**: `TrafficLens.WinUI` declares `<Platforms>x64</Platforms>`, so `-p:Platform=x64` is mandatory (see `BUILD_AND_TEST.md`) |

- `TrafficLens.App` still exists and is still referenced — it is **not** dead code.
  The WinUI project *links* its services and `.resx` resources, so `AGENTS.md` is
  right that strings come from `TrafficLens.App/Resources/Strings*.resx`. It is the
  "UI project" framing that is wrong.
- The solution also contains `tests\TrafficLens.Network.Verification` (a console
  harness) and five test projects; `AGENTS.md` does not mention any of them.
- **Trap:** following `AGENTS.md` literally sends an agent to the wrong UI project
  and to a build command that cannot succeed. Trust the code and this handoff over
  `AGENTS.md` for anything UI- or build-related.
- **Not fixed here** — the v0.1.4 closeout committed documentation only, and
  rewriting `AGENTS.md` was outside its agreed scope. It is listed in
  `NEXT_TASKS.md` as pending user-approved work.

## Unsigned release

- Both `artifacts\installer\TrafficLens-Setup-0.1.4-win-x64.exe` and the published
  `TrafficLens.WinUI.exe` report `Get-AuthenticodeSignature` → **`NotSigned`**.
- Consequence: enterprise **Smart App Control** / **WDAC** / AppLocker policies
  commonly block unsigned binaries. This was measured on the release host as *not*
  blocking, but that is a property of that machine's reputation state, **not** a
  guarantee. Never assert "blocked" or "allowed" without a Code Integrity event.
- There is no deterministic way to run an unsigned build under enforced SAC. Either
  Microsoft app intelligence classifies the file as safe, or the binary carries an
  **RSA** certificate chaining to the Microsoft Trusted Root Program. Cloud
  reputation accrues only through real distribution, so it is not a reproducible
  gate, and a self-signed certificate is not honoured.
- The pipeline is signing-ready: `scripts\build-release.ps1 -Sign` performs
  SHA-256 + RFC 3161 timestamping over the discovered product-binary set. Signing was
  **deferred**, so the published checksums describe the **unsigned** build and must
  be recomputed after any signing.

## Per-process data needs Administrator

The ETW per-process collector requires an elevated process
(`Enabling the ETW kernel network provider requires an elevated (Administrator)
process`). Without elevation it logs a `Warning` and reports a degraded status via
`ProcessTrafficCollectorStatus` instead of failing. This is expected and non-fatal —
do not "fix" it by making the app self-elevate.

## Installer not exercised end to end

- Install, upgrade, and uninstall were **NOT TESTED**.
- Reason: an earlier TrafficLens installation already occupied this release's AppId
  uninstall registration at `%LOCALAPPDATA%\Programs\TrafficLens` (containing the
  superseded WPF `TrafficLens.exe`), so a silent install of the final installer would
  overwrite that directory and rewrite the same registration rather than test in
  isolation.
- The installer was verified **statically** and by successful compilation:
  AppId `{8F0E8A8F-7B1D-4A5E-9C2D-3E5F6A7B8C9D}`, entry point
  `TrafficLens.WinUI.exe`, `VersionInfoProductVersion` / `VersionInfoVersion` both
  `0.1.4`, and `ProductVersion 0.1.4` in its version resource.
- Note the `[InstallDelete]` entry that removes `{app}\TrafficLens.exe` is the
  **intended** upgrade-time cleanup of the old WPF build, not a shipped payload.

## Release distribution not performed

No git remote exists. Nothing has been pushed, merged, tagged, or published. The
`v0.1.4` tag does not exist (existing tags: `v0.1.0`, `v0.1.1`, `v0.1.2`).

## Testing still outstanding

- Long-duration stability / soak run (the WUI-009 extended audit was intentionally
  stopped before its long-duration phase).
- Clean-machine / clean-VM install and launch.
- Upgrade-path test preserving settings and history from a prior installed version.
- Uninstall test (file removal and user-data retention policy).

---

# 2. DEFERRED WORK

Intentionally postponed by the user. See `handoff/NEXT_TASKS.md` for ordering.

| Item | State |
|---|---|
| GitHub repository creation and configuration | **DEFERRED** — no remote configured |
| Git remote (`origin`) | **DEFERRED** — none exists |
| `git push` | **DEFERRED** |
| GitHub Release / online publication | **DEFERRED** |
| Code signing (real public CA, RSA) | **DEFERRED** — build is unsigned |
| GitHub update checker | **DEFERRED until GitHub publication** — no URL exists to point at |
| Clean-machine installer validation | **DEFERRED** |
| Long soak run | **DEFERRED** |
| Merge to `master`, `v0.1.4` tag | **DEFERRED** |

> **GitHub Update Checker: DO NOT IMPLEMENT until the GitHub repository and release
> URL are established.** There is no owner, repository, or release URL. Do not
> invent one, and do not ship an updater with a guessed endpoint.

## Environment limitations on this machine

- `C:\Program Files\dotnet\dotnet.exe` has **no SDK**. Use
  `C:\Users\ali\.dotnet\dotnet.exe` (SDK 8.0.425). See `BUILD_AND_TEST.md`.
- `iscc` (Inno Setup 6) is **not on PATH**; it lives at
  `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe`.
- Windows PowerShell 5.1 is the default shell. See the caveat list in
  `BUILD_AND_TEST.md` before scripting anything.

---

# 3. IMPORTANT FIXED TRAPS

These bugs **are fixed**. They are listed because each one was expensive, is easy to
reintroduce, and the failure mode is subtle. **Do not report them as active bugs**
and do not "fix" them again.

## PRI must be explicitly included in publish

- MRT Core writes `TrafficLens.WinUI.pri` straight to `$(TargetDir)` and only
  registers it as publishable when `AppxPackage == true` (MSIX). This app is
  **unpackaged**, so the PRI silently never entered `ResolvedFileToPublish`.
- The compiled XBF lives **only** in that PRI — the managed assembly embeds zero
  XBF. The result: the app started, set the culture, then died with
  `XamlParseException: XAML parsing failed` in `MainWindow.InitializeComponent()`.
- Every other gate stayed green: build, 571 tests, publish, packaging, install,
  checksums. **Only a real launch exposed it.**
- Fixed by the `IncludeProjectPriFileInPublish` target in
  `TrafficLens.WinUI.csproj` (`AfterTargets=ComputeResolvedFilesToPublishList`),
  using the supported MSBuild publish mechanism — not a copy of `bin`.
- **Trap:** if you change publish settings, re-verify the PRI is in the output.

## Publish validates the PRI, and the check must stay

`scripts\build-release.ps1` fails the run **before** ZIP/installer creation when
`TrafficLens.WinUI.pri` is missing, empty, under 256 KB, or not a PRI container. MRT
emits `mrm_pri2`, so the header is checked as `mrm_` (legacy `PRIC` also accepted)
rather than a magic this toolchain never produces.
- **Trap:** the `0x800711C7` "Smart App Control blocked it" conclusion recorded for
  the first RC was **wrong**. That RC launched fine and wrote no Code Integrity
  event; the crash was purely the missing PRI. Do not attribute code-signing
  policy failures to this bug.

## `AppWindow.TitleBar` caption insets are not trustworthy

`AppWindow.TitleBar.LeftInset` / `RightInset` have been observed on this host to:

- report **nothing at all** for a window that still draws Minimize/Maximize/Close, and
- report a value that reserves only the **resize frame**, far narrower than the
  actual controls.

Either way the title ends up underneath the caption buttons. `DWMWA_CAPTION_BUTTON_BOUNDS`
was tried and **rejected**: it returns `S_OK` with a zero-width rectangle in both the
restored and maximized state.
- **Fix in place:** `Infrastructure\NativeCaptionButtons.cs` reads the real caption
  region off the live window with a bounded `WM_NCHITTEST` probe
  (`HTMINBUTTON`/`HTMAXBUTTON`/`HTCLOSE`), and `TitleBarCaptionLayout.Resolve` still
  prefers the shell insets whenever they already cover the measured controls.
- The probe is bounded on both axes: each edge is searched over the resize-frame
  thickness only, then each boundary is found by halving. It reports the **physical**
  side, so no per-language handling is needed and no value is hardcoded.
- **Trap:** the compositor publishes caption zones **one frame after** a resize, so a
  probe taken from `AppWindow.Changed` sees the previous frame. The re-check is taken
  once on the next rendered frame via a **self-removing**
  `CompositionTarget.Rendering` handler, capped at 3 attempts. There is deliberately
  no timer, poller, worker, or positioning loop — keep it that way.

## In RTL, flow direction ≠ physical side

The shell reports caption insets in **flow** order. On a 150% DPI Persian window it
reported a non-zero `LeftInset` (207 px) with `RightInset` 0, while the caption
buttons sat on the **physical right** (x 796..1003). Reading insets as physical
sides put the safe area on the wrong edge and ran the title under the buttons.
- `TitleBarCaptionLayout.ResolvePadding` takes insets in flow order **plus the layout
  direction** and returns physical left/right padding; the call site passes
  `RootGrid.FlowDirection == FlowDirection.RightToLeft`.
- **Trap:** the same trap applies to anything else anchored to a window edge. The
  widget quick action is anchored to the **physical left** and must **not** mirror to
  the right in Persian.

## The widget quick action is physically anchored, and there is exactly one

- The shell widget quick action holds a constant **29 physical px** from the window's
  left edge (12 DIP shell margin) at 150% DPI, in every state: resizing, maximized,
  restored, navigation pane collapsed or expanded, and both languages.
- It is applied as a **margin on the caption side of the title**, and the title bar
  grid keeps **no horizontal padding**. An earlier version applied the caption safe
  area as padding on the whole grid, so the reserve landed on the physical left in
  RTL and pushed the control inwards.
- There is **one** widget enable control. The duplicate quick card was **deleted
  outright** from the Settings page (card, `WidgetToggleToggle`, its `Toggled` handler
  and two code-behind labels — not collapsed). The remaining Settings switch and the
  shell quick action are **views of one** `FloatingWidgetEnabled` value owned by
  `WidgetEnabledState`, written only through `IFloatingWidgetService.SetEnabled`.
  `WidgetToggleSync.Apply` existed only to push a value into that *pair* and was
  removed; `WidgetToggleSync.Resolve` is still shared.
- **Trap:** do not reintroduce a second toggle, a second setting, or a parallel
  service. No localization key was removed — `FloatingWidgetLabel` /
  `EnableFloatingWidgetLabel` are still used by the shell control, the tray and the
  WPF views.

## Closing the widget must go through the service

The widget window used to cancel its own close and hide itself directly, bypassing
the service, so the enabled flag was never persisted and the widget returned on the
next start. The native close button now raises `UserCloseRequested`, which the
service turns into the same `SetEnabled(false)` path the Settings switch uses.
Closing the widget must never affect the main window, tray or monitoring.

## No duplicate network polling pipeline

There is **one** network collector, **one** connection provider, **one** ETW process
collector, and one history accumulator, each started once from `App.xaml.cs`. Do not
add a second timer or poller to "refresh faster", and do not create a parallel
collection path for the widget or the tray. Reuse the existing services and events.
- This also applies to graph and window layout: prefer a self-removing event handler
  over a repeating timer.

## ETW requires Administrator

Enabling the ETW kernel network provider needs an elevated process. Without it the
collector logs `permission denied` and reports a degraded status. This is **expected,
non-fatal, and documented** — do not escalate privileges to work around it.

## Tray exit is explicit and single-owner

Closing the main window hides to the tray; that is **not** exiting. `ApplicationExitCoordinator`
is the single owner of process exit, and the tray exposes an explicit **Exit** item.
Do not add a second exit path, and do not make window-close terminate the process.

## History stores real byte deltas

History is accumulated from **actual byte deltas between samples**, never from a
cumulative counter, so a counter reset cannot produce a bogus spike. Rate
calculation (`NetworkSpeedCalculator`, `SpeedRateTracker`) and history bucketing
(`TrafficHistoryAccumulator`, `HourlyHistoryBuilder`) are unit-tested — extend those
tests rather than reimplementing the math in a ViewModel.

## Other resolved defects worth not re-breaking

- `SelectWorkArea` let a zero-overlap work area override a positive-overlap one, so a
  widget near a right screen edge would jump to the next monitor.
- `RestoreIfEnabled` reloaded state then called the idempotent `SetEnabled(true)`, so
  an enabled widget was never recreated on start-up.
- The widget drag delta mixed DIPs with physical pixels.
- `AppWindowChangedEventArgs` has **no** `DidLayoutChange`, so caption layout must
  react to size/position changes instead.
- The widget could be dragged off-screen; it is now clamped to the real DPI-scaled
  monitor work area, with nearest-work-area recovery for a disconnected monitor.
- The release signing set omitted the shipped `fa-IR\TrafficLens.WinUI.resources.dll`.
  A partially signed app is treated as untrusted, so the release would have been
  unlaunchable even though every signing call succeeded. The set is now discovered and
  re-verified afterwards. **Trap:** `Get-ChildItem -Recurse -Include` silently ignores
  `-Include`, which is what hid the file.
