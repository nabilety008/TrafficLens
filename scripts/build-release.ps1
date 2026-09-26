<#
.SYNOPSIS
    Build and publish a TrafficLens Release, run tests, validate output,
    and optionally build the Inno Setup installer plus a portable ZIP.
.DESCRIPTION
    Reproducible one-command release pipeline for TrafficLens (TL-015).

    Since v0.1.4 the release entry point is the WinUI 3 application
    (src\TrafficLens.WinUI). The WPF project (src\TrafficLens.App) is kept in
    the repository as rollback/reference and is NOT published.

    Usage:
      .\scripts\build-release.ps1                    # default version from Directory.Build.props
      .\scripts\build-release.ps1 -Version 0.1.4     # explicit release version

    Repackaging without a rebuild:

      .\scripts\build-release.ps1 -ResumeFromPublish

    -ResumeFromPublish reuses the existing artifacts\publish\win-x64 output of a
    previous successful run and only rebuilds the portable ZIP, the installer and
    the checksums. The publish output is re-validated before it is packaged, so
    an incomplete or stale tree still fails the run. It is intended for
    re-packaging when build and tests already passed and must not be repeated.

    Optional Authenticode signing (signing-ready, no certificate required and
    no secret ever stored in the repository):

      .\scripts\build-release.ps1 -Sign `
          -CertificateThumbprint <thumbprint> -TimestampUrl <rfc3161-tsa-url>

    -Sign is opt-in and never the default. The thumbprint is resolved at run
    time from the Windows certificate store (CurrentUser\My, then
    LocalMachine\My); no thumbprint, password or key is committed. Signing runs
    in the required order: publish -> sign product binaries -> portable ZIP /
    installer -> sign installer -> SHA-256 of the final artifacts.

    Requires:
      - .NET 8 SDK (located via PATH or common install paths)
      - Inno Setup 6 (optional; only needed when -SkipInstaller is not set)
      - signtool.exe (only when -Sign is used; Windows SDK)

    Output (gitignored):
      artifacts/publish/win-x64/                      self-contained WinUI publish
      artifacts/installer/TrafficLens-Setup-<ver>-win-x64.exe
      artifacts/installer/TrafficLens-Setup-<ver>-win-x64.exe.sha256
      artifacts/portable/TrafficLens-Portable-<ver>-win-x64.zip
      artifacts/portable/TrafficLens-Portable-<ver>-win-x64.zip.sha256
#>
[CmdletBinding()]
param(
    [string]$Version = "",
    [switch]$SkipInstaller = $false,
    [switch]$SkipPortable = $false,
    [switch]$ResumeFromPublish = $false,
    [switch]$Sign = $false,
    [string]$CertificateThumbprint = "",
    [string]$TimestampUrl = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Write-Step { param([string]$Message) Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Done { param([string]$Message) Write-Host "    $Message" -ForegroundColor Green }
function Fail { param([string]$Message) Write-Error $Message; exit 1 }

$repoRoot = Split-Path -Parent $PSScriptRoot

# ---------------------------------------------------------------------------
# Locate .NET 8 SDK
# ---------------------------------------------------------------------------
function Resolve-DotNet {
    $candidates = @()
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { $candidates += $cmd.Source }
    $candidates += @(
        "C:\dotnet\dotnet.exe",
        "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe",
        "$env:USERPROFILE\.dotnet\dotnet.exe",
        "$env:ProgramFiles\dotnet\dotnet.exe",
        "${env:ProgramFiles(x86)}\dotnet\dotnet.exe"
    )
    foreach ($c in $candidates | Select-Object -Unique) {
        if (-not $c -or -not (Test-Path -LiteralPath $c)) { continue }
        # Only accept a dotnet that actually has an SDK installed.
        $list = & $c --list-sdks 2>$null
        if ($LASTEXITCODE -ne 0) { continue }
        if (($list | Where-Object { $_ -match "^8\." }) -and $list) { return $c }
    }
    return $null
}

$dotnet = Resolve-DotNet
if (-not $dotnet) {
    Fail "Could not locate a dotnet.exe. Install the .NET 8 SDK (https://dotnet.microsoft.com/download/dotnet/8.0)."
}
Write-Step "Using .NET SDK: $dotnet"
$sdkVersion = (& $dotnet --list-sdks | Select-Object -Last 1)
if ($sdkVersion -notmatch "^8\.") {
    Fail "Expected .NET 8 SDK, found $sdkVersion at $dotnet"
}
Write-Done "SDK $sdkVersion"

# ---------------------------------------------------------------------------
# Optional Authenticode signing (opt-in; no certificate required)
# ---------------------------------------------------------------------------
function Resolve-SignTool {
    $cmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $kitBin = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path -LiteralPath $kitBin) {
        $found = Get-ChildItem -LiteralPath $kitBin -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName "x64\signtool.exe" } |
            Where-Object { Test-Path -LiteralPath $_ } |
            Select-Object -First 1
        if ($found) { return $found }
    }
    return $null
}

function Invoke-SignFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$SignTool,
        [Parameter(Mandatory = $true)][string]$Thumbprint,
        [Parameter(Mandatory = $true)][string]$Timestamp,
        [string]$MachineStore = $false
    )
    # SHA-256 file digest + SHA-256 RFC 3161 timestamp. The timestamp is
    # mandatory: without it the signature stops validating when the certificate
    # expires, which would break Smart App Control and SmartScreen later.
    # /sm is required when the certificate lives in LocalMachine\My, otherwise
    # signtool only searches the current user's store.
    $signArgs = @("sign", "/sha1", $Thumbprint, "/fd", "SHA256", "/td", "SHA256", "/tr", $Timestamp)
    if ($MachineStore -eq "true") { $signArgs += "/sm" }
    $signArgs += $Path
    & $SignTool @signArgs | Out-Host
    if ($LASTEXITCODE -ne 0) { Fail "signtool sign failed for $Path (exit $LASTEXITCODE)" }
    $sig = Get-AuthenticodeSignature -LiteralPath $Path
    if ($sig.Status -ne 'Valid') {
        Fail "Signature is not valid after signing: $Path (status=$($sig.Status))"
    }
    if (-not $sig.TimeStamperCertificate) {
        Fail "Signature on $Path has no RFC 3161 timestamp countersignature. The signature would not outlive the certificate."
    }
    Write-Done "Signed $(Split-Path -Leaf $Path) (timestamped by $($sig.TimeStamperCertificate.Subject))"
}

$signToolPath = $null
$signMachineStore = $false
if ($Sign) {
    Write-Step "Validating signing prerequisites"
    if (-not $CertificateThumbprint) {
        Fail "-Sign requires -CertificateThumbprint. The thumbprint is supplied at run time and is never stored in this repository."
    }
    if (-not $TimestampUrl) {
        Fail "-Sign requires -TimestampUrl. An RFC 3161 timestamp is mandatory so signatures remain valid after certificate expiry."
    }
    $signToolPath = Resolve-SignTool
    if (-not $signToolPath) {
        Fail "signtool.exe not found on PATH or under Windows Kits\10\bin. Install the Windows SDK, or omit -Sign to produce an unsigned release."
    }
    $cert = Get-ChildItem -Path Cert:\CurrentUser\My -ErrorAction SilentlyContinue |
        Where-Object { $_.Thumbprint -eq $CertificateThumbprint } |
        Select-Object -First 1
    if ($cert) {
        $signMachineStore = $false
    } else {
        $cert = Get-ChildItem -Path Cert:\LocalMachine\My -ErrorAction SilentlyContinue |
            Where-Object { $_.Thumbprint -eq $CertificateThumbprint } |
            Select-Object -First 1
        $signMachineStore = $true
    }
    if (-not $cert) {
        Fail "Certificate '$CertificateThumbprint' was not found in Cert:\CurrentUser\My or Cert:\LocalMachine\My."
    }
    if (-not $cert.HasPrivateKey) {
        Fail "Certificate '$CertificateThumbprint' has no private key and cannot sign."
    }
    # Smart App Control's signature check accepts RSA certificates only; it does
    # not support elliptic-curve (ECC) signatures. Signing with an ECC
    # certificate would succeed here and still be blocked at launch, so it is
    # rejected up front instead of producing an unusable release.
    $publicKeyOid = $cert.PublicKey.Oid.Value
    if ($publicKeyOid -ne '1.2.840.113549.1.1.1') {
        $oidName = if ($publicKeyOid -eq '1.2.840.10045.2.1') { 'ECDSA (ECC)' } else { "OID $publicKeyOid" }
        Fail "Certificate '$CertificateThumbprint' uses $oidName. Smart App Control only accepts RSA code-signing certificates, so this release could not launch on a protected device. Obtain an RSA certificate instead."
    }
    if ($cert.NotAfter -le (Get-Date)) {
        Fail "Certificate '$CertificateThumbprint' expired on $($cert.NotAfter)."
    }
    Write-Done "signtool: $signToolPath"
    Write-Done "Certificate subject: $($cert.Subject)"
    Write-Done "Certificate store: $(if ($signMachineStore) { 'LocalMachine\My' } else { 'CurrentUser\My' }) (RSA, valid to $($cert.NotAfter))"
}

# ---------------------------------------------------------------------------
# Version resolution
# ---------------------------------------------------------------------------
if (-not $Version) {
    $propsPath = Join-Path $repoRoot "Directory.Build.props"
    $props = [xml](Get-Content -LiteralPath $propsPath -Raw)
    $Version = $props.Project.PropertyGroup.Version
}
if ($Version -notmatch "^\d+\.\d+\.\d+(\.\d+)?$") {
    Fail "Invalid version '$Version'. Use e.g. 0.1.0"
}
# Assembly/File version follow the existing project convention of a 4-part
# numeric version (0.1.4 -> 0.1.4.0); Product/Informational stay 3-part.
if (($Version -split '\.').Count -eq 3) { $numericVersion = "$Version.0" } else { $numericVersion = $Version }

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------
$publishDir = Join-Path $repoRoot "artifacts\publish\win-x64"
$installerDir = Join-Path $repoRoot "artifacts\installer"
$portableDirRoot = Join-Path $repoRoot "artifacts\portable"

# ---------------------------------------------------------------------------
# Release entry point - WinUI 3 application (v0.1.4+)
# ---------------------------------------------------------------------------
# The WPF project is retained in the repository as rollback/reference material
# and is never published as the release entry point.
$appProject = Join-Path $repoRoot "src\TrafficLens.WinUI\TrafficLens.WinUI.csproj"
$legacyWpfExeName = "TrafficLens.exe"
if (-not (Test-Path -LiteralPath $appProject)) {
    Fail "WinUI project missing: $appProject"
}
$appProjectXml = [xml](Get-Content -LiteralPath $appProject -Raw)
$appAssemblyName = $appProjectXml.Project.PropertyGroup.AssemblyName
if (-not $appAssemblyName) {
    Fail "Could not read <AssemblyName> from $appProject"
}
$appExeName = "$appAssemblyName.exe"
Write-Step "Release entry point: $appExeName (WinUI, from $appProject)"

$installerName = "TrafficLens-Setup-$Version-win-x64.exe"
$installerBaseName = [System.IO.Path]::GetFileNameWithoutExtension($installerName)
$installerPath = Join-Path $installerDir $installerName
$portableName = "TrafficLens-Portable-$Version-win-x64.zip"
$portablePath = Join-Path $portableDirRoot $portableName

# ---------------------------------------------------------------------------
# Clean only artifacts (never source or bin/obj of dev builds)
# ---------------------------------------------------------------------------
if ($ResumeFromPublish) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishDir $appExeName))) {
        Fail "-ResumeFromPublish was requested but no previous publish output exists at $publishDir. Run without -ResumeFromPublish first."
    }
    Write-Step "Resuming from existing publish output (clean, restore, build and tests skipped)"
    New-Item -ItemType Directory -Force -Path $installerDir, $portableDirRoot | Out-Null
} else {
    if (Test-Path -LiteralPath (Join-Path $repoRoot "artifacts")) {
        Write-Step "Cleaning artifacts/"
        Remove-Item -LiteralPath (Join-Path $repoRoot "artifacts") -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $publishDir, $installerDir, $portableDirRoot | Out-Null

    # -----------------------------------------------------------------------
    # Restore / Build Release
    # -----------------------------------------------------------------------
    Write-Step "Restoring solution"
    & $dotnet restore "$repoRoot\TrafficLens.sln" --nologo
    if ($LASTEXITCODE -ne 0) { Fail "dotnet restore failed (exit $LASTEXITCODE)" }

    Write-Step "Building Release (0 warnings / 0 errors required)"
    # Platform=x64 is required, not cosmetic: TrafficLens.WinUI sets
    # WindowsAppSDKSelfContained=true and declares <Platforms>x64</Platforms>, and
    # the Windows App SDK self-contained targets refuse to evaluate under AnyCPU
    # ("WindowsAppSDKSelfContained requires a supported Windows architecture").
    $buildOutput = & $dotnet build "$repoRoot\TrafficLens.sln" -c Release -p:Platform=x64 --no-restore --nologo 2>&1
    $buildExit = $LASTEXITCODE
    $buildOutput | Write-Host
    if ($buildExit -ne 0) { Fail "dotnet build failed (exit $buildExit)" }
    $warnings = $buildOutput | Select-String -Pattern "warning\s+[A-Z]{2,}\d+" -AllMatches
    if ($warnings) {
        Fail "Build produced warnings. Release builds must be 0 warnings."
    }

    # -----------------------------------------------------------------------
    # Tests
    # -----------------------------------------------------------------------
    Write-Step "Running all tests (Release)"
    & $dotnet test "$repoRoot\TrafficLens.sln" -c Release -p:Platform=x64 --no-build --nologo
    if ($LASTEXITCODE -ne 0) { Fail "dotnet test failed (exit $LASTEXITCODE)" }
}

# ---------------------------------------------------------------------------
# Publish self-contained win-x64 - WinUI 3, unpackaged
# ---------------------------------------------------------------------------
# Publish shape notes (v0.1.4+):
#   * Unpackaged WinUI 3 (WindowsPackageType=None) with a self-contained
#     Windows App SDK runtime requires the LOOSE file layout: the bootstrap and
#     the runtime payload (Microsoft.WindowsAppRuntime.*, Microsoft.ui.xaml.dll,
#     the x64 native bridge) must sit next to the executable. PublishSingleFile
#     is therefore NOT used - it is incompatible with self-contained Windows App
#     SDK deployment.
#   * PublishTrimmed=false: WinUI/WinAppSDK reflection is not trim-safe.
#   * DebugType=none: release artifacts must not ship PDBs.
if (-not $ResumeFromPublish) {
    Write-Step "Publishing WinUI self-contained win-x64 (loose layout, no single-file, no PDBs)"
    $publishArgs = @(
        "publish", $appProject,
        "-c", "Release",
        "--no-restore",
        "-p:Platform=x64",
        "-r", "win-x64",
        "--self-contained", "true",
        "-p:WindowsAppSDKSelfContained=true",
        "-p:PublishSingleFile=false",
        "-p:PublishTrimmed=false",
        "-p:PublishReadyToRun=false",
        "-p:DebugType=none",
        "-p:DebugSymbols=false",
        "-p:Version=$Version",
        "-p:AssemblyVersion=$numericVersion",
        "-p:FileVersion=$numericVersion",
        "-p:InformationalVersion=$Version",
        "-o", $publishDir,
        "--nologo"
    )
    & $dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { Fail "dotnet publish failed (exit $LASTEXITCODE)" }
}

# ---------------------------------------------------------------------------
# Validate publish output
# ---------------------------------------------------------------------------
Write-Step "Validating publish output"
$appExe = Join-Path $publishDir $appExeName
if (-not (Test-Path -LiteralPath $appExe)) {
    Fail "Expected executable missing: $appExe"
}
$exeSize = (Get-Item -LiteralPath $appExe).Length
if ($exeSize -lt 100KB) {
    Fail "$appExeName suspiciously small ($exeSize bytes); publish output likely incomplete."
}
Write-Done "$appExeName present ($([math]::Round($exeSize/1MB,2)) MB)"

# The WPF application must never be shipped as, or become, the entry point.
$legacyWpfExe = Join-Path $publishDir $legacyWpfExeName
if (Test-Path -LiteralPath $legacyWpfExe) {
    Fail "Legacy WPF executable '$legacyWpfExeName' is present in the publish output. The WinUI application is the only release entry point."
}
Write-Done "Legacy WPF executable absent from publish output"

# Verify it is an x64 PE. Machine field at IMAGE_NT_HEADERS offset e_lfanew+4:
# 0x8664 = AMD64.
$fs = [System.IO.File]::OpenRead($appExe)
try {
    $br = New-Object System.IO.BinaryReader($fs)
    $fs.Position = 0x3c
    $peOff = $br.ReadInt32()
    if ($peOff -le 0 -or $peOff -gt ($fs.Length - 8)) { Fail "Not a valid PE file: $appExe" }
    $fs.Position = $peOff + 4
    $machine = $br.ReadUInt16()
} finally {
    $fs.Dispose()
}
if ($machine -ne 0x8664) {
    Fail "$appExeName is not x64 (machine=0x{0:X4}); expected win-x64 publish." -f $machine
}
Write-Done "$appExeName is a valid x64 PE"

# Version metadata from the produced binary.
$ver = (Get-Item -LiteralPath $appExe).VersionInfo
Write-Host "    ProductVersion : $($ver.ProductVersion)"
Write-Host "    FileVersion    : $($ver.FileVersion)"
Write-Host "    Product        : $($ver.ProductName)"
Write-Host "    Description    : $($ver.FileDescription)"
if ($ver.ProductVersion -notlike "$Version*") {
    Fail "$appExeName ProductVersion is '$($ver.ProductVersion)', expected '$Version'."
}

# Windows App SDK self-contained runtime must be present, otherwise the app
# cannot start on a machine without the Windows App Runtime installed. With
# WindowsAppSDKSelfContained=true the runtime payload ships in the publish root
# (the Windows App Runtime, the XAML framework and the native bridges), not as a
# single bundled executable.
$appRuntimeRequired = @(
    "Microsoft.WindowsAppRuntime.dll",
    "Microsoft.WindowsAppRuntime.Bootstrap.dll",
    "Microsoft.WindowsAppRuntime.pri",
    "Microsoft.ui.xaml.dll"
)
$missingRuntime = @()
foreach ($rt in $appRuntimeRequired) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishDir $rt))) { $missingRuntime += $rt }
}
if ($missingRuntime) {
    Fail "Windows App SDK self-contained runtime payload incomplete. Missing: $($missingRuntime -join ', ')"
}
$runtimeFileCount = (Get-ChildItem -LiteralPath $publishDir -Recurse -File).Count
Write-Done "Windows App SDK self-contained runtime present ($runtimeFileCount files, loose layout)"

# Release artifacts must not ship debug symbols or test assemblies.
$strayPdb = Get-ChildItem -LiteralPath $publishDir -Recurse -Filter *.pdb -ErrorAction SilentlyContinue
if ($strayPdb) {
    Fail "PDB files present in the release publish output: $($strayPdb.Name -join ', ')"
}
$strayTest = Get-ChildItem -LiteralPath $publishDir -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '\.Tests?\.dll$|\.testhost\.|xunit' }
if ($strayTest) {
    Fail "Test assemblies present in the release publish output: $($strayTest.Name -join ', ')"
}
Write-Done "No PDB or test assemblies in the publish output"

# Branding assets shipped with the app.
foreach ($asset in @("Assets\TrafficLens.ico", "Assets\TrafficLens-256.png")) {
    $assetPath = Join-Path $publishDir $asset
    if (-not (Test-Path -LiteralPath $assetPath)) {
        Fail "Branding asset missing from the publish output: $asset"
    }
}
Write-Done "Branding assets present (TrafficLens.ico, TrafficLens-256.png)"

# Localization: the neutral (English) resource set is embedded in the WinUI
# assembly, and the Persian (fa-IR) set ships as a satellite assembly next to
# it. Both must be present for runtime EN/FA switching and RTL.
$winuiDll = Join-Path $publishDir "$appAssemblyName.dll"
if (-not (Test-Path -LiteralPath $winuiDll)) {
    Fail "WinUI assembly missing from the publish output: $winuiDll"
}
$winuiBytes = [System.IO.File]::ReadAllBytes($winuiDll)
$ascii = [System.Text.Encoding]::ASCII.GetString($winuiBytes)
if ($ascii -notmatch [regex]::Escape("$appAssemblyName.Resources.Strings")) {
    Fail "Embedded English (neutral) resource set not found in $appAssemblyName.dll"
}
$faSatellite = Join-Path $publishDir "fa-IR\$appAssemblyName.resources.dll"
if (-not (Test-Path -LiteralPath $faSatellite)) {
    Fail "Persian (fa-IR) satellite assembly missing from the publish output: fa-IR\$appAssemblyName.resources.dll"
}
Write-Done "Localization present (embedded en + fa-IR satellite)"

# ---------------------------------------------------------------------------
# Sign product binaries (optional, before packaging)
# ---------------------------------------------------------------------------
# Smart App Control (VerifiedAndReputableDesktop) and SmartScreen evaluate the
# product binaries themselves, so they are signed BEFORE the portable ZIP and
# the installer are produced.
#
# The set is DISCOVERED, not hard-coded: every TrafficLens-owned PE image in the
# publish output is signed, wherever it lives (the fa-IR satellite assembly sits
# in a culture subdirectory). Smart App Control treats a partially signed
# application as untrusted, so a new assembly must never be silently left
# unsigned.
# Microsoft/.NET/Windows App SDK binaries are NOT touched - they are already
# signed by their vendor and re-signing them would be both wrong and harmful.
#
# NOTE: the extension test is applied explicitly rather than with -Include.
# Get-ChildItem -LiteralPath -Recurse -Include silently IGNORES -Include and
# returns every file, which would make signtool attempt to sign .json/.ico/.png.
$productBinaryNames = Get-ChildItem -LiteralPath $publishDir -Recurse -File |
    Where-Object { $_.Extension -in '.exe', '.dll' -and $_.Name -like 'TrafficLens*' } |
    Sort-Object -Property FullName -Unique
if (-not $productBinaryNames) {
    Fail "No TrafficLens-owned binaries found in $publishDir; refusing to produce an unsigned release."
}
Write-Step "TrafficLens-owned binaries to sign: $($productBinaryNames.Count)"
foreach ($b in $productBinaryNames) {
    Write-Host "    $($b.FullName.Substring($publishDir.Length + 1))"
}

if ($Sign) {
    Write-Step "Signing product binaries (SHA-256 + RFC 3161 timestamp)"
    foreach ($file in $productBinaryNames) {
        Invoke-SignFile -Path $file.FullName -SignTool $signToolPath -Thumbprint $CertificateThumbprint `
            -Timestamp $TimestampUrl -MachineStore $signMachineStore
    }
    # Re-verify the whole set: a partial signature would leave the release
    # unlaunchable under Smart App Control even though every sign call succeeded.
    $unsigned = @()
    foreach ($file in $productBinaryNames) {
        $s = Get-AuthenticodeSignature -LiteralPath $file.FullName
        if ($s.Status -ne 'Valid' -or -not $s.TimeStamperCertificate) { $unsigned += "$($file.Name) ($($s.Status))" }
    }
    if ($unsigned) {
        Fail "Product binaries are not fully validly signed: $($unsigned -join ', ')"
    }
    Write-Done "All $($productBinaryNames.Count) TrafficLens binaries signed, verified and timestamped"
} else {
    Write-Host "    (unsigned build - release artifacts and their checksums describe an UNSIGNED candidate)" -ForegroundColor Yellow
}

Write-Done "Publish directory: $publishDir"

# ---------------------------------------------------------------------------
# Portable ZIP (optional)
# ---------------------------------------------------------------------------
# NOTE: The portable package is ONLY a zip of the same self-contained WinUI
# publish directory (loose layout: executable + product assemblies + Windows
# App SDK runtime). It does NOT redirect settings/history/logs; those stay in
# %LOCALAPPDATA%\TrafficLens.
if (-not $SkipPortable) {
    Write-Step "Creating portable ZIP"
    if (Test-Path -LiteralPath $portablePath) { Remove-Item -LiteralPath $portablePath -Force }
    # The ZIP is written entry-by-entry instead of with Compress-Archive for two
    # reasons: Compress-Archive on Windows PowerShell 5.1 writes nested entries
    # with backslash separators (which unzip on Linux/macOS turns into literal
    # backslashes in file names), and it has no -Exclude parameter. Entry names
    # are normalized to forward slashes per the ZIP specification, and the
    # publish tree is already validated to contain no PDBs. Files are added in
    # sorted order so the archive is reproducible. TrafficLens.WinUI.exe stays
    # at the ZIP root.
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $publishRootFull = (Resolve-Path -LiteralPath $publishDir).Path.TrimEnd('\')
    $zipArchive = [System.IO.Compression.ZipFile]::Open($portablePath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $zipEntries = 0
        foreach ($file in (Get-ChildItem -LiteralPath $publishDir -Recurse -File | Sort-Object -Property FullName)) {
            $entryName = $file.FullName.Substring($publishRootFull.Length + 1).Replace('\', '/')
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zipArchive, $file.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal)
            $zipEntries++
        }
    } finally {
        $zipArchive.Dispose()
    }
    if ($zipEntries -lt 1) { Fail "Portable ZIP would be empty; publish directory has no files." }
    Write-Done "Portable: $portablePath ($zipEntries entries)"
}

# ---------------------------------------------------------------------------
# Installer (Inno Setup 6) - optional
# ---------------------------------------------------------------------------
if (-not $SkipInstaller) {
    $iscc = Get-Command iscc -ErrorAction SilentlyContinue
    if (-not $iscc) {
        foreach ($candidate in @(
            "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
            "C:\Program Files\Inno Setup 6\ISCC.exe",
            "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
        )) {
            if (Test-Path -LiteralPath $candidate) { $iscc = @{ Source = $candidate }; break }
        }
    }
    if (-not $iscc) {
        Fail "Inno Setup 6 (iscc) not found. Install it from https://jrsoftware.org/isinfo.php or re-run with -SkipInstaller."
    }
    $isccPath = $iscc.Source

    $iss = Join-Path $repoRoot "packaging\TrafficLens.iss"
    if (-not (Test-Path -LiteralPath $iss)) {
        Fail "Installer definition missing: $iss"
    }

    Write-Step "Compiling installer with Inno Setup: $isccPath"
    & $isccPath `
        "/DAppVersion=$Version" `
        "/DAppVersionShort=$Version" `
        "/DAppExeName=$appExeName" `
        "/DSourceDir=$publishDir" `
        "/DOutputDir=$installerDir" `
        "/DOutputFile=$installerBaseName" `
        $iss
    if ($LASTEXITCODE -ne 0) { Fail "Inno Setup compile failed (exit $LASTEXITCODE)" }

    if (-not (Test-Path -LiteralPath $installerPath)) {
        Fail "Installer artifact missing after Inno Setup compile: $installerPath"
    }
    Write-Done "Installer: $installerPath"
}

# ---------------------------------------------------------------------------
# Sign the installer (optional, after it is generated)
# ---------------------------------------------------------------------------
if ($Sign -and (Test-Path -LiteralPath $installerPath)) {
    Write-Step "Signing installer (SHA-256 + RFC 3161 timestamp)"
    Invoke-SignFile -Path $installerPath -SignTool $signToolPath -Thumbprint $CertificateThumbprint `
        -Timestamp $TimestampUrl -MachineStore $signMachineStore
}

# ---------------------------------------------------------------------------
# SHA-256 checksums - computed LAST, after any signing
# ---------------------------------------------------------------------------
# Signatures change file bytes, so the published checksums must always be
# produced after the optional signing stages above.
Write-Step "Writing SHA-256 checksums"
$checksums = @()
if (Test-Path -LiteralPath $installerPath) {
    $checksums += $installerPath
}
if ((-not $SkipPortable) -and (Test-Path -LiteralPath $portablePath)) {
    $checksums += $portablePath
}
foreach ($artifact in $checksums) {
    $hash = Get-FileHash -LiteralPath $artifact -Algorithm SHA256
    $sidecar = "$artifact.sha256"
    Set-Content -LiteralPath $sidecar -Value "$($hash.Hash)  $($hash.Path)" -Encoding ascii
    Write-Done "SHA-256 $(Split-Path -Leaf $artifact): $($hash.Hash)"
    Write-Host "    Sidecar: $sidecar"
}
if (-not $Sign) {
    Write-Host "    NOTE: these hashes describe the UNSIGNED release candidate." -ForegroundColor Yellow
    Write-Host "    A signed release must be re-hashed after signing." -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "================ RELEASE BUILD COMPLETE ================" -ForegroundColor Green
Write-Host "Version        : $Version"
Write-Host "Entry point    : $appExeName (WinUI 3, unpackaged)"
Write-Host "Publish        : $publishDir"
if (Test-Path -LiteralPath $installerPath) {
    Write-Host "Installer      : $installerPath"
    Write-Host "Installer hash : $installerPath.sha256"
}
if (Test-Path -LiteralPath $portablePath) {
    Write-Host "Portable ZIP   : $portablePath"
    Write-Host "Portable hash  : $portablePath.sha256"
}
Write-Host "Signed         : $(if ($Sign) { 'yes' } else { 'NO (unsigned release candidate)' })"
Write-Host "=========================================================" -ForegroundColor Green