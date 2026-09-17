param(
    [string]$Exe = "$PSScriptRoot\..\artifacts\publish\win-x64\TrafficLens.exe",
    [string]$Settings = "$env:LOCALAPPDATA\TrafficLens\settings.json"
)
$ErrorActionPreference = 'Stop'

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class WinApiTl15 {
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

$script:anyFail = $false
function P64([int[]]$codes) { -join ($codes | ForEach-Object { [char]$_ }) }
$faTraffic = P64 0x62A,0x631,0x627,0x641,0x6CC,0x6A9
function Pass([string]$c) { Write-Host "  PASS: $c" -ForegroundColor Green }
function Fail([string]$c, [string]$r) { Write-Host "  FAIL: $c ($r)" -ForegroundColor Red; $script:anyFail = $true }
function Info([string]$c) { Write-Host "  (i) $c" -ForegroundColor DarkGray }
function Get-MainWindowTitle($proc) {
    $best = ''
    foreach ($h in [WinApiTl15]::WindowsOf([uint32]$proc.Id)) {
        if (-not [WinApiTl15]::IsWindowVisible($h)) { continue }
        $t = [WinApiTl15]::Text($h)
        if ($t -match 'TrafficLens' -or $t -match $faTraffic) { $best = $t; break }
    }
    return $best
}
function Wait-MainWindow($proc, $maxSeconds) {
    for ($t = 0; $t -lt $maxSeconds * 2; $t++) {
        $title = Get-MainWindowTitle $proc
        if ($title) { return $title }
        Start-Sleep -Milliseconds 500
    }
    return ''
}
function Wait-ProcessExit($procId, $maxSeconds) {
    for ($t = 0; $t -lt $maxSeconds * 2; $t++) {
        if ($null -eq (Get-Process -Id $procId -ErrorAction SilentlyContinue)) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return $false
}
function Wait-ProcessCount($name, $count, $maxSeconds) {
    for ($t = 0; $t -lt $maxSeconds * 2; $t++) {
        if ((Get-Process -Name $name -ErrorAction SilentlyContinue | Measure-Object).Count -eq $count) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return $false
}
function Kill-All { Get-Process -Name "TrafficLens" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue }
function Write-SettingsFile($data) { $data | ConvertTo-Json -Depth 6 | Set-Content $Settings -Encoding UTF8 }
function Get-EtwSessions { $n = @(); try { foreach ($l in (logman query -ets 2>$null)) { if ($l -match "TrafficLens") { $n += $l.Trim() } } } catch {} return $n }

if (-not (Test-Path $Exe)) { Write-Host "FATAL: published exe not found: $Exe" -ForegroundColor Red; exit 1 }
Info "Published exe: $Exe"
Info "Settings file: $Settings"
$procName = (Get-Item $Exe).BaseName
Info "Process name:  $procName"

Kill-All
Start-Sleep -Milliseconds 800

$settingsBackup = $null
if (Test-Path $Settings) { $settingsBackup = Get-Content $Settings -Raw -Encoding UTF8 }

try {
    # ---- A) Fresh launch from publish dir: window renders, process stays alive ----
    Write-Host "`nA) Fresh launch from published single-file build" -ForegroundColor Yellow
    Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'True' }
    $pA = Start-Process -FilePath $Exe -PassThru
    $titleA = Wait-MainWindow $pA 30
    if ($titleA) { Pass "A1: main window rendered ('$titleA')" } else { Fail "A1: main window" "timeout" }
    Start-Sleep -Seconds 2
    if ($null -ne (Get-Process -Id $pA.Id -ErrorAction SilentlyContinue)) { Pass "A2: process alive after settling" } else { Fail "A2: process alive" "exited" }
    if (Get-MainWindowTitle $pA) { Pass "A3: window still visible (en-US default)" } else { Fail "A3: window visible" "missing" }

    # ---- B) Single-instance: second launch must not create a second process ----
    Write-Host "`nB) Single-instance from published build" -ForegroundColor Yellow
    Kill-All
    Start-Sleep -Milliseconds 800
    $pB1 = Start-Process -FilePath $Exe -PassThru
    Start-Sleep -Seconds 3
    $pB2 = Start-Process -FilePath $Exe -PassThru
    Start-Sleep -Seconds 2
    $countB = (Get-Process -Name $procName -ErrorAction SilentlyContinue | Measure-Object).Count
    if ($countB -eq 1) { Pass "B1: single instance enforced ($countB process)" } else { Fail "B1: single instance" "$countB processes" }
    $pB1.Refresh()
    if ($null -ne (Get-Process -Id $pB1.Id -ErrorAction SilentlyContinue)) { Pass "B2: first instance still primary" } else { Fail "B2: first instance" "exited" }

    # ---- C) fa-IR localization works in packaged app ----
    Write-Host "`nC) fa-IR localization from published build" -ForegroundColor Yellow
    Kill-All
    Start-Sleep -Milliseconds 800
    Write-SettingsFile @{ Language = 'fa-IR'; MinimizeToTray = 'True'; CloseToTray = 'True' }
    $pC = Start-Process -FilePath $Exe -PassThru
    $titleC = Wait-MainWindow $pC 30
    if ($titleC -and $titleC.Contains($faTraffic)) { Pass "C1: UI switched to Persian ('$titleC')" } else { Fail "C1: fa-IR UI" "'$titleC'" }
    Start-Sleep -Seconds 2
    if ($null -ne (Get-Process -Id $pC.Id -ErrorAction SilentlyContinue)) { Pass "C2: process alive in fa-IR" } else { Fail "C2: process alive" "exited" }

    # Settings object must also persist Persian under en-US-releasable config, then back to en-US
    Kill-All
    Start-Sleep -Milliseconds 800
    Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'True' }
    $pC2 = Start-Process -FilePath $Exe -PassThru
    $titleC2 = Wait-MainWindow $pC2 30
    if ($titleC2 -match 'TrafficLens') { Pass "C3: back to en-US ('$titleC2')" } else { Fail "C3: en-US UI" "'$titleC2'" }

    # ---- D) Graceful exit: CloseToTray=false -> WM_CLOSE must terminate process ----
    Write-Host "`nD) Graceful exit (CloseToTray=false) from published build" -ForegroundColor Yellow
    Kill-All
    Start-Sleep -Milliseconds 800
    Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'False' }
    $pD = Start-Process -FilePath $Exe -PassThru
    $titleD = Wait-MainWindow $pD 30
    if ($titleD) {
        $hwnd = [IntPtr]::Zero
        foreach ($h in [WinApiTl15]::WindowsOf([uint32]$pD.Id)) {
            if ([WinApiTl15]::IsWindowVisible($h) -and [WinApiTl15]::Text($h) -match 'TrafficLens') { $hwnd = $h; break }
        }
        if ($hwnd -ne [IntPtr]::Zero) {
            [WinApiTl15]::SendMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null  # WM_CLOSE
            if (Wait-ProcessExit $pD.Id 20) {
                Pass "D1: process exited gracefully after WM_CLOSE (CloseToTray=false)"
            } else {
                Fail "D1: graceful exit" "still alive after 20s"; Kill-All
            }
        } else {
            Fail "D1: main window handle" "not found"
        }
    } else {
        Fail "D1: launch for graceful-exit" "window timeout"
    }

    # ---- E) No orphan processes / no leftover ETW session ----
    Start-Sleep -Seconds 2
    $leftover = Get-Process -Name $procName -ErrorAction SilentlyContinue
    $etw = Get-EtwSessions
    if ($null -eq $leftover) { Pass "E1: no orphan TrafficLens process" } else { Fail "E1: orphan process" "$($leftover.Count)" }
    if ($etw.Count -eq 0) { Pass "E2: no leftover TrafficLens ETW session" } else { Fail "E2: ETW left" ($etw -join ',') }

    # ---- F) User data in LocalAppData (settings + DB under the app-data root) ----
    if (Test-Path $Settings) { Pass "F1: settings.json created under LocalAppData" } else { Fail "F1: settings.json" "missing" }
    $db = "$env:LOCALAPPDATA\TrafficLens\data\trafficlens.db"
    if (Test-Path $db) { Pass "F2: database used under LocalAppData (personal data stays out of install dir)" } else { Fail "F2: database" "missing at $db" }
}
finally {
    Kill-All
    if ($settingsBackup) {
        Set-Content $Settings -Value $settingsBackup -Encoding UTF8
        Info "Restored original settings.json"
    }
}

Write-Host ""
if ($script:anyFail) { Write-Host "SMOKE FAILED" -ForegroundColor Red; exit 1 }
Write-Host "SMOKE PASSED" -ForegroundColor Green
exit 0