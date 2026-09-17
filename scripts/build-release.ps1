<#
.SYNOPSIS
    Build and publish a TrafficLens Release, run tests, validate output,
    and optionally build the Inno Setup installer plus a portable ZIP.
.DESCRIPTION
    Reproducible one-command release pipeline for TrafficLens (TL-015).

    Usage:
      .\scripts\build-release.ps1                    # default version from Directory.Build.props
      .\scripts\build-release.ps1 -Version 0.2.0     # explicit release version

    Requires:
      - .NET 8 SDK (located via PATH or common install paths)
      - Inno Setup 6 (optional; only needed when -SkipInstaller is not set)

    Output (gitignored):
      artifacts/publish/win-x64/                      self-contained publish
      artifacts/installer/TrafficLens-Setup-<ver>-win-x64.exe
      artifacts/installer/TrafficLens-Setup-<ver>-win-x64.exe.sha256
      artifacts/portable/TrafficLens-Portable-<ver>-win-x64.zip
#>
[CmdletBinding()]
param(
    [string]$Version = "",
    [switch]$SkipInstaller = $false,
    [switch]$SkipPortable = $false
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
$shortVersion = $Version -replace "\.(\d+)$", ''

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------
$publishDir = Join-Path $repoRoot "artifacts\publish\win-x64"
$installerDir = Join-Path $repoRoot "artifacts\installer"
$portableDirRoot = Join-Path $repoRoot "artifacts\portable"

$appExeName = "TrafficLens.exe"
$installerName = "TrafficLens-Setup-$Version-win-x64.exe"
$installerBaseName = [System.IO.Path]::GetFileNameWithoutExtension($installerName)
$installerPath = Join-Path $installerDir $installerName
$checksumPath = "$installerPath.sha256"
$portableName = "TrafficLens-Portable-$Version-win-x64.zip"
$portablePath = Join-Path $portableDirRoot $portableName

# ---------------------------------------------------------------------------
# Clean only artifacts (never source or bin/obj of dev builds)
# ---------------------------------------------------------------------------
if (Test-Path -LiteralPath (Join-Path $repoRoot "artifacts")) {
    Write-Step "Cleaning artifacts/"
    Remove-Item -LiteralPath (Join-Path $repoRoot "artifacts") -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publishDir, $installerDir, $portableDirRoot | Out-Null

# ---------------------------------------------------------------------------
# Restore / Build Release
# ---------------------------------------------------------------------------
Write-Step "Restoring solution"
& $dotnet restore "$repoRoot\TrafficLens.sln" --nologo
if ($LASTEXITCODE -ne 0) { Fail "dotnet restore failed (exit $LASTEXITCODE)" }

Write-Step "Building Release (0 warnings / 0 errors required)"
$buildOutput = & $dotnet build "$repoRoot\TrafficLens.sln" -c Release --no-restore --nologo 2>&1
$buildExit = $LASTEXITCODE
$buildOutput | Write-Host
if ($buildExit -ne 0) { Fail "dotnet build failed (exit $buildExit)" }
$warnings = $buildOutput | Select-String -Pattern "warning\s+[A-Z]{2,}\d+" -AllMatches
if ($warnings) {
    Fail "Build produced warnings. Release builds must be 0 warnings."
}

# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------
Write-Step "Running all tests (Release)"
& $dotnet test "$repoRoot\TrafficLens.sln" -c Release --no-build --nologo
if ($LASTEXITCODE -ne 0) { Fail "dotnet test failed (exit $LASTEXITCODE)" }

# ---------------------------------------------------------------------------
# Publish self-contained win-x64
# ---------------------------------------------------------------------------
Write-Step "Publishing self-contained win-x64 (PublishTrimmed=false)"
$publishArgs = @(
    "publish", "$repoRoot\src\TrafficLens.App\TrafficLens.App.csproj",
    "-c", "Release",
    "--no-restore",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:PublishTrimmed=false",
    "-p:Version=$Version",
    "-p:AssemblyVersion=$Version",
    "-p:FileVersion=$Version",
    "-p:InformationalVersion=$Version",
    "-o", $publishDir,
    "--nologo"
)
& $dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { Fail "dotnet publish failed (exit $LASTEXITCODE)" }

# ---------------------------------------------------------------------------
# Validate publish output
# ---------------------------------------------------------------------------
Write-Step "Validating publish output"
$appExe = Join-Path $publishDir $appExeName
if (-not (Test-Path -LiteralPath $appExe)) {
    Fail "Expected executable missing: $appExe"
}
$exeSize = (Get-Item -LiteralPath $appExe).Length
if ($exeSize -lt 1MB) {
    Fail "TrafficLens.exe suspiciously small ($exeSize bytes); publish output likely incomplete."
}
Write-Done "TrafficLens.exe present ($([math]::Round($exeSize/1MB,1)) MB)"

# Verify it is an x64 PE (single-file self-contained bundle). Machine field at
# IMAGE_NT_HEADERS offset e_lfanew+4: 0x8664 = AMD64.
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
    Fail "TrafficLens.exe is not x64 (machine=0x{0:X4}); expected win-x64 publish." -f $machine
}
Write-Done "TrafficLens.exe is a valid x64 PE"

# Version metadata from the produced binary.
$ver = (Get-Item -LiteralPath $appExe).VersionInfo
Write-Host "    ProductVersion : $($ver.ProductVersion)"
Write-Host "    FileVersion    : $($ver.FileVersion)"
Write-Host "    Product        : $($ver.ProductName)"
Write-Host "    Description    : $($ver.FileDescription)"

# NOTE: Single-file publish bundles managed assemblies AND satellite resource
# DLLs (incl. fa-IR\TrafficLens.resources.dll) inside TrafficLens.exe — they are
# extracted to a temp dir by the single-file host on first run. Localization of
# the packaged app is verified at runtime by the launch smoke test, not by file
# presence.

Write-Done "Publish directory: $publishDir"

# ---------------------------------------------------------------------------
# Portable ZIP (optional)
# ---------------------------------------------------------------------------
# NOTE: The portable package is ONLY a zip of the same self-contained publish.
# It does NOT redirect settings/history/logs; those stay in %LOCALAPPDATA%\TrafficLens.
if (-not $SkipPortable) {
    Write-Step "Creating portable ZIP"
    if (Test-Path -LiteralPath $portablePath) { Remove-Item -LiteralPath $portablePath -Force }
    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $portablePath -CompressionLevel Optimal
    Write-Done "Portable: $portablePath"
}

# ---------------------------------------------------------------------------
# Installer (Inno Setup 6) — optional
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
        "/DAppVersionShort=$shortVersion" `
        "/DSourceDir=$publishDir" `
        "/DOutputDir=$installerDir" `
        "/DOutputFile=$installerBaseName" `
        $iss
    if ($LASTEXITCODE -ne 0) { Fail "Inno Setup compile failed (exit $LASTEXITCODE)" }

    if (-not (Test-Path -LiteralPath $installerPath)) {
        Fail "Installer artifact missing after Inno Setup compile: $installerPath"
    }
    Write-Done "Installer: $installerPath"

    # -------------------------------------------------------------------------
    # SHA-256 checksum
    # -------------------------------------------------------------------------
    $hash = Get-FileHash -LiteralPath $installerPath -Algorithm SHA256
    $checksumLine = "$($hash.Hash)  $($hash.Path)"
    Set-Content -LiteralPath $checksumPath -Value $checksumLine -Encoding ascii
    Write-Done "Checksum: $checksumPath"
    Write-Host "SHA-256: $($hash.Hash)" -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "================ RELEASE BUILD COMPLETE ================" -ForegroundColor Green
Write-Host "Version       : $Version"
Write-Host "Publish       : $publishDir"
if (Test-Path -LiteralPath $installerPath) { Write-Host "Installer     : $installerPath" }
if (Test-Path -LiteralPath $checksumPath)  { Write-Host "SHA-256 file  : $checksumPath" }
if (Test-Path -LiteralPath $portablePath)  { Write-Host "Portable ZIP  : $portablePath" }
Write-Host "=========================================================" -ForegroundColor Green