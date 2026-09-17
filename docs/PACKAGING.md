# TrafficLens — Packaging (TL-015)

## Supported platform

- **OS:** Windows 10 / Windows 11.
- **Architecture:** x64 only (`win-x64`). x86/ARM packages are explicitly out
  of scope for TL-015 and an installer targeting them is not produced.
- **Runtime:** .NET 8 WPF. The install is **self-contained**: the end user
  does **not** need to install the .NET 8 runtime separately.

## Publish model decisions

| Topic | Decision | Reason |
|---|---|---|
| Distribution | **Self-contained `win-x64`** | End-user has no .NET requirement; reliability over size. |
| Single file | **`PublishSingleFile=true`** with `IncludeNativeLibrariesForSelfExtract=true` | Verified safe for WPF, DI, resx/satellite assemblies, SQLite (managed `Microsoft.Data.Sqlite` + bundled native `e_sqlite3`), TraceEvent/ETW and the WinForms `NotifyIcon`; the single `TrafficLens.exe` bundles managed + native + satellite payloads. Native libraries needed at runtime are extracted to a temp dir automatically by the single-file host. |
| Trimming | **`PublishTrimmed=false`** | Aggressive trimming is not proven safe for WPF reflection, DI container, resource lookup, `Microsoft.Data.Sqlite`, or `TraceEvent`. Reliability wins; the ~few-MB size delta is accepted. |
| NativeAOT | **Not used** | WPF does not support NativeAOT. |
| Globalization | **`InvariantGlobalization=false`** | Persian / fa-IR and culture-aware formatting must keep ICU for correct decimal separators and RTL-relevant culture data. |

## Installer technology

- **Inno Setup 6** (`packaging/TrafficLens.iss`).
- MSIX and WiX are intentionally not used (no proven requirement; Inno is
  simpler and fully sufficient for a per-user WPF desktop app).

## Install scope

- **Per-user install** — `PrivilegesRequired=lowest`, **no Administrator
  required**.
- Default location: `%LOCALAPPDATA%\Programs\TrafficLens`.
- This aligns with the existing per-user state:
  - settings: `%LOCALAPPDATA%\TrafficLens\settings.json`
  - history DB: `%LOCALAPPDATA%\TrafficLens\data\trafficlens.db`
  - logs: `%LOCALAPPDATA%\TrafficLens\logs\`
  - startup registration: HKCU `...\CurrentVersion\Run` value `TrafficLens`.

## Build prerequisites

- .NET 8 SDK (any recent 8.0.x; the build script locates it via PATH or the
  common install paths and verifies the major version).
- Inno Setup 6 (`ISCC.exe` on PATH or installed at
  `C:\Program Files (x86)\Inno Setup 6\`). Required only for the installer;
  pass `-SkipInstaller` to skip it.
- No Visual Studio / no GUI tools required.

## Release command

```powershell
git clone <repo>
cd TrafficLens
.\scripts\build-release.ps1 -Version 0.1.1
```

- `-Version` defaults to the `Version` in `Directory.Build.props` if omitted.
- The script fails fast and non-zero on any of: SDK missing, restore/build
  failure, build warnings, test failure, publish failure, missing/undersized
  `TrafficLens.exe`, missing expected publish files, Inno Setup failure, or
  missing installer artifact. It never reports success after a partial
  failure.

## Output & artifact naming

```
artifacts/
  publish/win-x64/                                   self-contained publish
    TrafficLens.exe
    TrafficLens.dll
    TrafficLens.Core.dll
    TrafficLens.Network.dll
    TrafficLens.Infrastructure.dll
    fa-IR/TrafficLens.App.resources.dll              (Persian satellite)
    ...
  installer/
    TrafficLens-Setup-0.1.1-win-x64.exe
    TrafficLens-Setup-0.1.1-win-x64.exe.sha256
  portable/
    TrafficLens-Portable-0.1.1-win-x64.zip
```

`artifacts/` is gitignored; only the source scripts and installer definition
are kept in Git.

## Versioning

- Centralised in `Directory.Build.props`:
  `Version`, `AssemblyVersion`, `FileVersion`, `InformationalVersion`
  (currently `0.1.1`).
- The release script overrides them for the publish (`-p:Version=…`).
- Stable `AppId` GUID in `TrafficLens.iss` keeps one uninstall entry and
  upgrade-aware paths across versions.
- The installer version (`AppVersion` define) matches the product version.

## Installer behavior

- Start Menu shortcut (required) + optional Desktop shortcut (installer task,
  off by default).
- Shortcuts point at the installed exe, working directory = install dir, and
  do **not** pass `--minimized` (manual launch opens the normal UI).
- No bundled/third-party software, no elevation, no EULA wall.
- Uninstall entry registered under Windows (Apps & Features).

## Running app during upgrade

- The installer detects a running `TrafficLens.exe` (WMI process scan) and
  displays a notice asking the user to close it. It **never** force-kills the
  process. Inno's built-in "file in use" dialog is the second safety net, so
  live binaries are never partially replaced. Because the app exits gracefully
  through its coordinator, no orphaned ETW session is left behind.

## Upgrade behavior

- Installing over an existing version:
  - binaries are replaced (same `{app}`);
  - `UsePreviousAppDir=yes` reuses the prior directory;
  - settings, history and logs in `%LOCALAPPDATA%\TrafficLens` are untouched;
  - the Start Menu shortcut is replaced, not duplicated;
  - the single uninstall entry is reused (stable AppId);
  - the HKCU Run value `TrafficLens` is **not** modified by the installer;
    if it pointed at the same `%LOCALAPPDATA%\Programs\TrafficLens\TrafficLens.exe`
    the path stays valid automatically. If the install location changed,
    StartupRegistrationService rewrites the value from
    `Environment.ProcessPath` the next time the user saves the setting —
    the app self-corrects without creating a duplicate Run value.
- Start-with-Windows is **never** auto-enabled by installation and is not
  changed across upgrades (it remains app-controlled in Settings).

## Uninstall & user-data safety

- Normal uninstall removes **program files only**:
  - install directory contents + shortcuts + Start Menu entry + uninstall key;
  - `Type: dirifempty` cleanup of the now-empty `{app}` folder.
- **User data is preserved by default** and the installer contains no
  `[UninstallDelete]` entry touching `%LOCALAPPDATA%\TrafficLens` and shows no
  "delete my data" task — nothing silently removes history or settings.

### Removing retained user data manually

Delete the whole app-data folder (while the app is closed):

```powershell
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\TrafficLens"
```

This removes `settings.json`, `data\trafficlens.db` (and WAL), `logs\`, and
any tray/alerts state. (Desktop/Start-menu shortcuts and the HKCU Run value
`TrafficLens` can be removed from Settings or manually if a full cleanup is
wanted; the installer does not own them beyond the shortcuts it creates.)

## Startup behavior

- Startup registration is exclusively app-owned (TL-013,
  `StartupRegistrationService`, HKCU Run value `TrafficLens`).
- The installer creates no parallel autostart mechanism.
- `--minimized` semantics are preserved: with Start-with-Windows enabled the
  registered command may be
  `"…\Programs\TrafficLens\TrafficLens.exe" --minimized`.

## Security / trust

- **Unsigned development build** — no code-signing certificate exists in this
  repository; installer and executable are unsigned.
- No SmartScreen bypass is attempted and no Windows security settings are
  weakened. Real code signing is a future release-checklist item.

## Checksum

- The release script writes `TrafficLens-Setup-<ver>-win-x64.exe.sha256`
  (SHA-256 of the installer) automatically when the installer builds.

## Localization

- The publish includes the Persian satellite
  `fa-IR/TrafficLens.App.resources.dll`; both en-US and fa-IR work from the
  installed (self-contained single-file) build, and the installer leaves
  `settings.json` untouched so the user's saved language survives install/
  upgrade/uninstall/reinstall.
- The installer UI itself is English-only for TL-015 (application localization
  is mandatory and shipped; installer localization is deferred).

## Native dependencies

- **SQLite** — `Microsoft.Data.Sqlite` (managed) + bundled native
  `e_sqlite3.dll`; self-extracted by the single-file host.
- **TraceEvent / ETW** — `Microsoft.Diagnostics.Tracing.TraceEvent` ships its
  own management/native helpers; resolved from the publish output, never the
  dev NuGet cache.
- **WinForms NotifyIcon** — `Microsoft.WindowsDesktop.App.WindowsForms`
  framework reference, included in self-contained output.
- **Windows IP Helper / kernel table P/Invokes** — system DLLs
  (`iphlpapi`, `Ntdll`), always present on Windows 10/11 x64.
- The launch test is performed from the isolated publish directory, so
  resolution is verified against the shipped payload only.

## Clean-machine coverage (limitation)

- **No disposable clean Windows VM is available in this environment.** The
  closest approximation is: launch from the isolated publish directory (out of
  the repo working tree), verify DLL/native resolution, resource satellites,
  `%LOCALAPPDATA%\TrafficLens` creation, SQLite DB creation/use, and run the
  install → launch → use → uninstall → reinstall lifecycle through the real
  installer (which installs into a fresh `{app}`). True from-scratch clean-VM
  verification should be performed on the first real release pipeline.

## Branding / icon pipeline (implemented in TL-016 continuation)

- A single replaceable brand asset set lives under `assets\branding\` and is
  generated by `scripts\generate-icons.ps1` (multi-size ICO + PNGs). The glyph
  mirrors the runtime-drawn tray icon. See `docs/BRANDING.md` for details.
- Wiring now in place:
  - EXE icon: `TrafficLens.App.csproj` → `<ApplicationIcon>`.
  - WPF resource: same ICO is a `<Resource>` so windows/tray can load it via
    `pack://application:,,,/TrafficLens.ico`.
  - taskbar/window icon: `Window.Icon` on `MainWindow` (and the floating widget
    title row shows a 14px brand glyph).
  - tray icon: `SystemTrayService` prefers the embedded brand ICO and falls back
    to the runtime-drawn glyph if the resource is unavailable.
  - installer icon: `SetupIconFile={#BrandIcon}` resolves to
    `assets\branding\TrafficLens.ico`; `[Setup]` also carries product metadata
    (`VersionInfoCompany`/`Description`/`ProductName`/`ProductVersion`).
  - Start Menu / Desktop shortcut icons inherit the exe icon.
- Replacing branding later only requires re-running `scripts\generate-icons.ps1`
  (or swapping the files in `assets\branding\`) — no `[Setup]` structural changes
  and no application wiring changes are required.
- Known follow-up: the installer is English-only (`[Languages]`). A Persian
  installer language file (Inno `Persian.isl`) is a separate localization task;
  the project URLs in `[Setup]` remain placeholders.

## Future branding/icon replacement point

- The placeholder icon (`packaging/placeholder.ico`) has been removed; the
  installer now points at the shared brand asset. Any final marketing artwork
  is dropped into `assets\branding\` (or the generator is updated) and the
  pipeline re-run. See the section above and `docs/BRANDING.md`.
- No `[Setup]` structural changes are required to swap artwork.