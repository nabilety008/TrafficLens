# TrafficLens — Packaging (TL-015)

## Supported platform

- **OS:** Windows 10 / Windows 11.
- **Architecture:** x64 only (`win-x64`). x86/ARM packages are explicitly out
  of scope for TL-015 and an installer targeting them is not produced.
- **Runtime:** .NET 8 + WinUI 3 (Windows App SDK 2.5.1). The install is
  **self-contained**: the end user does **not** need to install the .NET 8
  runtime or the Windows App Runtime separately.
- **Entry point:** `TrafficLens.WinUI.exe` (unpackaged WinUI 3) since v0.1.4.
  The WPF project `src/TrafficLens.App` stays in the repository as
  rollback/reference and is never published.

## Publish model decisions

| Topic | Decision | Reason |
|---|---|---|
| Distribution | **Self-contained `win-x64`** | End-user has no .NET or Windows App Runtime requirement; reliability over size. |
| Windows App SDK | **`WindowsAppSDKSelfContained=true`** (unpackaged) | The runtime payload ships next to the executable, so the app starts on a clean machine with no Windows App Runtime installed and no MSIX deployment. |
| Single file | **`PublishSingleFile=false`** | A self-contained Windows App SDK app must keep the **loose** layout: the bootstrap and runtime payload (`Microsoft.WindowsAppRuntime.*`, `Microsoft.ui.xaml.dll`, the x64 native bridge) have to sit next to the executable. Single-file bundling is not used for WPF any more. |
| Trimming | **`PublishTrimmed=false`** | Aggressive trimming is not proven safe for WinUI/WinAppSDK reflection, the DI container, resource lookup, `Microsoft.Data.Sqlite`, or `TraceEvent`. Reliability wins; the size delta is accepted. |
| NativeAOT | **Not used** | WinUI 3 does not support NativeAOT. |
| ReadyToRun | **`PublishReadyToRun=false`** | Keeps the build deterministic and the artifact smaller; startup cost is not a release blocker. |
| Debug symbols | **`DebugType=none`**, no PDBs in the package | Release artifacts do not ship debug symbols. |
| Platform | **`-p:Platform=x64`** for build, test and publish | The project declares `<Platforms>x64</Platforms>` and the Windows App SDK self-contained targets fail under AnyCPU (`WindowsAppSDKSelfContained requires a supported Windows architecture`). It also keeps the native payload x64-only. |
| Globalization | **`InvariantGlobalization=false`** | Persian / fa-IR and culture-aware formatting must keep ICU for correct decimal separators and RTL-relevant culture data. |
| Installer | **Inno Setup 6, unpackaged EXE** | No MSIX: the app stays a normal per-user desktop install with a stable `AppId`. |

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
.\scripts\build-release.ps1 -Version 0.1.4
```

- `-Version` defaults to the `Version` in `Directory.Build.props` if omitted.
- `-SkipInstaller` / `-SkipPortable` skip the respective artifact.
- `-ResumeFromPublish` reuses the **existing** `artifacts\publish\win-x64` output
  of a previous run and only rebuilds the ZIP, the installer and the checksums.
  The publish tree is re-validated first, so an incomplete or stale tree still
  fails. Use it for re-packaging when build and tests already passed and must
  not be repeated.
- Signing is opt-in and never the default (see *Security / trust*).
- The script fails fast and non-zero on any of: SDK missing, restore/build
  failure, build warnings, test failure, publish failure, missing/undersized
  `TrafficLens.WinUI.exe`, a WPF `TrafficLens.exe` present in the output, a
  missing Windows App SDK runtime payload, PDBs or test assemblies in the
  output, missing branding assets, missing localization, wrong version
  metadata, Inno Setup failure, or a missing installer artifact. It never
  reports success after a partial failure.

## Output & artifact naming

```
artifacts/
  publish/win-x64/                                   self-contained WinUI publish
    TrafficLens.WinUI.exe
    TrafficLens.WinUI.dll
    TrafficLens.Core.dll
    TrafficLens.Network.dll
    TrafficLens.Infrastructure.dll
    TrafficLens.WinUI.Tray.dll
    Microsoft.WindowsAppRuntime.dll                  (self-contained WASDK)
    Microsoft.WindowsAppRuntime.Bootstrap.dll
    Microsoft.ui.xaml.dll
    amd64/                                           (x64 native bridge)
    Assets/TrafficLens.ico
    Assets/TrafficLens-256.png
    fa-IR/TrafficLens.WinUI.resources.dll            (Persian satellite)
    ...
  installer/
    TrafficLens-Setup-0.1.4-win-x64.exe
    TrafficLens-Setup-0.1.4-win-x64.exe.sha256
  portable/
    TrafficLens-Portable-0.1.4-win-x64.zip
    TrafficLens-Portable-0.1.4-win-x64.zip.sha256
```

`artifacts/` is gitignored; only the source scripts and installer definition
are kept in Git. The portable ZIP is written entry-by-entry with spec-compliant
**forward-slash** entry names (`Compress-Archive` on Windows PowerShell 5.1
writes backslashes, which breaks `unzip` on Linux/macOS) and with
`TrafficLens.WinUI.exe` at the archive root.

## Versioning

- Centralised in `Directory.Build.props`:
  `Version`, `AssemblyVersion`, `FileVersion`, `InformationalVersion`
  (currently `0.1.4` / `0.1.4.0`).
- The release script overrides them for the publish: `Version` and
  `InformationalVersion` stay 3-part (`0.1.4`), `AssemblyVersion` and
  `FileVersion` are 4-part (`0.1.4.0`).
- The About page resolves the shown version from
  `AssemblyInformationalVersion`, so no source change is needed per release.
- Stable `AppId` GUID in `TrafficLens.iss` keeps one uninstall entry and
  upgrade-aware paths across versions.
- The installer version (`AppVersion` define) matches the product version.
- The release entry point is not hard-coded: the script reads `<AssemblyName>`
  from `TrafficLens.WinUI.csproj`, verifies `TrafficLens.WinUI.exe` exists, and
  passes it to Inno Setup as `/DAppExeName`. Use `{#AppExeName}` directly in
  the `.iss` — ISPP does not expand a constant nested inside another `#define`.

## Installer behavior

- Start Menu shortcut (required) + optional Desktop shortcut (installer task,
  off by default).
- Shortcuts point at the installed exe, working directory = install dir, and
  do **not** pass `--minimized` (manual launch opens the normal UI).
- No bundled/third-party software, no elevation, no EULA wall.
- Uninstall entry registered under Windows (Apps & Features).

## Running app during upgrade

- The installer detects a running `TrafficLens.WinUI.exe` (WMI process scan)
  and displays a notice asking the user to close it. It **never** force-kills the
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
  - `[InstallDelete]` removes a stale WPF `TrafficLens.exe` left by a pre-0.1.4
    install, so the old entry point cannot survive an upgrade;
  - the HKCU Run value `TrafficLens` is **not** modified by the installer;
    if it pointed at the same
    `%LOCALAPPDATA%\Programs\TrafficLens\TrafficLens.exe` the path stays valid
    automatically. If the install location or entry point changed (for example
    the WPF → WinUI upgrade), StartupRegistrationService rewrites the value
    from `Environment.ProcessPath` the next time the user saves the setting —
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

- **Unsigned release candidate** — no code-signing certificate exists in this
  repository, so installer and executable are unsigned
  (`Get-AuthenticodeSignature` → `NotSigned`).
- The pipeline is **signing-ready**: `-Sign -CertificateThumbprint <thumbprint>
  -TimestampUrl <rfc3161-tsa-url>` resolves the certificate from the Windows
  certificate store (`CurrentUser\My`, then `LocalMachine\My`, passing `/sm` for
  the machine store), refuses a certificate without a private key or an expired
  one, and signs in the required order —
  publish → product binaries → ZIP/installer → installer → SHA-256. No
  thumbprint, password or key is ever stored in the repository, and SHA-256
  checksums are always computed **after** signing.
- **The certificate must be RSA.** Smart App Control's signature check accepts
  RSA certificates only and does not support elliptic-curve (ECC) signatures, so
  the pipeline rejects an ECC certificate up front rather than emitting a
  correctly signed build that still cannot launch on a protected device.
- **The sign set is discovered, not hard-coded.** Every TrafficLens-owned PE
  image in the publish output is signed, which is currently 7 files:
  `TrafficLens.WinUI.exe` (native apphost), `TrafficLens.WinUI.dll`,
  `TrafficLens.Core.dll`, `TrafficLens.Network.dll`,
  `TrafficLens.Infrastructure.dll`, `TrafficLens.WinUI.Tray.dll`, and the
  Persian satellite `fa-IR/TrafficLens.WinUI.resources.dll`. Discovery exists
  because a *partially* signed application is treated as untrusted: omitting
  the satellite would leave the release unlaunchable even though every signing
  call succeeded. Microsoft/.NET/Windows App SDK binaries are never re-signed.
  After signing, the whole set is re-verified (`Valid` **and** a present RFC 3161
  timestamp countersignature) so a partial signature fails the release.
- **Smart App Control** is ON on the release machine (policy
  `{0283ac0f-fff1-49ae-ada1-8a933130cad6}`, `VerifiedAndReputablePolicyState=1`,
  no enterprise-authored policy). It was **not** modified, bypassed or weakened,
  and no SmartScreen override was used.
- **Correction: Smart App Control did not block this release.** An earlier
  revision of this document reported `0x800711C7` for the v0.1.4 RC. That was
  wrong. The single launch of the broken RC started normally and wrote **no**
  Code Integrity event; the application crashed for an unrelated packaging
  reason (see below). Enforcement remains in place, so a signed build is still
  the supported route for any *reputation*-dependent check, but it is not what
  prevented the v0.1.4 launch from being validated.
- **There is no supported deterministic way to run this release unsigned on a
  Smart App Control-enforced host.** An app runs when Microsoft app intelligence
  can classify it as safe, or when it is signed with an RSA certificate chaining
  to a CA in the Microsoft Trusted Root Program. Cloud reputation can only
  accumulate after real-world distribution, so it is not a reproducible release
  gate, and a self-signed certificate is not trusted for this purpose. Signing
  is therefore the only supported route; the checks that require it stay
  unchecked below.
- **EV is no longer a shortcut.** Since 2024 EV-signed files build SmartScreen
  reputation on the same schedule as OV, so EV is not required and is not
  recommended purely for reputation reasons. OV from a Trusted Root Program CA
  is sufficient. Microsoft's own docs recommend *Azure Artifact Signing*
  (formerly Trusted Signing) for non-Store distribution.

## Checksum

- The release script writes a `.sha256` sidecar for **both** the installer and
  the portable ZIP, computed last, after any optional signing:

```powershell
Get-Content artifacts\installer\TrafficLens-Setup-0.1.4-win-x64.exe.sha256
```

- Because signing changes file bytes, a signed release must always be re-hashed
  after signing. Checksums published for an unsigned build describe that
  unsigned build only.

## Localization

- The publish includes the Persian satellite
  `fa-IR/TrafficLens.WinUI.resources.dll`; the neutral English resource set is
  embedded in `TrafficLens.WinUI.dll`. Both en-US and fa-IR work from the
  installed build, and the installer leaves `settings.json` untouched so the
  user's saved language survives install/upgrade/uninstall/reinstall. The
  pipeline fails if either part of the localization is missing.
- **Installer localization (TL-024):** The installer supports English and
  Persian (Farsi) via a language selection dialog at startup. Custom
  `packaging/Persian.isl` provides RTL layout and translated wizard messages.
  Some uncommon error messages fall back to English. The application's
  `[CustomMessages]` section provides per-language translations for the
  running-app warning dialog.

## Native dependencies

- **Windows App SDK** — self-contained payload in the publish root
  (`Microsoft.WindowsAppRuntime.dll`, `Microsoft.WindowsAppRuntime.Bootstrap.dll`,
  `Microsoft.WindowsAppRuntime.pri`, `Microsoft.ui.xaml.dll`, `Microsoft.UI.Xaml\*`)
  plus the x64 native bridge in `amd64\`. No Windows App Runtime install is
  required on the target machine.
- **SQLite** — `Microsoft.Data.Sqlite` (managed) + bundled native
  `e_sqlite3.dll`, resolved from the publish directory.
- **TraceEvent / ETW** — `Microsoft.Diagnostics.Tracing.TraceEvent` ships its
  own management/native helpers; resolved from the publish output, never the
  dev NuGet cache.
- **WinForms NotifyIcon** — the tray host project
  `TrafficLens.WinUI.Tray` uses `Microsoft.WindowsDesktop.App.WindowsForms`,
  included in the self-contained output.
- **Windows IP Helper / kernel table P/Invokes** — system DLLs
  (`iphlpapi`, `Ntdll`), always present on Windows 10/11 x64.
- The launch test is performed from the isolated publish directory, so
  resolution is verified against the shipped payload only.

## Project resources (PRI) — release-critical

- **The project PRI must ship, or the app cannot start.** This is an unpackaged
  app (`WindowsPackageType=None`), but its XAML is still compiled by MRT Core
  and delivered **only** inside `TrafficLens.WinUI.pri`; the managed assembly
  embeds zero XBF resources. A publish tree without that file builds, tests,
  packages, installs and checksums cleanly and then dies at runtime with
  `Microsoft.UI.Xaml.Markup.XamlParseException: XAML parsing failed` in
  `MainWindow.InitializeComponent()` (Application Error 1000,
  `Microsoft.UI.Xaml.dll`, `0xc000027b`).
- **Why the default publish drops it.** MRT Core
  (`Microsoft.Windows.SDK.BuildTools.MSIX.MrtCore.PriGen.targets`) writes the
  PRI straight to `$(TargetDir)` and only adds it as a publishable item when
  `AppxPackage == true`, i.e. for MSIX. With `AppxPackage=false` the PRI is
  never registered in `ResolvedFileToPublish`, so `dotnet publish -o` silently
  omits it. Nothing warns and the build stays green.
- **The fix is an explicit publish hook,** not a copy of `bin` contents:
  `IncludeProjectPriFileInPublish` runs after
  `ComputeResolvedFilesToPublishList` and adds `$(ProjectPriFullPath)` with
  `RelativePath=$(ProjectPriFileName)`. `Build` (and therefore
  `PrepareForRun` → `_GenerateProjectPriFile`) has already run by that point.
- **The pipeline now refuses to package without it.** Publish validation fails
  the run **before** the ZIP and installer are produced when the PRI is missing,
  empty, implausibly small (<256 KB), or not a valid PRI container. MRT Core
  emits the `mrm_pri2` container, so the header is validated as `mrm_` (legacy
  `PRIC` is also accepted) instead of assuming a magic this toolchain never
  produces. The guard was verified to fail on the broken tree and pass on the
  fixed one.

## Clean-machine coverage (limitation)

- **No disposable clean Windows VM is available in this environment.** The
  closest approximation is: launch from the isolated publish directory (out of
  the repo working tree), verify DLL/native resolution, resource satellites,
  `%LOCALAPPDATA%\TrafficLens` creation, SQLite DB creation/use, and run the
  install → launch → use → uninstall → reinstall lifecycle through the real
  installer (which installs into a fresh `{app}`). True from-scratch clean-VM
  verification should be performed on the first real release pipeline.
- **v0.1.4 launches on the release host.** The earlier claim that Smart App
  Control blocked the release was wrong (see above). The defect that actually
  stopped the first RC was the missing project PRI; after the publish fix a
  single launch started cleanly and was left running for human inspection. The
  signed-build-dependent *reputation* checks and
  `scripts/tl023-release-lifecycle.ps1` remain **PENDING** a signed build.

## v0.1.4 release artifacts (verified)

> Two earlier v0.1.4 RCs are listed first and are kept only as history: the first was
> **invalid** (missing PRI, unlaunchable), the second predates human verification.
> The authoritative final pair is the third table, built from `fd7c176` after the
> human UI PASS.

| Superseded artifact (broken) | Size | SHA-256 |
|---|---|---|
| `TrafficLens-Setup-0.1.4-win-x64.exe` | 85.50 MB | `3E9DEA0EB6594DE7055C6FAF73746467AA40D9256C672FF9685A77FD7269146A` |
| `TrafficLens-Portable-0.1.4-win-x64.zip` | 123.09 MB | `660E1D4C9F502F179CB249E7A36AAF9F4A0943583E37C4A66E8A7973E96AF7FE` |

| Superseded artifact (pre-human-verification RC) | Size | SHA-256 |
|---|---|---|
| `artifacts/installer/TrafficLens-Setup-0.1.4-win-x64.exe` | 85.78 MB | `E03C4B00B7A724F702D294D38EC82F415622988FAFEBC5000C44C7DA08F7CBAF` |
| `artifacts/portable/TrafficLens-Portable-0.1.4-win-x64.zip` | 123.61 MB | `0539049C57B4214632592382648524E8D7BA40E2F5571CFBE4A6210A6E1289F6` |

| **Final artifact (authoritative, from `fd7c176`)** | Size | SHA-256 |
|---|---|---|
| `artifacts/installer/TrafficLens-Setup-0.1.4-win-x64.exe` | 89,953,854 bytes (85.79 MB) | `A982E268E13AB03DD36BBEA335947CC886FF50170E255391D1D935F00E52D70E` |
| `artifacts/portable/TrafficLens-Portable-0.1.4-win-x64.zip` | 129,618,364 bytes (123.61 MB) | `03A41E32F5A76837C8E58A1AC6D1EC557E4471A9D0F979669C511AC2BFA61D24` |

- Both final hashes were **recomputed from the final files** and match their
  `.sha256` sidecars exactly. The sidecar files are generated last in the pipeline,
  after the artifacts.
- Installer: `ProductVersion 0.1.4`, `FileVersion 0.1.4`, `TrafficLens` /
  `TrafficLens Contributors` / `TrafficLens Network Monitor Setup`, `NotSigned`.
  AppId `{8F0E8A8F-7B1D-4A5E-9C2D-3E5F6A7B8C9D}` and entry point
  `TrafficLens.WinUI.exe` are unchanged in `packaging\TrafficLens.iss`; the
  `[InstallDelete]` line that removes `{app}\TrafficLens.exe` is the intended
  upgrade-time removal of the superseded WPF build, not a shipped payload.
- ZIP: 815 entries, **all forward-slash**, 0 PDB entries, 0 test assemblies,
  0 `.cs`/`.xaml` sources, 0 `obj`/`bin` leftovers, no WPF `TrafficLens.exe`, and it
  **contains `TrafficLens.WinUI.pri`** plus the `fa-IR` satellite. Verified by
  extracting to a temp directory: 815 files, `TrafficLens.WinUI.exe`
  `ProductVersion 0.1.4` / `FileVersion 0.1.4.0`, `AssemblyVersion 0.1.4.0`,
  Windows App SDK payload, `fa-IR` satellite and branding assets present.
- Publish tree: 815 files, `TrafficLens.WinUI.exe` 297,984 bytes valid x64 PE,
  `TrafficLens.WinUI.dll` 499,200 bytes, Windows App SDK runtime present
  (`CoreMessagingXP.dll` and `Microsoft.WindowsAppRuntime.Bootstrap.dll` are the
  current filenames; the older `CoreMessaging.dll` / `WinAppRuntimeBootstrap.dll`
  names do not apply to this toolchain), no PDB/test assemblies.
- Branding: `assets\branding\TrafficLens.ico`
  (`9F8DD468D671D648367E345109118EEF6C3B2E78A6F4B07054D292B12F8C60BE`) and
  `TrafficLens-256.png` (`41686F13…`) are byte-identical between the repository
  sources and the packaged payload; the EXE embeds the icon resource.
- Project PRI: 2,231,888 bytes, `mrm_pri2` container, SHA-256
  `5F286940DDC6AE04C8904DB78597A0664164B344CB8AF9308728EBCDC95756C6`, present in
  both the publish tree and the ZIP.
- **Runtime smoke-tested against the extracted final portable ZIP** (not a
  `bin\Release` build): window opened, `MainWindow activated`, culture `fa-IR`, seven
  nav items, shell quick action at 29 physical px relLeft / 13 relTop at 150% DPI,
  title-to-caption gap **+35** px, Floating Widget opened 510x210 with live values
  and disabled cleanly, three pages navigated, process still alive at the end, and
  `settings.json` byte-identical to its pre-test backup. The run's log block is clean
  — 0 Error; the only warnings are the documented non-elevated ETW per-process
  collector.
- **Installer runtime test not performed:** an earlier TrafficLens installation
  occupies this release's AppId uninstall registration at
  `%LOCALAPPDATA%\Programs\TrafficLens`, so a silent install of the final installer
  would overwrite that directory and rewrite the same registration rather than test
  in isolation. Verified statically and by successful compilation instead.
- Both final hashes describe the **unsigned** build and must be recomputed for a
  signed release.
- The v0.1.3 artifacts were preserved unchanged next to these outputs
  (installer SHA-256 `55EBCCD8A30CE6BEED0BCF67A77DD77E517FB10D97F315B5A25F251ED2C3D252`).

## Branding / icon pipeline (implemented in TL-016 continuation)

- A single replaceable brand asset set lives under `assets\branding\` and is
  generated by `scripts\generate-icons.ps1` (multi-size ICO + PNGs). The glyph
  mirrors the runtime-drawn tray icon. See `docs\BRANDING.md` for details.
- Wiring now in place:
  - EXE icon: `TrafficLens.WinUI.csproj` → `<ApplicationIcon>`; WPF keeps its
    own `<ApplicationIcon>` for the retained rollback build.
  - WinUI window icon: per-size HICON cache in `WindowIcon` shared by the main
    window, the floating widget and the tray host; `WM_SETICON` is re-applied to
    the correct HWNDs, including a widget window created after startup (one-shot
    deferred `DispatcherQueue` reapply).
  - tray icon: `TrafficLens.WinUI.Tray` prefers the embedded brand ICO and falls
    back to the runtime-drawn glyph if the resource is unavailable.
  - installer icon: `SetupIconFile={#BrandIcon}` resolves to
    `assets\branding\TrafficLens.ico`; `[Setup]` also carries product metadata
    (`VersionInfoCompany`/`Description`/`ProductName`/`ProductVersion`).
  - Start Menu / Desktop shortcut icons inherit the exe icon.
  - the pipeline fails if `Assets\TrafficLens.ico` or
    `Assets\TrafficLens-256.png` is missing from the publish output.
- Replacing branding later only requires re-running `scripts\generate-icons.ps1`
  (or swapping the files in `assets\branding\`) — no `[Setup]` structural changes
  and no application wiring changes are required.
- Known follow-up: The installer now supports English and Persian (TL-024).
  `AppPublisherURL`/`AppSupportURL` placeholders have been removed; no valid
  official URL exists yet. Code signing remains a future release-checklist item.

## Future branding/icon replacement point

- The placeholder icon (`packaging/placeholder.ico`) has been removed; the
  installer now points at the shared brand asset. Any final marketing artwork
  is dropped into `assets\branding\` (or the generator is updated) and the
  pipeline re-run. See the section above and `docs/BRANDING.md`.
- No `[Setup]` structural changes are required to swap artwork.

## Final release checklist (v0.1.4)

Only items that were actually verified on this machine are ticked. Everything
that needs a signed build or a human eye is deliberately left unticked.

### Automated — static and build-time (verified)

- [x] Release configuration is `AnyCPU`-free; x64 is required end to end.
- [x] `dotnet build` Release: 0 warnings, 0 errors.
- [x] Full automated test suite: 731/731 pass (App 270 / Network 212 / Infrastructure 84 / WinUI 165).
- [x] Publish is WinUI 3 self-contained `win-x64`, loose layout, no single-file, no PDBs.
- [x] Publish output contains no PDBs, no test assemblies, no WPF executable.
- [x] Entry point is `TrafficLens.WinUI.exe`.
- [x] Version metadata consistent: product/informational `0.1.4`, assembly/file `0.1.4.0`.
- [x] Publish output validated before packaging (rejects PDBs, tests, WPF binary).
- [x] Portable ZIP built with forward-slash entry names and verified entry count (815).
- [x] Project PRI present and validated before packaging (present, size, `mrm_` header).
- [x] SHA-256 sidecars written for installer and ZIP, computed last.
- [x] Signing path statically verified: correct order, discovered sign set, RSA-only guard, no secrets in repo.

### Automated — signing (NOT yet executed; no certificate available)

- [ ] Installer Authenticode signature is `Valid` after signing.
- [ ] All 7 TrafficLens-owned product binaries signed, `Valid`, and RFC 3161 timestamped.
- [ ] No Microsoft/.NET/Windows App SDK binary was re-signed.
- [ ] Post-sign re-verification of the full product set passes.
- [ ] Checksums re-generated after signing and match the shipped files.

### Runtime — automated observations (v0.1.4)

Performed against the **extracted final portable ZIP** (not a `bin\Release` build),
one launch, PID 11924. Smart App Control did **not** block it; no Code Integrity
event was written.

- [x] App starts from the final portable payload, stays responsive, and opens a real window.
- [x] `WinUI MainWindow activated` and culture `fa-IR` in the app log (`PageTitleText` = داشبورد).
- [x] All 7 nav items present; داشبورد (Dashboard) selected.
- [x] Live data flowing from the collectors (دانلود / آپلود / مجموع populated, and
      verifiably live: مجموع read 11.14 KB/s and 2.82 KB/s ten seconds apart).
- [x] Network, connection and history services started; SQLite schema v2 ready.
- [x] PRI loaded and `InitializeComponent` succeeded — proven by `MainWindow activated`
      on a payload whose compiled XBF lives only in the PRI.
- [x] Shell widget quick action present at 29 physical px relLeft / 13 relTop at 150% DPI.
- [x] Title-to-caption gap +35 px in Persian (clear; the pre-fix build measured -183).
- [x] Floating Widget opens (510x210) at the persisted position and disables cleanly
      again: window closed and the toggle returned to `Off`.
- [x] `settings.json` byte-identical to its pre-test backup afterwards.
- [x] No new Code Integrity events during the launch; whole-day log 0 Error.
- Note: the ETW process collector reports `permission denied` without elevation;
  it needs an Administrator process. This is expected and non-fatal.

### Human visual verification (PASS — 2026-09-28)

A person looked at the screen and approved the build. Machine checks are recorded
above as corroboration, not as a substitute.

- [x] App window icon renders correctly.
- [x] Taskbar icon correct.
- [x] Alt+Tab entry shows the correct icon and name.
- [x] Thumbnail preview (DWM) correct.
- [x] Floating Widget appearance confirmed visually.
- [x] Persian (`fa-IR`) layout verified: no clipping, correct RTL flow, numerals.
- [x] Persian widget controls render correctly.

The UI is now **locked**; any later change must be limited to a proven release,
version or packaging defect.

### Human / signed-build-gated

- [ ] Installer launches and completes a clean install. **Not performed:** an earlier
      installation occupies this release's AppId uninstall registration at
      `%LOCALAPPDATA%\Programs\TrafficLens`, so a silent install would overwrite it
      rather than test in isolation. Verified statically and by compilation instead.
- [ ] Upgrade from the previous installed version preserves settings and history.
- [ ] Uninstall removes program files and (per policy) retains user data.
- [ ] Long-duration stability/soak run completed (WUI-009).
- [ ] Clean-machine / clean-VM install and launch verified.
- [ ] Smart App Control reputation confirmed on the signed build.

### Release decision

- [ ] Certificate obtained and identity validation completed.
- [ ] Signed release candidate built with `-Sign` and re-verified.
- [x] All human visual items above completed.
- [ ] Tag and publish approved by a human.

Not approved for public release: the build is **unsigned**, and that is now the only
outstanding blocker. Human visual verification has passed. No merge, tag, push or
publish has been performed, and no git remote is configured.