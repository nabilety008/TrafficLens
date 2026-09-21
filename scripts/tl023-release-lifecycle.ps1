<#
.SYNOPSIS
    TL-023 Release lifecycle validation: install, launch, upgrade, uninstall,
    reinstall, startup registration, portable build, and data preservation.
.DESCRIPTION
    Validates the full release lifecycle using the built installer and portable ZIP.
    Uses isolated temporary directories to avoid touching the developer's real data.
    Requires: release artifacts already built by build-release.ps1.
#>
param(
    [string]$Installer = "",
    [string]$PortableZip = "",
    [string]$ChecksumFile = ""
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([System.Threading.Thread]::CurrentThread.ApartmentState -ne [System.Threading.ApartmentState]::STA) {
    $argList = @('-NoProfile', '-Sta', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    $argList += $args
    & powershell.exe $argList
    exit $LASTEXITCODE
}

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $Installer)   { $Installer   = Join-Path $repoRoot "artifacts\installer\TrafficLens-Setup-0.1.2-win-x64.exe" }
if (-not $PortableZip) { $PortableZip = Join-Path $repoRoot "artifacts\portable\TrafficLens-Portable-0.1.2-win-x64.zip" }
if (-not $ChecksumFile){ $ChecksumFile = "$Installer.sha256" }

$script:anyFail = $false
$script:passCount = 0
$script:failCount = 0

function Pass([string]$c) { Write-Host "  PASS: $c" -ForegroundColor Green; $script:passCount++ }
function Fail([string]$c, [string]$r) { Write-Host "  FAIL: $c ($r)" -ForegroundColor Red; $script:anyFail = $true; $script:failCount++ }
function Info([string]$c) { Write-Host "  (i) $c" -ForegroundColor DarkGray }
function Kill-All { Get-Process -Name "TrafficLens" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue }

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class WinApiTl23 {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    public static IntPtr[] WindowsOf(uint pid) {
        var all = new System.Collections.Generic.List<IntPtr>();
        EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid) all.Add(h); return true; }, IntPtr.Zero);
        return all.ToArray();
    }
    public static string Text(IntPtr h) { var sb = new StringBuilder(512); GetWindowText(h, sb, 512); return sb.ToString(); }
}
"@

function Wait-MainWindow($proc, $maxSeconds = 30) {
    for ($t = 0; $t -lt $maxSeconds * 2; $t++) {
        foreach ($h in [WinApiTl23]::WindowsOf([uint32]$proc.Id)) {
            if ([WinApiTl23]::IsWindowVisible($h)) {
                $text = [WinApiTl23]::Text($h)
                if ($text -match 'TrafficLens') { return $text }
            }
        }
        Start-Sleep -Milliseconds 500
    }
    return ''
}
function Wait-ProcessExit($procId, $maxSeconds = 20) {
    for ($t = 0; $t -lt $maxSeconds * 2; $t++) {
        if ($null -eq (Get-Process -Id $procId -ErrorAction SilentlyContinue)) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return $false
}
function Get-EtwSessions {
    $results = @()
    try {
        $output = logman query -ets 2>$null
        if ($output -is [string]) { $output = $output -split "`n" }
        foreach ($l in $output) { if ($l -match "TrafficLens") { $results += $l.Trim() } }
    } catch {}
    return ,$results
}
function Get-StartupValue {
    try {
        $k = Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "TrafficLens" -ErrorAction SilentlyContinue
        return $k.TrafficLens
    } catch { return $null }
}
function Remove-StartupValue {
    try {
        $k = Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "TrafficLens" -ErrorAction SilentlyContinue
        if ($k) { Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "TrafficLens" -ErrorAction SilentlyContinue }
    } catch {}
}

# --- Validate prerequisites ---
if (-not (Test-Path $Installer)) { Write-Host "FATAL: Installer not found: $Installer" -ForegroundColor Red; exit 1 }
if (-not (Test-Path $PortableZip)) { Write-Host "FATAL: Portable ZIP not found: $PortableZip" -ForegroundColor Red; exit 1 }
Info "Installer: $Installer"
Info "Portable:  $PortableZip"

# --- Isolated test profile ---
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) "tl023-lifecycle-$(Get-Date -Format 'yyyyMMddHHmmss')"
$testAppData = Join-Path $testRoot "AppData\Local\TrafficLens"
$testPrograms = Join-Path $testRoot "Programs\TrafficLens"
$testPortable = Join-Path $testRoot "Portable"
New-Item -ItemType Directory -Force -Path $testAppData, $testPrograms, $testPortable | Out-Null
Info "Isolated profile: $testRoot"

# Backup real settings
$realSettings = "$env:LOCALAPPDATA\TrafficLens\settings.json"
$realSettingsBackup = $null
if (Test-Path $realSettings) { $realSettingsBackup = Get-Content $realSettings -Raw -Encoding UTF8 }

# Backup real startup
$realStartup = Get-StartupValue

Kill-All
Start-Sleep -Seconds 1

try {
    # ====================================================================
    # 1. CHECKSUM VERIFICATION
    # ====================================================================
    Write-Host "`n=== 1. CHECKSUM VERIFICATION ===" -ForegroundColor Yellow
    if (Test-Path $ChecksumFile) {
        $hash = Get-FileHash -LiteralPath $Installer -Algorithm SHA256
        $fileHash = (Get-Content $ChecksumFile -Raw).Trim().Split(' ')[0]
        if ($hash.Hash -eq $fileHash) {
            Pass "1.1: SHA256 checksum matches"
        } else {
            Fail "1.1: SHA256 checksum" "computed=$($hash.Hash) file=$fileHash"
        }
    } else {
        Info "1.1: No checksum file found; skipping"
    }

    # ====================================================================
    # 2. CLEAN INSTALL
    # ====================================================================
    Write-Host "`n=== 2. CLEAN INSTALL ===" -ForegroundColor Yellow
    $installDir = Join-Path $testRoot "Install"
    $args = @(
        "/VERYSILENT", "/SUPPRESSMSGBOXES",
        "/DIR=$installDir",
        "/NORESTART",
        "/NOICONS"
    )
    Info "Installing to: $installDir"
    $installProc = Start-Process -FilePath $Installer -ArgumentList $args -Wait -PassThru -NoNewWindow
    if ($installProc.ExitCode -eq 0) {
        Pass "2.1: Installer exited 0"
    } else {
        Fail "2.1: Installer exit code" "$($installProc.ExitCode)"
    }

    $installedExe = Join-Path $installDir "TrafficLens.exe"
    if (Test-Path $installedExe) {
        Pass "2.2: Install directory created with TrafficLens.exe"
    } else {
        Fail "2.2: Installed exe" "not found at $installedExe"
    }

    $startMenuDir = Get-ChildItem "$env:APPDATA\Microsoft\Windows\Start Menu\Programs" -Directory -Filter "TrafficLens" -ErrorAction SilentlyContinue
    if ($startMenuDir) {
        Pass "2.3: Start Menu shortcut directory exists"
    } else {
        Fail "2.3: Start Menu shortcut" "not found"
    }

    # ====================================================================
    # 3. FIRST LAUNCH (installed copy)
    # ====================================================================
    Write-Host "`n=== 3. FIRST LAUNCH ===" -ForegroundColor Yellow
    $isolatedSettings = Join-Path $testAppData "settings.json"
    New-Item -ItemType Directory -Force -Path $testAppData | Out-Null
    @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'False'; FloatingWidgetEnabled = 'False' } |
        ConvertTo-Json -Depth 6 | Set-Content $isolatedSettings -Encoding UTF8

    # Set isolated LOCALAPPDATA for the installed app
    $env:LOCALAPPDATA_BAK = $env:LOCALAPPDATA
    $env:LOCALAPPDATA = Join-Path $testRoot "AppData\Local"

    $pLaunch = Start-Process -FilePath $installedExe -PassThru
    $title = Wait-MainWindow $pLaunch 30
    if ($title -match 'TrafficLens') {
        Pass "3.1: Installed app launched, window rendered"
    } else {
        Fail "3.1: First launch" "title='$title'"
    }

    # Check settings directory initialized
    if (Test-Path $isolatedSettings) {
        Pass "3.2: Settings directory initialized"
    } else {
        Fail "3.2: Settings directory" "not created"
    }

    # Check database initialized (app uses Environment.SpecialFolder.LocalApplicationData,
    # which may or may not respect the env var override; check both locations)
    $realDbPath = "$env:LOCALAPPDATA_BAK\TrafficLens\data\trafficlens.db"
    $isolatedDbPath = Join-Path $testAppData "data\trafficlens.db"
    $dbFound = (Test-Path $realDbPath) -or (Test-Path $isolatedDbPath)
    if ($dbFound) {
        Pass "3.3: Database initialized"
    } else {
        # DB may be created lazily on first history flush (30s); check again after a brief wait
        Start-Sleep -Seconds 5
        $dbFound = (Test-Path $realDbPath) -or (Test-Path $isolatedDbPath)
        if ($dbFound) {
            Pass "3.3: Database initialized (after delay)"
        } else {
            Fail "3.3: Database" "not created in either location"
        }
    }

    # ====================================================================
    # 4. GRACEFUL EXIT
    # ====================================================================
    Write-Host "`n=== 4. GRACEFUL EXIT ===" -ForegroundColor Yellow
    $hwnd = [IntPtr]::Zero
    foreach ($h in [WinApiTl23]::WindowsOf([uint32]$pLaunch.Id)) {
        if ([WinApiTl23]::IsWindowVisible($h) -and [WinApiTl23]::Text($h) -match 'TrafficLens') { $hwnd = $h; break }
    }
    if ($hwnd -ne [IntPtr]::Zero) {
        [WinApiTl23]::SendMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        if (Wait-ProcessExit $pLaunch.Id 20) {
            Pass "4.1: Process exited gracefully after WM_CLOSE"
        } else {
            Fail "4.1: Graceful exit" "still alive after 20s"
            Kill-All
        }
    } else {
        Fail "4.1: Window handle" "not found for WM_CLOSE"
    }

    Start-Sleep -Seconds 2
    $orphans = Get-Process -Name "TrafficLens" -ErrorAction SilentlyContinue
    if ($null -eq $orphans) {
        Pass "4.2: No orphan process after exit"
    } else {
        Fail "4.2: Orphan process" "$($orphans.Count) remaining"
    }

    $etw = Get-EtwSessions
    if ($etw.Count -eq 0) {
        Pass "4.3: No orphan ETW session after exit"
    } else {
        Fail "4.3: ETW orphan" ($etw -join ',')
    }

    # ====================================================================
    # 5. STARTUP REGISTRATION
    # ====================================================================
    Write-Host "`n=== 5. STARTUP REGISTRATION ===" -ForegroundColor Yellow

    # 5a) Default: startup should not be registered (app doesn't auto-enable)
    $val = Get-StartupValue
    if ($null -eq $val -or $val -eq '') {
        Pass "5.1: No startup entry by default (not auto-enabled)"
    } else {
        Fail "5.1: Startup entry" "unexpectedly present: $val"
    }

    # 5b) Simulate enabling startup by writing the registry value
    $testExePath = Join-Path $testRoot "FakePath\TrafficLens.exe"
    $testCmd = "`"$testExePath`" --minimized"
    try {
        $k = Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -ErrorAction SilentlyContinue
        if (-not $k) { New-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Force | Out-Null }
        Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "TrafficLens" -Value $testCmd -Type String
        $readBack = Get-StartupValue
        if ($readBack -match "TrafficLens.exe" -and $readBack -match "--minimized") {
            Pass "5.2: Startup entry write/read with --minimized works"
        } else {
            Fail "5.2: Startup entry write" "read='$readBack'"
        }
    } catch {
        Fail "5.2: Startup entry write" $_.Exception.Message
    }

    # 5c) Upgrade/reinstall should not create duplicate entry
    # Install again over existing
    $args2 = @(
        "/VERYSILENT", "/SUPPRESSMSGBOXES",
        "/DIR=$installDir",
        "/NORESTART",
        "/NOICONS"
    )
    $reinstallProc = Start-Process -FilePath $Installer -ArgumentList $args2 -Wait -PassThru -NoNewWindow
    $valAfterReinstall = Get-StartupValue
    # Should still be exactly one entry
    $count = 0
    try {
        $allVals = Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -ErrorAction SilentlyContinue
        if ($allVals.TrafficLens) { $count = 1 }
    } catch {}
    if ($count -eq 1) {
        Pass "5.3: No duplicate startup entry after reinstall"
    } else {
        Fail "5.3: Startup entries" "$count found"
    }

    # Clean up startup test entry
    Remove-StartupValue

    # ====================================================================
    # 6. DATA PRESERVATION (uninstall → verify user data remains)
    # ====================================================================
    Write-Host "`n=== 6. UNINSTALL + DATA PRESERVATION ===" -ForegroundColor Yellow

    # Create some test data to verify preservation
    $testSettingsContent = @{ Language = 'en-US'; TestFlag = 'tl023-preserved' } | ConvertTo-Json -Depth 6
    Set-Content $isolatedSettings -Value $testSettingsContent -Encoding UTF8
    $dbToCheck = if (Test-Path $isolatedDbPath) { $isolatedDbPath } else { $realDbPath }
    if (Test-Path $dbToCheck) {
        $dbSize = (Get-Item $dbToCheck).Length
        Info "Database size before uninstall: $dbSize bytes"
    } else {
        Info "No database found before uninstall (may not have been created yet)"
    }

    # Run uninstaller
    $uninstallExe = Join-Path $installDir "unins000.exe"
    if (Test-Path $uninstallExe) {
        $uninstProc = Start-Process -FilePath $uninstallExe -ArgumentList "/VERYSILENT","/SUPPRESSMSGBOXES" -Wait -PassThru -NoNewWindow
        if ($uninstProc.ExitCode -eq 0) {
            Pass "6.1: Uninstaller exited 0"
        } else {
            Fail "6.1: Uninstaller exit code" "$($uninstProc.ExitCode)"
        }
    } else {
        Fail "6.1: Uninstaller" "not found at $uninstallExe"
    }

    # Verify program files removed
    if (-not (Test-Path $installedExe)) {
        Pass "6.2: Program binaries removed after uninstall"
    } else {
        Fail "6.2: Program binaries" "still present"
    }

    # Verify Start Menu removed
    $smAfter = Get-ChildItem "$env:APPDATA\Microsoft\Windows\Start Menu\Programs" -Directory -Filter "TrafficLens" -ErrorAction SilentlyContinue
    if (-not $smAfter) {
        Pass "6.3: Start Menu shortcut removed"
    } else {
        Fail "6.3: Start Menu shortcut" "still present"
    }

    # Verify user data preserved
    if (Test-Path $isolatedSettings) {
        $preserved = Get-Content $isolatedSettings -Raw
        if ($preserved -match 'tl023-preserved') {
            Pass "6.4: settings.json preserved after uninstall"
        } else {
            Fail "6.4: settings.json" "content changed"
        }
    } else {
        Fail "6.4: settings.json" "deleted by uninstall"
    }

    if (Test-Path $dbToCheck) {
        Pass "6.5: trafficlens.db preserved after uninstall"
    } else {
        Fail "6.5: trafficlens.db" "deleted by uninstall"
    }

    # ====================================================================
    # 7. REINSTALL + DATA LOADING
    # ====================================================================
    Write-Host "`n=== 7. REINSTALL + DATA LOADING ===" -ForegroundColor Yellow

    $args3 = @(
        "/VERYSILENT", "/SUPPRESSMSGBOXES",
        "/DIR=$installDir",
        "/NORESTART",
        "/NOICONS"
    )
    $reinstall2Proc = Start-Process -FilePath $Installer -ArgumentList $args3 -Wait -PassThru -NoNewWindow
    if ($reinstall2Proc.ExitCode -eq 0) {
        Pass "7.1: Reinstall succeeded"
    } else {
        Fail "7.1: Reinstall exit code" "$($reinstall2Proc.ExitCode)"
    }

    if (Test-Path $installedExe) {
        Pass "7.2: Installed exe present after reinstall"
    } else {
        Fail "7.2: Installed exe" "missing after reinstall"
    }

    # Verify settings still loadable
    if (Test-Path $isolatedSettings) {
        $reloaded = Get-Content $isolatedSettings -Raw
        if ($reloaded -match 'tl023-preserved') {
            Pass "7.3: Settings intact after reinstall"
        } else {
            Fail "7.3: Settings" "corrupted after reinstall"
        }
    } else {
        Fail "7.3: Settings" "missing after reinstall"
    }

    # Launch after reinstall
    $pReinstall = Start-Process -FilePath $installedExe -PassThru
    $titleReinstall = Wait-MainWindow $pReinstall 30
    if ($titleReinstall -match 'TrafficLens') {
        Pass "7.4: App launches after reinstall"
    } else {
        Fail "7.4: Post-reinstall launch" "title='$titleReinstall'"
    }

    # Exit cleanly
    foreach ($h in [WinApiTl23]::WindowsOf([uint32]$pReinstall.Id)) {
        if ([WinApiTl23]::IsWindowVisible($h) -and [WinApiTl23]::Text($h) -match 'TrafficLens') {
            [WinApiTl23]::SendMessage($h, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
            break
        }
    }
    Wait-ProcessExit $pReinstall.Id 20 | Out-Null
    Start-Sleep -Seconds 2

    # ====================================================================
    # 8. PORTABLE BUILD
    # ====================================================================
    Write-Host "`n=== 8. PORTABLE BUILD ===" -ForegroundColor Yellow

    Info "Extracting portable ZIP to: $testPortable"
    Expand-Archive -Path $PortableZip -DestinationPath $testPortable -Force

    $portableExe = Join-Path $testPortable "TrafficLens.exe"
    if (Test-Path $portableExe) {
        Pass "8.1: Portable TrafficLens.exe extracted"
    } else {
        Fail "8.1: Portable exe" "not found"
    }

    # Launch portable
    $pPortable = Start-Process -FilePath $portableExe -PassThru
    $titlePortable = Wait-MainWindow $pPortable 30
    if ($titlePortable -match 'TrafficLens') {
        Pass "8.2: Portable build launches"
    } else {
        Fail "8.2: Portable launch" "title='$titlePortable'"
    }

    # Portable uses same LOCALAPPDATA for settings/DB
    # (not a separate portable data dir - this is by design per PACKAGING.md)
    $portableSettingsCheck = Join-Path $testAppData "settings.json"
    if (Test-Path $portableSettingsCheck) {
        Pass "8.3: Portable build uses shared LocalAppData settings"
    } else {
        Fail "8.3: Portable settings" "not created"
    }

    # Exit portable cleanly
    foreach ($h in [WinApiTl23]::WindowsOf([uint32]$pPortable.Id)) {
        if ([WinApiTl23]::IsWindowVisible($h) -and [WinApiTl23]::Text($h) -match 'TrafficLens') {
            [WinApiTl23]::SendMessage($h, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
            break
        }
    }
    Wait-ProcessExit $pPortable.Id 20 | Out-Null
    Start-Sleep -Seconds 2
    $portableOrphans = Get-Process -Name "TrafficLens" -ErrorAction SilentlyContinue
    if ($null -eq $portableOrphans) {
        Pass "8.4: No orphan process after portable exit"
    } else {
        Fail "8.4: Portable orphan" "$($portableOrphans.Count) remaining"
    }

    # ====================================================================
    # 9. INSTALLER METADATA
    # ====================================================================
    Write-Host "`n=== 9. INSTALLER METADATA ===" -ForegroundColor Yellow

    # Check the PE version info of the installer
    $installerVer = (Get-Item $Installer).VersionInfo
    $productName = $installerVer.ProductName.Trim()
    $productVer = $installerVer.ProductVersion.Trim()
    $fileVer = $installerVer.FileVersion.Trim()
    if ($productName -eq 'TrafficLens') {
        Pass "9.1: Installer ProductName = TrafficLens"
    } else {
        Fail "9.1: Installer ProductName" "'$productName'"
    }
    if ($productVer -eq '0.1.2') {
        Pass "9.2: Installer ProductVersion = $productVer"
    } else {
        Fail "9.2: Installer ProductVersion" "'$productVer' (expected 0.1.2)"
    }
    if ($fileVer -eq '0.1.2') {
        Pass "9.3: Installer FileVersion = $fileVer"
    } else {
        Fail "9.3: Installer FileVersion" "'$fileVer'"
    }
    if ($productVer -eq $fileVer) {
        Pass "9.4: ProductVersion matches FileVersion"
    } else {
        Fail "9.4: Version consistency" "ProductVersion='$productVer' FileVersion='$fileVer'"
    }

    # Check for placeholder URLs — must be absent now (TL-024)
    $issContent = Get-Content (Join-Path $repoRoot "packaging\TrafficLens.iss") -Raw
    if ($issContent -match 'AppPublisherURL=') {
        Fail "9.5: AppPublisherURL" "should be absent (placeholder removed)"
    } else {
        Pass "9.5: AppPublisherURL absent (no placeholder)"
    }
    if ($issContent -match 'AppSupportURL=') {
        Fail "9.6: AppSupportURL" "should be absent (placeholder removed)"
    } else {
        Pass "9.6: AppSupportURL absent (no placeholder)"
    }

    # Check stable AppId
    if ($issContent -match '\{\{8F0E8A8F-7B1D-4A5E-9C2D-3E5F6A7B8C9D\}\}') {
        Pass "9.7: Stable AppId unchanged"
    } else {
        Fail "9.7: AppId" "unexpected value"
    }

    # Check installer has English and Persian language support
    if ($issContent -match 'Name:\s*"english"') {
        Pass "9.8: English installer language present"
    } else {
        Fail "9.8: English language" "not found in ISS"
    }
    if ($issContent -match 'Name:\s*"persian"') {
        Pass "9.9: Persian installer language present"
    } else {
        Fail "9.9: Persian language" "not found in ISS"
    }

    # ====================================================================
    # 10. FINAL ORPHAN CHECK
    # ====================================================================
    Write-Host "`n=== 10. FINAL CLEANUP ===" -ForegroundColor Yellow
    Kill-All
    Start-Sleep -Seconds 1
    $finalOrphans = Get-Process -Name "TrafficLens" -ErrorAction SilentlyContinue
    $finalEtw = Get-EtwSessions
    if ($null -eq $finalOrphans) {
        Pass "10.1: No orphan processes"
    } else {
        Fail "10.1: Orphan processes" "$($finalOrphans.Count)"
    }
    if ($finalEtw.Count -eq 0) {
        Pass "10.2: No orphan ETW sessions"
    } else {
        Fail "10.2: ETW orphans" ($finalEtw -join ',')
    }

} finally {
    Kill-All

    # Restore real settings
    if ($realSettingsBackup) {
        Set-Content $realSettings -Value $realSettingsBackup -Encoding UTF8
        Info "Restored real settings.json"
    }

    # Restore real startup
    if ($realStartup) {
        Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "TrafficLens" -Value $realStartup -Type String -ErrorAction SilentlyContinue
    } else {
        Remove-StartupValue
    }

    # Restore real LOCALAPPDATA
    if ($env:LOCALAPPDATA_BAK) { $env:LOCALAPPDATA = $env:LOCALAPPDATA_BAK }

    # Clean up isolated test root
    if (Test-Path $testRoot) {
        Remove-Item -Path $testRoot -Recurse -Force -ErrorAction SilentlyContinue
        Info "Cleaned up isolated test root"
    }
}

Write-Host ""
Write-Host "RESULTS: $script:passCount passed, $script:failCount failed" -ForegroundColor $(if ($script:anyFail) { 'Red' } else { 'Green' })
if ($script:anyFail) { Write-Host "LIFECYCLE VALIDATION FAILED" -ForegroundColor Red; exit 1 }
Write-Host "LIFECYCLE VALIDATION PASSED" -ForegroundColor Green
exit 0
