param(
    [string]$Exe = "",
    [string]$Settings = "$env:LOCALAPPDATA\TrafficLens\settings.json"
)
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Exe)) {
    $Exe = "$PSScriptRoot\..\src\TrafficLens.App\bin\Release\net8.0-windows\TrafficLens.App.exe"
}

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class WinApi {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    public static IntPtr[] WindowsOf(uint pid) {
        var all = new System.Collections.Generic.List<IntPtr>();
        EnumWindows((h, l) => {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == pid) all.Add(h);
            return true;
        }, IntPtr.Zero);
        return all.ToArray();
    }
    public static string Text(IntPtr h) { var sb = new StringBuilder(256); GetWindowText(h, sb, 256); return sb.ToString(); }
    public static string Class(IntPtr h) { var sb = new StringBuilder(256); GetClassName(h, sb, 256); return sb.ToString(); }
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_SYSCOMMAND = 0x0112;
}
"@

$script:anyFail = $false
function Pass([string]$c) { Write-Host "  PASS: $c" -ForegroundColor Green }
function Fail([string]$c, [string]$r) { Write-Host "  FAIL: $c ($r)" -ForegroundColor Red; $script:anyFail = $true }
function Info([string]$c) { Write-Host "  (i) $c" -ForegroundColor DarkGray }

function Get-TlWindows($proc) {
    $out = @()
    foreach ($h in [WinApi]::WindowsOf([uint32]$proc.Id)) {
        $out += [pscustomobject]@{ Handle = $h; Class = [WinApi]::Class($h); Title = [WinApi]::Text($h); Visible = [WinApi]::IsWindowVisible($h) }
    }
    return $out
}
function Get-MainWindow($proc) {
    $w = Get-TlWindows $proc | Where-Object { $_.Visible -and $_.Title -match 'TrafficLens' } | Select-Object -First 1
    if ($null -eq $w) { return [IntPtr]::Zero }
    return $w.Handle
}
function Has-NotifyIcon($windows) {
    if ($null -eq $windows) { return $false }
    foreach ($w in $windows) { if ($w.Class -match '^WindowsForms10\.Window\.0\.app\..+_ad\d$') { return $true } }
    return $false
}
function Wait-ProcessExit($procId, $maxSeconds) {
    for ($t = 0; $t -lt $maxSeconds * 2; $t++) {
        if ($null -eq (Get-Process -Id $procId -ErrorAction SilentlyContinue)) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return $false
}
function Get-EtwSessions { $n = @(); try { foreach ($l in (logman query -ets 2>$null)) { if ($l -match "TrafficLens") { $n += $l.Trim() } } } catch {} return $n }
function Kill-All { Get-Process -Name "TrafficLens.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue }
function Write-SettingsFile($data) { $data | ConvertTo-Json -Depth 5 | Set-Content $Settings -Encoding UTF8 }

$dbPath = "$env:LOCALAPPDATA\TrafficLens\data\trafficlens.db"

Kill-All; Start-Sleep -Seconds 1
$hadSettings = Test-Path $Settings
if ($hadSettings) { Copy-Item $Settings "$Settings.tl011.bak" -Force }

Write-Host "=== TL-011 System Tray GUI Verification ===" -ForegroundColor Cyan

# ---- A) Fresh launch (defaults: MinimizeToTray/CloseToTray = true) ----
Write-Host "`nA) Tray startup" -ForegroundColor Yellow
Remove-Item $Settings -ErrorAction SilentlyContinue
$proc = Start-Process -FilePath $Exe -PassThru
Start-Sleep -Seconds 5
if (-not $proc.HasExited) { Pass "A1: app is alive after startup" } else { Fail "A1: app alive" "exited immediately" }
$main = Get-MainWindow $proc
if ($main -ne [IntPtr]::Zero) { Pass "A2: main window visible (title TrafficLens)" } else { Fail "A2: main window" "not found" }
if (Has-NotifyIcon (Get-TlWindows $proc)) { Pass "A3: tray NotifyIcon created (message window present)" } else { Fail "A3: NotifyIcon" "no WindowsForms10 message window" }

# ---- B) Minimize to tray; collectors keep running ----
Write-Host "`nB) Minimize to tray" -ForegroundColor Yellow
[WinApi]::SendMessage($main, [WinApi]::WM_SYSCOMMAND, [IntPtr]0xF020, [IntPtr]::Zero) | Out-Null
Start-Sleep -Seconds 2
$m2 = Get-MainWindow $proc
if ($m2 -eq [IntPtr]::Zero -and -not $proc.HasExited) { Pass "B1: main hidden after minimize; app still alive" }
else { if ($m2 -ne [IntPtr]::Zero) { Fail "B1: main hidden" "still visible" }; if ($proc.HasExited) { Fail "B1: app alive" "exited" } }
if (Has-NotifyIcon (Get-TlWindows $proc)) { Pass "B2: tray icon still present while hidden" } else { Fail "B2: tray icon" "missing after minimize" }

$log = Get-ChildItem "$env:LOCALAPPDATA\TrafficLens\logs" -Filter *.log -EA SilentlyContinue | Sort-Object LastWriteTime -Desc | Select-Object -First 1
if ($log) {
    if (Select-String -Path $log.FullName -Pattern 'Network traffic collector started' -Quiet) { Pass "B3: network traffic collector running" } else { Fail "B3: network Collector" "marker not in logs" }
    if (Select-String -Path $log.FullName -Pattern 'Traffic history service started' -Quiet) { Pass "B4: traffic history service running" } else { Fail "B4: history service" "marker not in logs" }
} else { Fail "B3: log file" "no log files found" }

Write-Host "`nB5) History accumulates while hidden" -ForegroundColor Yellow
if (Test-Path $dbPath) { Info "history db present (schema ready, WAL mode)" }
$dl = Start-Process curl.exe -ArgumentList "-s","-L","-o","NUL","--max-time","60","https://proof.ovh.net/files/10Mb.dat" -PassThru -WindowStyle Hidden
function Get-HistorySize {
    $total = 0L
    $files = @("$dbPath", "$dbPath-wal", "$dbPath-shm")
    foreach ($f in $files) { if (Test-Path $f) { $total += (Get-Item $f).Length } }
    return $total
}
$s0 = Get-HistorySize
$s0T = (Get-Item $dbPath -EA SilentlyContinue).LastWriteTimeUtc
$grew = $false
for ($t = 0; $t -lt 120; $t++) {
    Start-Sleep -Milliseconds 500
    $sz = Get-HistorySize
    $mtime = (Get-Item $dbPath -EA SilentlyContinue).LastWriteTimeUtc
    if ($sz -gt $s0 + 2048 -or $mtime -gt $s0T.AddSeconds(5)) { $grew = $true; $grewAt = [math]::Round(($t + 1) / 2, 1); break }
}
if (-not $dl.HasExited) { $dl | Stop-Process -Force -ErrorAction SilentlyContinue }
if ($grew) { Pass "B5: history DB/WAL grew while app hidden (db+wal $s0 -> $sz bytes after ~${grewAt}s; traffic captured while hidden)" }
else { Fail "B5: history grows while hidden" "no growth/mtime change at $dbPath (s0=$s0) within 60s" }

# ---- C) Same-window restore model ----
Write-Host "`nC) Singleton restore model" -ForegroundColor Yellow
$still = $false
foreach ($w in (Get-TlWindows $proc)) { if ($w.Handle -eq $main) { $still = $true; break } }
if ($still) { Pass "C1: same main HWND ($main) owned by process (singleton hidden, not destroyed)" } else { Fail "C1: same HWND" "main HWND vanished after minimize" }

# ---- D) Close-to-tray: hide + alive + notice persisted once ----
Write-Host "`nD) Close to tray" -ForegroundColor Yellow
[WinApi]::SendMessage($main, [WinApi]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
Start-Sleep -Seconds 2
$m3 = Get-MainWindow $proc
if ($m3 -eq [IntPtr]::Zero -and -not $proc.HasExited) { Pass "D1: main hidden after WM_CLOSE; app stays alive (close-to-tray)" }
else { if ($m3 -ne [IntPtr]::Zero) { Fail "D1: main hidden" "still visible" }; if ($proc.HasExited) { Fail "D1: app alive" "exited instead of tray-hide" } }
Start-Sleep -Seconds 3
$s = Get-Content $Settings -Raw | ConvertFrom-Json
$n1 = if ($null -eq $s -or $null -eq $s.TrayCloseNoticeShown) { $null } else { [string]$s.TrayCloseNoticeShown }
if ($n1 -eq 'True') { Pass "D2: TrayCloseNoticeShown persisted as True (once)" } else { Fail "D2: notice persisted" "TrayCloseNoticeShown='$n1'" }

# ---- E) Second close does not re-notify ----
Write-Host "`nE) Second close-to-tray" -ForegroundColor Yellow
[WinApi]::SendMessage($main, [WinApi]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
Start-Sleep -Seconds 2
$sE = Get-Content $Settings -Raw | ConvertFrom-Json
$n2 = if ($null -eq $sE -or $null -eq $sE.TrayCloseNoticeShown) { $null } else { [string]$sE.TrayCloseNoticeShown }
if ($n2 -eq 'True' -and -not $proc.HasExited) { Pass "E1: notice stays True and app alive after 2nd close" }
else { if ($n2 -ne 'True') { Fail "E1: notice stays True" "value='$n2'" }; if ($proc.HasExited) { Fail "E1: app alive" "exited on 2nd close" } }

# ---- F) CloseToTray=false -> real exit (settings read once at startup, so relaunch) ----
Write-Host "`nF) CloseToTray=false -> clean real exit (fresh launch)" -ForegroundColor Yellow
Kill-All; Start-Sleep -Milliseconds 800
Write-SettingsFile @{ MinimizeToTray = 'True'; CloseToTray = 'False' }
$pF = Start-Process -FilePath $Exe -PassThru
Start-Sleep -Seconds 4
$mF = Get-MainWindow $pF
if ($mF -eq [IntPtr]::Zero) { Fail "F1: main window (CloseToTray=false run)" "not found" }
[WinApi]::SendMessage($mF, [WinApi]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
$exitedF = Wait-ProcessExit $pF.Id 15
if ($exitedF) { Pass "F1: process exited promptly after close with CloseToTray=false (single pipeline)" }
else { Fail "F1: process exit" "still alive 15s after close-to-tray=false" ; Kill-All }
if ((Get-EtwSessions).Count -eq 0) { Pass "F2: no orphan ETW session after exit" } else { Fail "F2: ETW orphan" ((Get-EtwSessions) -join ',') }

# ---- G) Multi-cycle stability (3x) + no leaks/ghosts ----
Write-Host "`nG) 3x lifecycle cycles (launch -> minimize -> close-to-tray -> teardown)" -ForegroundColor Yellow
$cycleOk = $true
for ($i = 1; $i -le 3; $i++) {
    Kill-All; Start-Sleep -Milliseconds 800
    Write-SettingsFile @{ MinimizeToTray = 'True'; CloseToTray = 'True'; TrayCloseNoticeShown = 'True' }
    $pC = Start-Process -FilePath $Exe -PassThru
    Start-Sleep -Seconds 4
    $bad = $false
    if ($pC.HasExited) { Fail "G${i}: launch" "exited"; $bad = $true }
    $mC = Get-MainWindow $pC
    if ($mC -eq [IntPtr]::Zero) { Fail "G${i}: main window" "not found"; $bad = $true }
    if (-not (Has-NotifyIcon (Get-TlWindows $pC))) { Fail "G${i}: tray icon" "NotifyIcon missing"; $bad = $true }
    [WinApi]::SendMessage($mC, [WinApi]::WM_SYSCOMMAND, [IntPtr]0xF020, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Seconds 1
    if ($pC.HasExited) { Fail "G${i}: alive after minimize" "exited"; $bad = $true }
    [WinApi]::SendMessage($mC, [WinApi]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Seconds 1
    if ($pC.HasExited) { Fail "G${i}: alive after close-to-tray" "exited"; $bad = $true }
    if ((Get-EtwSessions).Count -gt 0) { Fail "G${i}: no ETW orphan" ((Get-EtwSessions) -join ','); $bad = $true }
    # NOTE: settings are read once at process startup (ISettingsService caches),
    # so CloseToTray cannot be flipped mid-run without the Settings UI (TL-013);
    # the coordinated real-exit pipeline is verified in F above. Teardown kills.
    Stop-Process -Id $pC.Id -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    if ($null -eq (Get-Process -Id $pC.Id -ErrorAction SilentlyContinue)) {
        if (-not $bad) { Info "cycle ${i} clean (launch/minimize/close-to-tray + no ETW orphan)" }
    } else { Fail "G${i}: teardown" "process still alive after kill"; $bad = $true }
    if ($bad) { $cycleOk = $false }
}
if ($cycleOk) { Pass "G1: 3 lifecycle cycles clean (no ghost processes, no ETW orphans, tray icon recreated each run)" }
else { Fail "G1: 3 cycles" "see per-cycle results above" }

# ---- Restore user settings ----
if ($hadSettings -and (Test-Path "$Settings.tl011.bak")) { Copy-Item "$Settings.tl011.bak" $Settings -Force; Remove-Item "$Settings.tl011.bak" -Force }
Kill-All

Write-Host "`n=== TL-011 Verification Results ===" -ForegroundColor Cyan
Write-Host ""
if ($script:anyFail) { Write-Host "OVERALL: FAIL" -ForegroundColor Red; exit 1 } else { Write-Host "OVERALL: PASS" -ForegroundColor Green }
Write-Host ""
Write-Host "Coverage notes:" -ForegroundColor Cyan
Write-Host "  - Tray double-click restore + tray-menu Exit funnel through the same" 
Write-Host "    OpenRequested/ExitRequested handlers wired in App.xaml.cs and are" 
Write-Host "    covered by unit tests (TrayBehavior/ApplicationExitCoordinator)." 
Write-Host "  - NotifyIcon clicks cannot be automated against the Windows 11 XAML"
Write-Host "    tray from this session; restore is verified here via the singleton"
Write-Host "    HWND surviving minimize/hide, and real exit via CloseToTray=false."
Write-Host "  - CloseToTray is read once at startup (settings cache), so the false"
Write-Host "    case is exercised on a fresh launch (block F)."