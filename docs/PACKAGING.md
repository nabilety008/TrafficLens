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
  no enterprise-authored policy). Fresh unsigned binaries are therefore blocked
  with `0x800711C7` and the installer cannot be launched for runtime validation
  on this host. No SmartScreen bypass is attempted and no Windows security
  settings are weakened. A trusted (signed) build is required for the
  install/launch/reputation checks.
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

## Clean-machine coverage (limitation)

- **No disposable clean Windows VM is available in this environment.** The
  closest approximation is: launch from the isolated publish directory (out of
  the repo working tree), verify DLL/native resolution, resource satellites,
  `%LOCALAPPDATA%\TrafficLens` creation, SQLite DB creation/use, and run the
  install → launch → use → uninstall → reinstall lifecycle through the real
  installer (which installs into a fresh `{app}`). True from-scratch clean-VM
  verification should be performed on the first real release pipeline.
- **v0.1.4 could not be launched on the release host:** Smart App Control blocks
  fresh unsigned binaries (`0x800711C7`), so install/launch/reputation checks
  and `scripts/tl023-release-lifecycle.ps1` are **PENDING** a signed or
  otherwise trusted build. Verification of v0.1.4 was therefore **static**:
  archive structure, entry names, extraction, version metadata, signature
  status, checksums and payload contents.

## v0.1.4 release artifacts (verified)

| Artifact | Size | SHA-256 |
|---|---|---|
| `artifacts/installer/TrafficLens-Setup-0.1.4-win-x64.exe` | 85.50 MB | `3E9DEA0EB6594DE7055C6FAF73746467AA40D9256C672FF9685A77FD7269146A` |
| `artifacts/portable/TrafficLens-Portable-0.1.4-win-x64.zip` | 123.09 MB | `660E1D4C9F502F179CB249E7A36AAF9F4A0943583E37C4A66E8A7973E96AF7FE` |

- Installer: `ProductVersion 0.1.4`, `TrafficLens` / `TrafficLens Contributors`
  / `TrafficLens Network Monitor Setup`, `NotSigned`.
- ZIP: 814 entries, 0 PDB entries, no WPF `TrafficLens.exe`, forward-slash entry
  names, extracts to a runnable tree (verified by extracting to a temp
  directory: 814 files, `TrafficLens.WinUI.exe` `ProductVersion 0.1.4` /
  `FileVersion 0.1.4.0`, Windows App SDK payload, `fa-IR` satellite and
  branding assets present).
- Publish tree: 814 files / 323.5 MB, `TrafficLens.WinUI.exe` 0.28 MB valid x64
  PE, Windows App SDK runtime present, no PDB/test assemblies.
- Both hashes describe the **unsigned** build and must be recomputed for a
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
- [x] Full automated test suite: 571/571 pass.
- [x] Publish is WinUI 3 self-contained `win-x64`, loose layout, no single-file, no PDBs.
- [x] Publish output contains no PDBs, no test assemblies, no WPF executable.
- [x] Entry point is `TrafficLens.WinUI.exe`.
- [x] Version metadata consistent: product/informational `0.1.4`, assembly/file `0.1.4.0`.
- [x] Publish output validated before packaging (rejects PDBs, tests, WPF binary).
- [x] Portable ZIP built with forward-slash entry names and verified entry count (814).
- [x] SHA-256 sidecars written for installer and ZIP, computed last.
- [x] Signing path statically verified: correct order, discovered sign set, RSA-only guard, no secrets in repo.

### Automated — signing (NOT yet executed; no certificate available)

- [ ] Installer Authenticode signature is `Valid` after signing.
- [ ] All 7 TrafficLens-owned product binaries signed, `Valid`, and RFC 3161 timestamped.
- [ ] No Microsoft/.NET/Windows App SDK binary was re-signed.
- [ ] Post-sign re-verification of the full product set passes.
- [ ] Checksums re-generated after signing and match the shipped files.

### Runtime / human verification (NOT possible on this host)

Blocked because Smart App Control is enforcing and the release is unsigned
(`0x800711C7`). Re-run these on a signed build.

- [ ] Installer launches and completes a clean install.
- [ ] App window opens with the correct title bar and icon.
- [ ] Taskbar icon correct.
- [ ] Alt+Tab entry shows the correct icon and name.
- [ ] Thumbnail preview (DWM) correct.
- [ ] Floating Widget icon correct in the Widgets board.
- [ ] Persian (`fa-IR`) layout verified: no clipping, correct RTL flow, numerals.
- [ ] Persian widget controls render correctly.
- [ ] Upgrade from the previous installed version preserves settings and history.
- [ ] Uninstall removes program files and (per policy) retains user data.
- [ ] Long-duration stability/soak run completed (WUI-009).
- [ ] Clean-machine / clean-VM install and launch verified.
- [ ] Smart App Control reputation confirmed to no longer block the signed build.

### Release decision

- [ ] Certificate obtained and identity validation completed.
- [ ] Signed release candidate built with `-Sign` and re-verified.
- [ ] All runtime items above completed.
- [ ] Tag and publish approved by a human.

Not approved for public release while the signing and runtime sections remain
unticked.