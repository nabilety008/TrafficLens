param(
    [string]$Exe = "",
    [string]$Settings = "$env:LOCALAPPDATA\TrafficLens\settings.json",
    [int]$SoakMinutes = 30,
    [int]$NavCycles = 50,
    [int]$WidgetCycles = 25,
    [int]$WindowCycles = 25,
    [int]$RestartCycles = 10,
    [switch]$KeepRunning
)
$ErrorActionPreference = 'Stop'

# UIAutomation requires an STA thread. Relaunch ourselves in STA if needed.
if ([System.Threading.Thread]::CurrentThread.ApartmentState -ne [System.Threading.ApartmentState]::STA) {
    $argList = @('-NoProfile', '-Sta', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    $argList += $args
    & powershell.exe $argList
    exit $LASTEXITCODE
}

if ([string]::IsNullOrWhiteSpace($Exe)) {
    $Exe = "$PSScriptRoot\..\src\TrafficLens.App\bin\Release\net8.0-windows\TrafficLens.App.exe"
}

$script:isElevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class WinApiTl14 {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    public static readonly int GWL_EXSTYLE = -20;
    public static readonly long WS_EX_TOPMOST = 0x00000008;
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    public static IntPtr[] WindowsOf(uint pid) {
        var all = new System.Collections.Generic.List<IntPtr>();
        EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid) all.Add(h); return true; }, IntPtr.Zero);
        return all.ToArray();
    }
    public static string Text(IntPtr h) { var sb = new StringBuilder(512); GetWindowText(h, sb, 512); return sb.ToString(); }
    public static string Class(IntPtr h) { var sb = new StringBuilder(512); GetClassName(h, sb, 512); return sb.ToString(); }
    public static uint WM_CLOSE = 0x0010;
}
"@

$script:anyFail = $false
$script:tlWin = $null
function Pass([string]$c) { Write-Host "  PASS: $c" -ForegroundColor Green }
function Fail([string]$c, [string]$r) { Write-Host "  FAIL: $c ($r)" -ForegroundColor Red; $script:anyFail = $true }
function Info([string]$c) { Write-Host "  (i) $c" -ForegroundColor DarkGray }

function Kill-All { Get-Process -Name "TrafficLens.App" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue }
function Write-SettingsFile($data) { $data | ConvertTo-Json -Depth 6 | Set-Content $Settings -Encoding UTF8 }
function Remove-Settings { Remove-Item $Settings -ErrorAction SilentlyContinue }
function Get-TlWindows($proc) {
    $out = @()
    foreach ($h in [WinApiTl14]::WindowsOf([uint32]$proc.Id)) {
        $out += [pscustomobject]@{ Handle = $h; Class = [WinApiTl14]::Class($h); Title = [WinApiTl14]::Text($h); Visible = [WinApiTl14]::IsWindowVisible($h) }
    }
    return $out
}
function Get-MainWindow($proc) {
    $w = Get-TlWindows $proc | Where-Object { $_.Visible -and $_.Title -match 'TrafficLens' } | Select-Object -First 1
    if ($null -eq $w) { return [IntPtr]::Zero }
    return $w.Handle
}
function Get-WidgetWindow($proc) {
    $w = Get-TlWindows $proc | Where-Object { $_.Visible -and $_.Title -match 'Floating Widget' } | Select-Object -First 1
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
function Wait-Condition([scriptblock]$cond, [int]$maxSeconds, [string]$what) {
    for ($t = 0; $t -lt $maxSeconds * 2; $t++) {
        if (& $cond) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return $false
}
function Get-EtwSessions { $n = @(); try { foreach ($l in (logman query -ets 2>$null)) { if ($l -match "TrafficLens") { $n += $l.Trim() } } } catch {} return $n }
function Get-TlProcessCount { @(Get-Process -Name "TrafficLens.App" -ErrorAction SilentlyContinue).Count }
function Get-LogDirInfo {
    $dir = "$env:LOCALAPPDATA\TrafficLens\logs"
    if (-not (Test-Path $dir)) { return [pscustomobject]@{ Files = 0; Bytes = 0 } }
    $files = @(Get-ChildItem $dir -File -Filter 'trafficlens-*.log' -ErrorAction SilentlyContinue)
    return [pscustomobject]@{ Files = $files.Count; Bytes = ($files | Measure-Object -Property Length -Sum).Sum }
}
function Get-DbInfo {
    $db = "$env:LOCALAPPDATA\TrafficLens\data\trafficlens.db"
    $rows = [pscustomobject]@{ Db = 0; Wal = 0; Shm = 0 }
    foreach ($suffix in @('', '-wal', '-shm')) {
        $p = $db + $suffix
        if (Test-Path $p) {
            $len = (Get-Item $p).Length
            if ($suffix -eq '') { $rows.Db = $len } elseif ($suffix -eq '-wal') { $rows.Wal = $len } else { $rows.Shm = $len }
        }
    }
    return $rows
}
function Get-AppMetrics($procId) {
    $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
    if ($null -eq $p) { return $null }
    return [pscustomobject]@{
        Ts            = [DateTime]::UtcNow
        WorkingSetMB  = [math]::Round($p.WorkingSet64 / 1MB, 1)
        PrivateMB     = [math]::Round($p.PrivateMemorySize64 / 1MB, 1)
        Handles       = $p.HandleCount
        Threads       = $p.Threads.Count
        CpuSeconds    = $p.TotalProcessorTime.TotalSeconds
        WindowCount   = @(Get-TlWindows $p | Where-Object { $_.Visible }).Count
        TrayIcon      = Has-NotifyIcon (Get-TlWindows $p)
        TlsProcesses  = Get-TlProcessCount
    }
}

$script:AeRoot = [System.Windows.Automation.AutomationElement]::RootElement
function Find-WindowElement([string]$nameSubstring, [int]$maxSeconds = 20) {
    for ($t = 0; $t -lt $maxSeconds * 2; $t++) {
        $all = $script:AeRoot.FindAll([System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($el in $all) {
            $n = $el.Current.Name
            if ($n -like "*$nameSubstring*") { return $el }
        }
        Start-Sleep -Milliseconds 500
    }
    return $null
}
function Find-Element($parent, [string[]]$names, [string]$controlType = 'Button', [int]$maxSeconds = 8) {
    for ($t = 0; $t -lt $maxSeconds * 2; $t++) {
        try {
            $all = $parent.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                [System.Windows.Automation.Condition]::TrueCondition)
            foreach ($el in $all) {
                $n = $el.Current.Name
                if ([string]::IsNullOrWhiteSpace($controlType) -or
                    $el.Current.ControlType.Equals([System.Windows.Automation.ControlType]::$controlType)) {
                    foreach ($candidate in $names) {
                        if ($n -eq $candidate) { return $el }
                    }
                }
            }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    return $null
}
function Invoke-Nav([string]$label) {
    if ($null -eq $script:tlWin) { return $false }
    $el = Find-Element $script:tlWin @($label, "Nav$label") 'Button'
    if ($null -eq $el) { return $false }
    try { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() | Out-Null; Start-Sleep -Milliseconds 250; return $true } catch { return $false }
}
function Invoke-Button([string]$name) {
    if ($null -eq $script:tlWin) { return $false }
    $el = Find-Element $script:tlWin @($name) 'Button'
    if ($null -eq $el) { return $false }
    try { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() | Out-Null; Start-Sleep -Milliseconds 250; return $true } catch { return $false }
}
function Start-NewInstance {
    return Start-Process -FilePath $Exe -PassThru
}
function Start-Seconds([double]$sec) { Start-Sleep -Seconds $sec }
function Wait-MainWindow($proc, [int]$maxSeconds) {
    return (Wait-Condition { (Get-MainWindow $proc) -ne [IntPtr]::Zero } $maxSeconds 'main window')
}

$dbPath = "$env:LOCALAPPDATA\TrafficLens\data\trafficlens.db"
$logsDir = "$env:LOCALAPPDATA\TrafficLens\logs"
$logFile = "$env:LOCALAPPDATA\TrafficLens\logs\trafficlens-$(Get-Date -Format 'yyyy-MM-dd').log"
$navPages = @('Dashboard', 'Applications', 'Connections', 'History', 'Alerts', 'Settings')

Write-Host "=== TL-014 Stability & Performance Verification ===" -ForegroundColor Cyan
Write-Host "exe        : $Exe" -ForegroundColor DarkGray
Write-Host "soak       : $SoakMinutes min | nav:$NavCycles widget:$WidgetCycles window:$WindowCycles restart:$RestartCycles" -ForegroundColor DarkGray
Write-Host "elevated   : $script:isElevated (affects ETW process-monitor expectations)" -ForegroundColor DarkGray
Write-Host ""

Kill-All; Start-Seconds 1
$hadSettings = Test-Path $Settings
if ($hadSettings) { Copy-Item $Settings "$Settings.tl014.bak" -Force -ErrorAction SilentlyContinue }

# ---------------------------------------------------------------- A) Single instance + activation
Write-Host "A) Single instance guard + window activation" -ForegroundColor Yellow
Remove-Settings
Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'True'; FloatingWidgetEnabled = 'False' }
$pA = Start-NewInstance
if (Wait-MainWindow $pA 20) { Pass "A1: primary instance launched, main window shown" } else { Fail "A1: primary launch" "no window in 20s" }
if ((Get-TlProcessCount) -eq 1) { Pass "A2: exactly one TrafficLens process after first launch" } else { Fail "A2: process count" "count=$(Get-TlProcessCount)" }

$pA2 = Start-NewInstance
$secondExited = Wait-ProcessExit $pA2.Id 10
if (-not $pA2.HasExited -and (Get-Process -Id $pA2.Id -ErrorAction SilentlyContinue) -eq $null) { $secondExited = $true }
if ($secondExited) { Pass "A3: second launch exits cleanly (single-instance guard)" } else { Fail "A3: second instance" "still alive after 10s" }
Start-Seconds 1
if (-not $pA.HasExited -and (Get-TlProcessCount) -eq 1) { Pass "A4: primary unaffected, still exactly one process" } else { Fail "A4: primary after secondary" "primaryExited=$($pA.HasExited) count=$(Get-TlProcessCount)" }

# hide primary window to tray first (fresh window, no pending activation), then
# confirm a second launch re-activates it
Start-Seconds 1
[WinApiTl14]::SendMessage((Get-MainWindow $pA), 0x0112, [IntPtr]0xF020, [IntPtr]::Zero) | Out-Null
$trayGone = Wait-Condition { (Get-MainWindow $pA) -eq [IntPtr]::Zero } 5 'window hidden'
if ($trayGone -and -not $pA.HasExited) { Pass "A5: primary hidden to tray (SC_MINIMIZE), process alive" } else { Fail "A5: hide to tray" "visible=$(Get-MainWindow $pA) exited=$($pA.HasExited)" }

$pA3 = Start-NewInstance
if (Wait-ProcessExit $pA3.Id 8) { Info "A6: re-launch exited; awaiting activation" } else { Fail "A6: second instance" "still alive after 8s" }
$restored = Wait-Condition { (Get-MainWindow $pA) -ne [IntPtr]::Zero } 10 'window restored'
if ($restored -and -not $pA.HasExited) { Pass "A6: second launch restored+activated the primary window" } else { Fail "A6: activation restore" "restored=$restored exited=$($pA.HasExited)" }
if ((Get-TlProcessCount) -eq 1) { Pass "A7: still exactly one process after activation cycle" } else { Fail "A7: process count after activation" "count=$(Get-TlProcessCount)" }
Kill-All; Start-Seconds 1

# ---------------------------------------------------------------- B) Navigation stress
Write-Host "`nB) Navigation stress ($NavCycles switches across all pages)" -ForegroundColor Yellow
Remove-Settings
Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'True'; FloatingWidgetEnabled = 'False' }
$pB = Start-NewInstance
if (-not (Wait-MainWindow $pB 20)) { Fail "B0: launch" "no window" }
$script:tlWin = Find-WindowElement 'TrafficLens' 10
$navFailures = 0
for ($i = 1; $i -le $NavCycles; $i++) {
    $page = $navPages[($i - 1) % $navPages.Count]
    if (-not (Invoke-Nav $page)) { $navFailures++ }
}
if ($navFailures -eq 0) { Pass "B1: $NavCycles navigation switches invoked" } else { Fail "B1: navigation" "$navFailures/$NavCycles failed" }
if (-not $pB.HasExited -and (Get-MainWindow $pB) -ne [IntPtr]::Zero) { Pass "B2: app alive with window after navigation stress" } else { Fail "B2: alive after nav" "exited=$($pB.HasExited)" }
$errorCount = 0
$errorLines = @()
if (Test-Path $logFile) {
    $errorLines = @(Select-String -Path $logFile -Pattern '"level":"Error"|"level":"Critical"|UnhandledException' -ErrorAction SilentlyContinue)
    $errorCount = $errorLines.Count
}
if ($errorCount -eq 0) { Pass "B3: no Error/Critical/Unhandled entries logged during navigation stress" } else {
    Fail "B3: error logs" "count=$errorCount"
    $errorLines | Select-Object -Last 5 | ForEach-Object { Info "  log: $($_.Line)" }
}
Kill-All; Start-Seconds 1

# ---------------------------------------------------------------- C) Widget stress
Write-Host "`nC) Floating widget show/hide stress ($WidgetCycles cycles)" -ForegroundColor Yellow
Remove-Settings
Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'True'; FloatingWidgetEnabled = 'False' }
$pC = Start-NewInstance
if (-not (Wait-MainWindow $pC 20)) { Fail "C0: launch" "no window" }
$script:tlWin = Find-WindowElement 'TrafficLens' 10
if (-not (Invoke-Nav 'Settings')) { Fail "C0: open Settings" "nav" }
$widgetFails = 0
for ($i = 1; $i -le $WidgetCycles; $i++) {
    Invoke-Button 'SettingsShowWidget' | Out-Null
    Start-Seconds 0.3
    if ((Get-WidgetWindow $pC) -eq [IntPtr]::Zero) { $widgetFails++ }
    Invoke-Button 'SettingsHideWidget' | Out-Null
    Start-Seconds 0.3
    if ((Get-WidgetWindow $pC) -ne [IntPtr]::Zero) { $widgetFails++ }
}
if ($widgetFails -eq 0) { Pass "C1: $WidgetCycles show/hide cycles, widget followed every toggle" } else { Fail "C1: widget toggles" "$widgetFails/$($WidgetCycles * 2) transitions wrong" }
Invoke-Button 'SettingsShowWidget' | Out-Null
Start-Seconds 0.5
if ((Get-WidgetWindow $pC) -ne [IntPtr]::Zero) { Pass "C2: widget visible after final show" } else { Fail "C2: widget final state" "not visible" }
if (-not $pC.HasExited) { Pass "C3: app process stable after widget stress" } else { Fail "C3: process alive" "exited" }
Kill-All; Start-Seconds 1

# ---------------------------------------------------------------- D) Tray / main-window activation stress
Write-Host "`nD) Tray hide + activation restore stress ($WindowCycles cycles)" -ForegroundColor Yellow
Remove-Settings
Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'True'; FloatingWidgetEnabled = 'False' }
$pD = Start-NewInstance
if (-not (Wait-MainWindow $pD 20)) { Fail "D0: launch" "no window" }
$winFails = 0
for ($i = 1; $i -le $WindowCycles; $i++) {
    [WinApiTl14]::SendMessage((Get-MainWindow $pD), 0x0112, [IntPtr]0xF020, [IntPtr]::Zero) | Out-Null
    if (-not (Wait-Condition { (Get-MainWindow $pD) -eq [IntPtr]::Zero } 5 'hidden')) { $winFails++; continue }
    if ($pD.HasExited) { $winFails++; continue }
    $react = Start-NewInstance
    if (-not (Wait-ProcessExit $react.Id 8)) { $winFails++; continue }
    if (-not (Wait-Condition { (Get-MainWindow $pD) -ne [IntPtr]::Zero } 8 'restored')) { $winFails++; continue }
}
if ($winFails -eq 0) { Pass "D1: $WindowCycles hide/restore cycles succeeded" } else { Fail "D1: tray/window cycles" "$winFails/$WindowCycles failed" }
if (-not $pD.HasExited -and (Get-TlProcessCount) -eq 1) { Pass "D2: single process stable after activation stress" } else { Fail "D2: process count" "exited=$($pD.HasExited) count=$(Get-TlProcessCount)" }
Kill-All; Start-Seconds 1

# ---------------------------------------------------------------- E) Startup/shutdown loop + ETW cleanup
Write-Host "`nE) Startup/shutdown loop ($RestartCycles cycles, clean peer exit, no ETW orphans)" -ForegroundColor Yellow
Remove-Settings
Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'False'; CloseToTray = 'False'; FloatingWidgetEnabled = 'False' }
$exitFails = 0
$etwFails = 0
for ($i = 1; $i -le $RestartCycles; $i++) {
    $pE = Start-NewInstance
    if (-not (Wait-MainWindow $pE 15)) { $exitFails++; Kill-All; continue }
    [WinApiTl14]::SendMessage((Get-MainWindow $pE), [WinApiTl14]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    if (-not (Wait-ProcessExit $pE.Id 15)) { $exitFails++; Kill-All }
    Start-Seconds 0.5
    $etw = Get-EtwSessions
    if ($etw.Count -gt 0) { $etwFails++; Info "ETW leftover: $($etw -join ',')" }
}
if ($exitFails -eq 0) { Pass "E1: $RestartCycles clean launches+graceful exits" } else { Fail "E1: restart cycles" "$exitFails/$RestartCycles failed" }
if ($etwFails -eq 0) { Pass "E2: no orphan ETW sessions after any exit" } else { Fail "E2: ETW orphans" "seen $etwFails times" }
if ((Get-TlProcessCount) -eq 0) { Pass "E3: no lingering processes after loop (mutex released)" } else { Fail "E3: lingering processes" "count=$(Get-TlProcessCount)" }

# ---------------------------------------------------------------- F) Soak test
Write-Host "`nF) Soak test ($SoakMinutes minutes idle, 30s sampling)" -ForegroundColor Yellow
Remove-Settings
Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'True'; FloatingWidgetEnabled = 'False' }
$pF = Start-NewInstance
if (-not (Wait-MainWindow $pF 20)) { Fail "F0: launch" "no window" }
Start-Seconds 3

$samples = @()
$watch = [System.Diagnostics.Stopwatch]::StartNew()
$lastCpu = $null
$lastWall = $watch.Elapsed.TotalSeconds
$maxSamples = [int]($SoakMinutes * 2) + 5

# first sample right after warm-up so short runs still produce >=3 samples
$warm = Get-AppMetrics $pF.Id
if ($null -ne $warm) {
    $warm | Add-Member -NotePropertyName CpuPct -NotePropertyValue $null
    $warm | Add-Member -NotePropertyName DbSizeMB -NotePropertyValue ([math]::Round((Get-DbInfo).Db / 1MB, 1))
    $warm | Add-Member -NotePropertyName WalSizeMB -NotePropertyValue ([math]::Round((Get-DbInfo).Wal / 1MB, 1))
    $warm | Add-Member -NotePropertyName LogFiles -NotePropertyValue (Get-LogDirInfo).Files
    $warm | Add-Member -NotePropertyName LogBytesMB -NotePropertyValue ([math]::Round((Get-LogDirInfo).Bytes / 1MB, 1))
    $warm | Add-Member -NotePropertyName EtwSessions -NotePropertyValue @(Get-EtwSessions).Count
    $samples += $warm
    $lastCpu = $warm.CpuSeconds
}

while ($watch.Elapsed.TotalMinutes -lt $SoakMinutes -and $samples.Count -lt $maxSamples) {
    Start-Seconds 30
    $m = Get-AppMetrics $pF.Id
    if ($null -eq $m) { Fail "F1: process disappeared during soak" "pid=$($pF.Id)"; break }
    $wallNow = $watch.Elapsed.TotalSeconds
    if ($null -ne $lastCpu) {
        $cpuDelta = [math]::Max(0, $m.CpuSeconds - $lastCpu)
        $wallDelta = [math]::Max(0.001, $wallNow - $lastWall)
        $m | Add-Member -NotePropertyName CpuPct -NotePropertyValue ([math]::Round($cpuDelta / $wallDelta * 100, 2))
    } else {
        $m | Add-Member -NotePropertyName CpuPct -NotePropertyValue $null
    }
    $m | Add-Member -NotePropertyName DbSizeMB -NotePropertyValue ([math]::Round((Get-DbInfo).Db / 1MB, 1))
    $m | Add-Member -NotePropertyName WalSizeMB -NotePropertyValue ([math]::Round((Get-DbInfo).Wal / 1MB, 1))
    $m | Add-Member -NotePropertyName LogFiles -NotePropertyValue (Get-LogDirInfo).Files
    $m | Add-Member -NotePropertyName LogBytesMB -NotePropertyValue ([math]::Round((Get-LogDirInfo).Bytes / 1MB, 1))
    $m | Add-Member -NotePropertyName EtwSessions -NotePropertyValue @(Get-EtwSessions).Count
    $samples += $m
    $lastCpu = $m.CpuSeconds
    $lastWall = $wallNow
    $alive = (Get-Process -Id $pF.Id -ErrorAction SilentlyContinue) -ne $null
    $oneProc = (Get-TlProcessCount) -eq 1
    if (-not $alive -or -not $oneProc) { Fail "F1: process/app anomaly during soak" "alive=$alive singleProc=$oneProc sample=$($samples.Count)" }
}
$watch.Stop()

if ($samples.Count -ge 3) {
    $first = $samples[0]; $last = $samples[-1]
    $cpuSamples = @($samples | Where-Object { $null -ne $_.CpuPct })
    $maxCpu = if ($cpuSamples.Count -gt 0) { ($cpuSamples | Measure-Object -Property CpuPct -Maximum).Maximum } else { 0 }
    $avgCpu = if ($cpuSamples.Count -gt 0) { [math]::Round(($cpuSamples | Measure-Object -Property CpuPct -Average).Average, 2) } else { 0 }
    $wsGrowth = [math]::Round($last.WorkingSetMB - $first.WorkingSetMB, 1)
    $privGrowth = [math]::Round($last.PrivateMB - $first.PrivateMB, 1)
    $handleGrowth = $last.Handles - $first.Handles
    $threadGrowth = $last.Threads - $first.Threads
    $walGrowthMB = [math]::Round($last.WalSizeMB - $first.WalSizeMB, 2)
    $logFilesGrowth = $last.LogFiles - $first.LogFiles
    $logBytesGrowthMB = [math]::Round($last.LogBytesMB - $first.LogBytesMB, 2)

    Write-Host "  Soak summary (elapsed $([math]::Round($watch.Elapsed.TotalMinutes,1)) min, $($samples.Count) samples):" -ForegroundColor DarkGray
    Write-Host ("    {0,-12} {1,10} {2,10} {3,10}" -f 'metric', 'start', 'end', 'delta') -ForegroundColor DarkGray
    Write-Host ("    {0,-12} {1,10} {2,10} {3,10}" -f 'WorkingSetMB', $first.WorkingSetMB, $last.WorkingSetMB, $wsGrowth) -ForegroundColor DarkGray
    Write-Host ("    {0,-12} {1,10} {2,10} {3,10}" -f 'PrivateMB', $first.PrivateMB, $last.PrivateMB, $privGrowth) -ForegroundColor DarkGray
    Write-Host ("    {0,-12} {1,10} {2,10} {3,10}" -f 'Handles', $first.Handles, $last.Handles, $handleGrowth) -ForegroundColor DarkGray
    Write-Host ("    {0,-12} {1,10} {2,10} {3,10}" -f 'Threads', $first.Threads, $last.Threads, $threadGrowth) -ForegroundColor DarkGray
    Write-Host ("    {0,-12} {1,10} {2,10} {3,10}" -f 'WAL MB', $first.WalSizeMB, $last.WalSizeMB, $walGrowthMB) -ForegroundColor DarkGray
    Write-Host ("    {0,-12} {1,10} {2,10} {3,10}" -f 'Logs MB', $first.LogBytesMB, $last.LogBytesMB, $logBytesGrowthMB) -ForegroundColor DarkGray
    Write-Host "    Avg CPU single-core: $avgCpu% (max $maxCpu%)" -ForegroundColor DarkGray

    $failures = @()
    if ($wsGrowth -gt 64) { $failures += "WS growth ${wsGrowth}MB > 64MB" }
    if ($privGrowth -gt 128) { $failures += "Private growth ${privGrowth}MB > 128MB" }
    if ($handleGrowth -gt 3000) { $failures += "Handle growth ${handleGrowth} > 3000" }
    if ($threadGrowth -gt 30) { $failures += "Thread growth ${threadGrowth} > 30" }
    if ($maxCpu -gt 15) { $failures += "peak CPU ${maxCpu}% > 15% single core" }
    if ($walGrowthMB -gt 25) { $failures += "WAL growth ${walGrowthMB}MB > 25MB" }
    if ($logFilesGrowth -ne 0) { $failures += "log file count +${logFilesGrowth} (retention should hold at 0)" }
    if ($failures.Count -eq 0) { Pass "F2: bounded resource trends over soak (no leak / no runaway growth)" }
    else { Fail "F2: soak bounds" ($failures -join '; ') }
} else {
    Fail "F1: soak" "not enough samples ($($samples.Count))"
}
$etwDuringEnd = Get-EtwSessions
$expectedEtws = if ($script:isElevated) { 1 } else { 0 }
if ($etwDuringEnd.Count -eq $expectedEtws) {
    if ($script:isElevated) { Pass "F3: exactly the app's own ETW session active during soak" }
    else { Pass "F3: no ETW session (running non-elevated; PermissionDenied path, UI stays usable)" }
} else { Fail "F3: ETW during soak" "sessions=$($etwDuringEnd.Count) expected=$expectedEtws (elevated=$($script:isElevated))" }
Kill-All; Start-Seconds 1
if ((Get-EtwSessions).Count -eq 0) { Pass "F4: no orphan ETW sessions after soak shutdown" } else { Fail "F4: ETW orphan after soak" ((Get-EtwSessions) -join ',') }
if ((Get-TlProcessCount) -eq 0) { Pass "F5: no lingering processes after soak" } else { Fail "F5: lingering after soak" "count=$(Get-TlProcessCount)" }

# ---------------------------------------------------------------- G) Final relaunch sanity + restore
Write-Host "`nG) Final relaunch sanity (guard released after clean exits)" -ForegroundColor Yellow
Remove-Settings
Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'False'; CloseToTray = 'False'; FloatingWidgetEnabled = 'False' }
$pG = Start-NewInstance
if (Wait-MainWindow $pG 20) { Pass "G1: relaunch after all cycles works" } else { Fail "G1: relaunch" "no window" }
if ((Get-TlProcessCount) -eq 1) { Pass "G2: exactly one process" } else { Fail "G2: process count" "count=$(Get-TlProcessCount)" }
if (-not $KeepRunning) {
    [WinApiTl14]::SendMessage((Get-MainWindow $pG), [WinApiTl14]::WM_CLOSE, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    if (Wait-ProcessExit $pG.Id 15) { Pass "G3: final clean exit" } else { Fail "G3: final exit" "still alive"; Kill-All }
    Start-Seconds 1
    if ((Get-EtwSessions).Count -eq 0) { Pass "G4: no ETW orphan at end" } else { Fail "G4: ETW orphan" ((Get-EtwSessions) -join ',') }
}

# ---------------------------------------------------------------- Restore
if ($hadSettings -and (Test-Path "$Settings.tl014.bak")) { Copy-Item "$Settings.tl014.bak" $Settings -Force; Remove-Item "$Settings.tl014.bak" -Force }
if (-not $hadSettings) { Remove-Settings }
Kill-All

Write-Host "`n=== TL-014 Stability Verification Results ===" -ForegroundColor Cyan
if ($script:anyFail) { Write-Host "OVERALL: FAIL" -ForegroundColor Red; exit 1 }
Write-Host "OVERALL: PASS" -ForegroundColor Green
Write-Host ""
Write-Host "Snapshot (last soak sample):" -ForegroundColor Cyan
if ($samples.Count -gt 0) { $s = $samples[-1]; Write-Host ("  WS=$($s.WorkingSetMB)MB Private=$($s.PrivateMB)MB Handles=$($s.Handles) Threads=$($s.Threads) windows=$($s.WindowCount) tray=$($s.TrayIcon)") -ForegroundColor DarkGray }
Write-Host ""
Write-Host "Coverage notes:" -ForegroundColor Cyan
Write-Host "  - Single-instance guard (mutex) + window activation exercised in-process via real second launches."
Write-Host "  - Memory/collection boundedness comes from the code audit: TrafficSampleBuffer(1320)," -ForegroundColor DarkGray
Write-Host "    ProcessTrafficAccountingEngine(4096), ConnectionProcessResolver(~512), ProcessIconResolver(128)," -ForegroundColor DarkGray
Write-Host "    AlertHistoryBuffer(100), SQLite 90-day prune, and log retention (14 days, FileLoggerProvider)." -ForegroundColor DarkGray
Write-Host "  - Event subscriptions are symmetric (subscribe in ctor / unsubscribe in Dispose; singletons), so" -ForegroundColor DarkGray
Write-Host "    page navigation toggles visibility only and never re-creates ViewModels." -ForegroundColor DarkGray
Write-Host "  - Sleep/resume is covered deterministically by SpeedRateTrackerTests.LongGapAfterSleep..." -ForegroundColor DarkGray