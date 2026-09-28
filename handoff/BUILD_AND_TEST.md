# BUILD AND TEST — TrafficLens

Repository root:

```
C:\Users\ali\Documents\New folder\TrafficLens
```

Run everything from the repository root.

## Read this first: the dotnet on PATH cannot build

`C:\Program Files\dotnet\dotnet.exe` is the `dotnet` that PATH resolves, and it has
**zero SDKs installed** (`dotnet --list-sdks` returns nothing). Calling it gives:

> The command could not be loaded… No .NET SDKs were found.

The working SDK is a **private install**:

| | |
|---|---|
| Working `dotnet.exe` | `C:\Users\ali\.dotnet\dotnet.exe` |
| SDK | **8.0.425** (`net8.0`) |

Setting `$env:DOTNET_ROOT` alone is **not sufficient** — the PATH muxer still wins.
Use one of these instead:

```powershell
# Option A - call it by full path (most explicit, recommended for scripts)
$dn = "C:\Users\ali\.dotnet\dotnet.exe"
& $dn --version        # -> 8.0.425

# Option B - prepend to PATH for the current session
$env:PATH = "C:\Users\ali\.dotnet;$env:PATH"
dotnet --version       # -> 8.0.425
```

`scripts\build-release.ps1` handles this itself: `Resolve-DotNet` probes a list of
candidate locations and only accepts a `dotnet` that actually reports an SDK.

## x64 is mandatory, not cosmetic

`TrafficLens.WinUI` declares `<Platforms>x64</Platforms>`, `WindowsAppSDKSelfContained=true`
and `RuntimeIdentifiers=win-x64`. **Always pass `-p:Platform=x64`**, or the WinUI
project will not build. This is the single most common build failure here.

## Commands

### Restore

```powershell
& $dn restore "C:\Users\ali\Documents\New folder\TrafficLens\TrafficLens.sln" --nologo
```

### Release build

```powershell
& $dn build "C:\Users\ali\Documents\New folder\TrafficLens\TrafficLens.sln" `
    -c Release -p:Platform=x64 --no-restore --nologo
```

Expected at closeout: **0 warnings, 0 errors**.

### Full test suite

```powershell
& $dn test "C:\Users\ali\Documents\New folder\TrafficLens\TrafficLens.sln" `
    -c Release -p:Platform=x64 --no-build --nologo
```

Expected at closeout: **731 passed, 0 failed**
(App 270 / Network 212 / Infrastructure 84 / WinUI 165).

### Focused tests (one project)

```powershell
# WinUI (165 tests) - most relevant for UI/widget/titlebar work
& $dn test "C:\Users\ali\Documents\New folder\TrafficLens\tests\TrafficLens.WinUI.Tests\TrafficLens.WinUI.Tests.csproj" `
    -c Release -p:Platform=x64

# Network (212)
& $dn test "C:\Users\ali\Documents\New folder\TrafficLens\tests\TrafficLens.Network.Tests\TrafficLens.Network.Tests.csproj" `
    -c Release -p:Platform=x64

# Infrastructure (84)
& $dn test "C:\Users\ali\Documents\New folder\TrafficLens\tests\TrafficLens.Infrastructure.Tests\TrafficLens.Infrastructure.Tests.csproj" `
    -c Release -p:Platform=x64

# App (270)
& $dn test "C:\Users\ali\Documents\New folder\TrafficLens\tests\TrafficLens.App.Tests\TrafficLens.App.Tests.csproj" `
    -c Release -p:Platform=x64
```

Filter a single test or class with `--filter`, e.g.:

```powershell
& $dn test "…\tests\TrafficLens.WinUI.Tests\TrafficLens.WinUI.Tests.csproj" `
    -c Release -p:Platform=x64 --filter "FullyQualifiedName~TitleBarCaptionLayoutTests"
```

`tests\TrafficLens.Network.Verification` is a **console harness** (no
`Microsoft.NET.Test.Sdk`), so `dotnet test` will not run it and it is not part of
the 731. Run it with `dotnet run --project` if you need it.

## Launching the app

```powershell
# Published payload (what the release actually ships)
Start-Process "C:\Users\ali\Documents\New folder\TrafficLens\artifacts\publish\win-x64\TrafficLens.WinUI.exe"

# Release build output (dev iteration)
Start-Process "C:\Users\ali\Documents\New folder\TrafficLens\src\TrafficLens.WinUI\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\TrafficLens.WinUI.exe"
```

Notes:

- **Close the running app before rebuilding Release.** It locks the output DLLs and
  the build fails with a file-in-use error. Check with
  `Get-Process TrafficLens.WinUI -ErrorAction SilentlyContinue`.
- `TrafficLens.WinUI.exe` is a native apphost. `AssemblyName.GetAssemblyName()`
  on it throws — that is expected, not a defect. Read version metadata from
  `TrafficLens.WinUI.dll` or the exe's `VersionInfo` instead.
- Per-process (Applications) data needs an **Administrator** process. Without
  elevation the ETW collector logs `permission denied` and reports a degraded
  status; the rest of the app works normally.
- User data lives in `%LOCALAPPDATA%\TrafficLens\` (`settings.json`, `logs\`,
  SQLite history). Back up or delete that folder to reset app state.

## Release pipeline

```powershell
& "C:\Users\ali\Documents\New folder\TrafficLens\scripts\build-release.ps1" -Version 0.1.4
```

Useful switches (read the `param` block at the top of the script):

| Switch | Effect |
|---|---|
| `-Version <x.y.z>` | Version to stamp; defaults to the value in `Directory.Build.props` |
| `-SkipInstaller` | Skip the Inno Setup step (ISCC not required) |
| `-SkipPortable` | Skip the ZIP |
| `-ResumeFromPublish` | Reuse an existing publish output |
| `-Sign` | Sign product binaries (needs a cert in the store) |
| `-CertificateThumbprint <hex>` | Certificate to use with `-Sign` |
| `-TimestampUrl <url>` | RFC 3161 timestamp service |

The pipeline runs: resolve dotnet → restore → build (x64) → **all tests** → publish
→ **validate PRI** → ZIP → Inno installer → SHA-256 sidecars (computed last).

**Do not run the release pipeline just to test a code change.** It rebuilds and
overwrites the v0.1.4 artifacts, whose hashes are recorded in
`handoff/CURRENT_STATE.md` and `docs/PACKAGING.md`. Use build + focused tests for
ordinary work.

### External tool requirements

| Tool | Needed for | Location on this machine |
|---|---|---|
| .NET 8 SDK | build, test, publish | `C:\Users\ali\.dotnet\dotnet.exe` (8.0.425) |
| Inno Setup 6 (`iscc`) | installer step only | `C:\Users\ali\AppData\Local\Programs\Inno Setup 6\ISCC.exe` |

`iscc` is **not** on PATH. The release script probes, in order, `Get-Command iscc`,
`C:\Program Files (x86)\Inno Setup 6\ISCC.exe`, `C:\Program Files\Inno Setup 6\ISCC.exe`,
then `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe` — which is where it actually is
here. Use `-SkipInstaller` if Inno Setup is unavailable.

## Artifact locations

| Artifact | Path |
|---|---|
| Publish payload | `artifacts\publish\win-x64\` |
| Portable ZIP (+ `.sha256`) | `artifacts\portable\TrafficLens-Portable-<ver>-win-x64.zip` |
| Installer (+ `.sha256`) | `artifacts\installer\TrafficLens-Setup-<ver>-win-x64.exe` |

`artifacts/` is **gitignored** (`.gitignore` line 10). Artifacts are not preserved
by git — copy them out before cleaning the directory. Expected v0.1.4 SHA-256
values are in `handoff/CURRENT_STATE.md`.

## PowerShell caveats (learned the hard way)

**Never use `Set-Content`, `Out-File`, or `Add-Content` for repository source or
documentation.** Windows PowerShell 5.1 has repeatedly caused encoding, BOM and
mojibake corruption this way — including mangling Persian and em-dash characters
in `.md` files. Use the editor/`write` tooling, which writes UTF-8 correctly.
`Set-Content` is acceptable only for disposable scratch files under `artifacts/`.

Other traps in this environment:

- **Heredocs do not work.** `git commit -F - <<'EOF'` is a parse error in
  PowerShell 5.1. Write the message to a temporary file and use `git commit -F <file>`.
- **`&&` does not work** between commands. Use `;` plus an explicit check, or
  `if ($?) { … }`.
- **Console output mangles non-ASCII.** The default code page renders em dashes as
  `?` and Persian text as mojibake. That is a *display* artifact, not file
  corruption — set `[Console]::OutputEncoding = [System.Text.Encoding]::UTF8` when
  you need to read such text, and re-read the file with a proper read tool before
  believing it is wrong.
- **`[System.IO.File]` methods do not resolve PowerShell-relative paths.** Use an
  absolute path or `Join-Path`, or the call fails with "Could not find a part of
  the path" even though `Get-Item` on the same relative path succeeds.
- **`Get-ChildItem -Recurse -Include` silently ignores `-Include`** unless you
  filter with `-Filter` or add a trailing `\*`. This already caused a real defect
  in the release signing set.
- **Do not enumerate `bin\` or `obj\`** to understand the project; the output is
  thousands of localization satellite folders. Filter them out.
- Prefer `workdir` over `Set-Location` when running tools.
