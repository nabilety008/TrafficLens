# TrafficLens — WinUI 3 / Windows App SDK Migration Map

Updated: 2026-09-23  
Branch: `feature/winui3-migration`  
Safe WPF rollback commit: `f9017f0`  
Status: **WUI-001 in progress** — foundation + application shell only

## Goals and non-goals

Goals for this migration track:

- Replace the WPF presentation layer with WinUI 3 (Windows App SDK) while preserving monitoring logic.
- Keep `TrafficLens.Core`, `TrafficLens.Network`, and `TrafficLens.Infrastructure` as the shared non-UI stack.
- Keep the existing WPF app (`src/TrafficLens.App`) intact as the rollback path.
- Move milestone by milestone (WUI-001 … WUI-010) with human GUI verification between milestones.

Non-goals (until explicitly scheduled):

- Merging to `master`, tagging, publishing, GitHub Releases, or updating the GitHub Update Checker.
- Overwriting the verified v0.1.3 installer / portable artifacts under `artifacts/`.
- Deleting or rewriting the WPF application.
- Reimplementing collectors, storage, or alerting logic in the UI project.

## Windows App SDK version choice

| Item | Value |
|------|--------|
| Package | `Microsoft.WindowsAppSDK` |
| Selected version | **2.5.1** (stable) |
| Released | 2026-09-16 |
| NuGet | https://www.nuget.org/packages/Microsoft.WindowsAppSDK/2.5.1 |
| Project TFM | `net8.0-windows10.0.19041.0` |
| Platform | `x64` / `win-x64` |
| Packaging | Unpackaged (`WindowsPackageType=None`) |
| Runtime | Self-contained Windows App SDK (`WindowsAppSDKSelfContained=true`) |

Rationale:

1. **2.5.1 is the current stable** on NuGet (newer than 2.4.0 / 2.3.1; not experimental/preview).
2. Matches Microsoft’s current Windows App SDK 2.x line (AI/WinUI/Runtime package split) while staying on the .NET 8 SDK already used by TrafficLens (`C:\dotnet\dotnet.exe`, SDK 8.0.425).
3. **Self-contained** WASDK avoids requiring a separate Windows App Runtime install on the verification machine and keeps the shell launchable for offline GUI checks.
4. Unpackaged mode matches the existing WPF distribution model (no MSIX requirement for the v0-style portable/installer flow later).

References consulted: Windows App SDK overview and downloads (learn.microsoft.com), NuGet `Microsoft.WindowsAppSDK` package page.

## New project layout

```
src/TrafficLens.WinUI/          ← new (this milestone)
  TrafficLens.WinUI.csproj
  App.xaml / App.xaml.cs        composition root (DI, culture, launch)
  MainWindow.xaml(.cs)          TitleBar + NavigationView shell
  app.manifest                  DPI / asInvoker (aligned with WPF)
  LocalizationService.cs        ILocalizationService over linked resx
  Pages/
    DashboardPage, ApplicationsPage, ConnectionsPage,
    HistoryPage, AlertsPage, SettingsPage, AboutPage
  Resources/
    Strings.resx / Strings.fa-IR.resx   ← linked from TrafficLens.App
```

Solution membership: project is added to `TrafficLens.sln` under the existing `src` folder. Solution `Any CPU` maps to project platform **x64** for this project only.

## Component mapping (WPF → WinUI 3)

Difficulty: L = low, M = medium, H = high.  
Risk: feedback or data path that can regress monitoring behavior if mishandled.

| WPF component | WinUI 3 equivalent | Difficulty | Risk | Verification |
|---|---|---|---|---|
| `App.xaml` / `App.OnStartup` | `App.xaml` / `OnLaunched` | L | M | Process starts; DI graph builds |
| `MainWindow` chrome (custom title bar row) | `Window` + `ExtendsContentIntoTitleBar` + `SetTitleBar` | L | L | Caption buttons drag; title visible |
| Button-row nav in `MainWindow` | `NavigationView` + `NavigationViewItem` + `Frame` | L | M | All 7 destinations navigate |
| Dashboard content in `MainWindow` | `DashboardPage` | M | H | Cards/graph bound to live samples |
| `Views/ApplicationsView` | `Pages/ApplicationsPage` | M | H | Process list + sort/search |
| `Views/ConnectionsView` | `Pages/ConnectionsPage` | M | H | Connection table + filters |
| `Views/HistoryView` + `HistoryBarChartControl` | `Pages/HistoryPage` + WinUI graph control | H | H | Range buttons, EN/FA titles, bar balance |
| `Views/AlertsView` | `Pages/AlertsPage` | M | M | Configured + triggered sections |
| `Views/SettingsView` | `Pages/SettingsPage` | M | M | Language, tray, alerts, paths |
| `Views/AboutView` | `Pages/AboutPage` | L | L | Version, diagnostics, icon |
| `Views/OnboardingView` | Onboarding `ContentDialog` or flyout | M | M | First-run flow; Escape dismiss |
| `Themes/DarkTheme.xaml` brushes | WinUI theme resources / `ResourceDictionary` | M | M | Dark default; no white flash |
| Custom dark `ComboBox` template | WinUI `ComboBox` styles (drop custom template) | L | L | No navigation crash class of bug |
| Implicit WPF `ContextMenu` styles | WinUI `MenuFlyout` / `MenuFlyoutItem` | L | L | Flyouts readable on dark bg |
| `Popup` options tray menu | `Flyout` / `TeachingTip` / `MenuFlyout` | L | L | Minimize/close-to-tray toggles |
| `WrapPanel` nav | `NavigationView` pane | L | L | Selection highlight |
| `TrafficGraphControl` (`OnRender`) | Win2D `CanvasControl` or `Shapes`/`Canvas` | H | H | Live series, ranges, RTL labels |
| `IFloatingWidgetService` + `FloatingWidgetWindow` | Second `Window` (WinUI) or HWND interop | H | H | Toggle, position, suspend/resume |
| `ISystemTrayService` / WinForms NotifyIcon | WinUI has no tray API → keep WinForms/HWND host or Windows App SDK notify APIs | H | H | Show/hide, alerts, exit |
| `ApplicationExitCoordinator` | Same coordinator; hook `Window.Closed` | M | H | No orphan ETW/processes |
| `SingleInstanceGuard` | Unchanged (Infrastructure/App helper) | L | M | Second launch activates first |
| `LocalizationService` + resx | Same pattern; `ResourceManager` on linked resx | L | M | en-US ↔ fa-IR runtime switch |
| RTL (`FlowDirection`) | `Window.FlowDirection` from `ILocalizationService.IsRightToLeft` | L | M | Nav/content flip; IP/rate LTR |
| `ILocalizationService` in Core | **Unchanged** | L | L | Interface consumers compile |
| ViewModels (MVVM) | Keep; bind in WinUI (x:Bind or Binding) | M | M | Existing App tests still target WPF project |
| `Infrastructure` / `Network` services | **Unchanged** (project references) | L | H | Collector tests unchanged |
| `app.manifest` DPI | Same manifest on WinUI csproj | L | M | Sharp text, PerMonitorV2 |
| Branding icon/PNG | `ApplicationIcon` + content assets | L | L | Taskbar/About icon |
| Inno Setup packaging | Later milestone (WUI-010) | H | H | New installer **without** replacing v0.1.3 artifacts |

## Components with no direct WinUI equivalent

| Component | Approach |
|---|---|
| `System.Windows.Forms.NotifyIcon` tray | Host WinForms control in HWND, or dedicated HWND message window; keep behavior in `ISystemTrayService` |
| WPF `OnRender` graph controls | Rewrite drawing with Win2D (`Microsoft.Graphics.Canvas`) or retained-mode `Canvas` |
| WPF `Popup` + `AllowsTransparency` | WinUI `Flyout` / `Window` with `SystemBackdrop` |
| WPF `BooleanToVisibilityConverter` | `x:Bind` `Visibility` helpers or keep converter |
| WPF `KeyBinding` Escape → command | `KeyboardAccelerator` or `KeyDown` on shell |
| MSIX-only WinUI niceties | Stay unpackaged; enable only if packaging milestone requires it |

## Milestone plan

| ID | Scope | Exit criteria |
|----|--------|----------------|
| **WUI-001** | Project foundation + shell (TitleBar, NavigationView, 7 destinations, theme, DI, localization) | Builds 0/0; launches; human verifies chrome + nav + EN/FA/RTL |
| WUI-002 | Dashboard page + live bindings | Live rates/cards match WPF behavior |
| WUI-003 | Applications page | Process table parity |
| WUI-004 | Connections page | Connection table parity |
| WUI-005 | History page + graph control | Ranges + hourly bars + localization |
| WUI-006 | Alerts + Settings + About + onboarding | Full settings/alerts parity |
| WUI-007 | Floating widget window | Toggle/position/suspend parity |
| WUI-008 | System tray + exit coordination + single instance | Tray/exit/no-orphan parity |
| WUI-009 | Full regression (tests + dual-UI coexistence) | Automated tests green; side-by-side checklist |
| WUI-010 | Packaging decision (separate installer name/version) | Does not overwrite v0.1.3 artifacts |

## Rules for every WUI milestone

1. Work only on `feature/winui3-migration` (or a child branch); never rewrite `f9017f0`.
2. Do not modify WPF behavior except for shared Core/Infrastructure bugfixes required by both UIs.
3. No hardcoded user-visible strings; use `ILocalizationService` / linked resx.
4. Technical values (IPs, ports, rates) stay LTR inside RTL UI.
5. Build and test before reporting; record warnings/errors honestly.
6. Stop for human GUI verification before the next WUI milestone.
7. Do not merge, tag, or publish until explicitly approved.

## WUI-001 verification checklist (human)

- [ ] Window opens with custom title bar and `NavigationView` pane
- [ ] Destinations: Dashboard, Applications, Connections, History, Alerts, Settings, About
- [ ] Default theme is dark; no glaring white surfaces
- [ ] English ↔ فارسی switch updates labels; RTL flips layout
- [ ] Window minimize/maximize/close work; resizing respected
- [ ] No crash on repeated navigation between all destinations
- [ ] Process remains single-instance-safe enough for shell smoke test (WUI-008 hardens this)
