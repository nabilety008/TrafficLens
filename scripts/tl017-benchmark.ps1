param(
    [string]$Exe = "",
    [string]$Settings = "$env:LOCALAPPDATA\TrafficLens\settings.json",
    [int]$SoakMinutes = 5,
    [int]$ScenarioMinutes = 3,
    [int]$TrafficSeconds = 60,
    [string]$OutDir = "$PSScriptRoot\..\artifacts\tl017-baseline",
    [switch]$Counters,
    [switch]$KeepRunning
)
$ErrorActionPreference = 'Stop'

if ([System.Threading.Thread]::CurrentThread.ApartmentState -ne [System.Threading.ApartmentState]::STA) {
    powershell.exe -NoProfile -Sta -ExecutionPolicy Bypass -File $PSCommandPath @args
    exit $LASTEXITCODE
}

if ([string]::IsNullOrWhiteSpace($Exe)) {
    $Exe = "$PSScriptRoot\..\artifacts\publish\win-x64\TrafficLens.exe"
}
if (-not (Test-Path $Exe)) { Write-Host "FATAL: exe not found: $Exe" -ForegroundColor Red; exit 1 }

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$script:isElevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class WinApiTl17 {
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
$script:AeRoot = [System.Windows.Automation.AutomationElement]::RootElement

function Pass([string]$c) { Write-Host "  PASS: $c" -ForegroundColor Green }
function Fail([string]$c, [string]$r) { Write-Host "  FAIL: $c ($r)" -ForegroundColor Red; $script:anyFail = $true }
function Info([string]$c) { Write-Host "  (i) $c" -ForegroundColor DarkGray }
function Write-Note([string]$c) { Write-Host "  * $c" -ForegroundColor DarkCyan }
function Kill-All { Get-Process -Name "TrafficLens" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue }
function Write-SettingsFile($data) { $data | ConvertTo-Json -Depth 6 | Set-Content $Settings -Encoding UTF8 }
function Get-MainHwnd($proc) {
    foreach ($h in [WinApiTl17]::WindowsOf([uint32]$proc.Id)) {
        if ([WinApiTl17]::IsWindowVisible($h) -and [WinApiTl17]::Text($h)) { return $h }
    }
    return [IntPtr]::Zero
}
function Wait-MainWindow($proc, [int]$maxSec) {
    for ($t = 0; $t -lt $maxSec * 20; $t++) {
        if ((Get-MainHwnd $proc) -ne [IntPtr]::Zero) { return $true }
        Start-Sleep -Milliseconds 50
    }
    return $false
}
function Wait-ProcessExit($procId, [int]$maxSec) {
    for ($t = 0; $t -lt $maxSec * 2; $t++) {
        if ($null -eq (Get-Process -Id $procId -ErrorAction SilentlyContinue)) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return $false
}
function Get-EtwSessions { $n = @(); try { foreach ($l in (logman query -ets 2>$null)) { if ($l -match "TrafficLens") { $n += $l.Trim() } } } catch {} return $n }
function Get-WindowElement($proc) {
    $hwnd = Get-MainHwnd $proc
    if ($hwnd -eq [IntPtr]::Zero) { return $null }
    return [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
}
function Invoke-Nav([System.Windows.Automation.AutomationElement]$win, [string]$name) {
    if ($null -eq $win) { return $false }
    try {
        $all = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($el in $all) {
            if ($el.Current.Name -eq $name -and $el.Current.ControlType.Equals([System.Windows.Automation.ControlType]::Button)) {
                $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() | Out-Null
                Start-Sleep -Milliseconds 400
                return $true
            }
        }
    } catch { }
    return $false
}

# ---- process metrics --------------------------------------------------------
function Get-Metrics($procId) {
    $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
    if ($null -eq $p) { return $null }
    return [pscustomobject]@{
        Ts          = [DateTime]::UtcNow.ToString('o')
        ElapsedSec  = [math]::Round($p.TotalProcessorTime.TotalSeconds, 3)
        WS_MB       = [math]::Round($p.WorkingSet64 / 1MB, 2)
        Priv_MB     = [math]::Round($p.PrivateMemorySize64 / 1MB, 2)
        Handles     = $p.HandleCount
        Threads     = @($p.Threads).Count
        Gen0        = [math]::Round($p.PagedSystemMemorySize64 / 1MB, 2)
    }
}
function Csv-Export($rows, $path) {
    $rows | ConvertTo-Csv -NoTypeInformation | Set-Content -LiteralPath $path -Encoding ASCII
}

$script:counterProc = $null
$script:counterOut = ""
function Start-Counters($procId, $outFile) {
    if (-not $Counters) { return }
    $dc = "$env:USERPROFILE\.dotnet\tools\dotnet-counters.exe"
    if (-not (Test-Path $dc)) { Info "dotnet-counters not found at $dc; GC counters skipped"; return }
    $script:counterOut = $outFile
    $script:counterProc = Start-Process -FilePath $dc -ArgumentList @("monitor","--process-id",$procId,"--counters","System.Runtime","--format","csv","-o", "`"$outFile`"") -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 2
}
function Stop-Counters {
    if ($null -ne $script:counterProc -and -not $script:counterProc.HasExited) {
        $script:counterProc.Kill()
        Start-Sleep -Milliseconds 800
    }
    $script:counterProc = $null
}

# ---- traffic generator: parallel download loop to drive real counter deltas ---
function Start-Traffic([int]$seconds) {
    $sb = {
        param($duration)
        $until = (Get-Date).AddSeconds($duration)
        while ((Get-Date) -lt $until) {
            try {
                $big = Invoke-WebRequest -Uri "https://speed.cloudflare.com/__down?bytes=33554432" -UseBasicParsing -TimeoutSec 30
                if ($big.Content.Length -gt 0) { }
            } catch { }
        }
    }
    return (Start-Job -ScriptBlock $sb -ArgumentList $seconds)
}

# =============================================================================
# Run
# =============================================================================
Write-Host "=== TL-017 Baseline Benchmark ===" -ForegroundColor Cyan
Write-Host "exe  : $Exe" -ForegroundColor DarkGray
$ts = Get-Date -Format 'yyyyMMdd-HHmmss'
$runDir = Join-Path $OutDir $ts
New-Item -ItemType Directory -Path $runDir -Force | Out-Null

$hadSettings = Test-Path $Settings
if ($hadSettings) { Copy-Item $Settings "$Settings.tl017.bak" -Force -ErrorAction SilentlyContinue }

$rowsAll = [System.Collections.Generic.List[object]]::new()
$startup = [System.Collections.Generic.List[object]]::new()

function Add-Sample($procId, $stage, $prevCpu, $prevElapsed) {
    $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
    if ($null -eq $p) { return $prevCpu }
    $now = [DateTime]::UtcNow
    $cpuNow = $p.TotalProcessorTime.TotalSeconds
    $cpuPct = 0.0
    if ($null -ne $prevCpu -and $null -ne $prevElapsed) {
        $wall = $now.Subtract($prevElapsed).TotalSeconds
        if ($wall -gt 0) { $cpuPct = [math]::Round([math]::Max(0.0, ($cpuNow - $prevCpu)) / $wall * 100, 3) }
    }
    $row = [pscustomobject]@{
        Stage   = $stage
        Ts      = $now.ToString('HH:mm:ss')
        WS_MB   = [math]::Round($p.WorkingSet64 / 1MB, 2)
        Priv_MB = [math]::Round($p.PrivateMemorySize64 / 1MB, 2)
        Handles = $p.HandleCount
        Threads = @($p.Threads).Count
        CpuPct  = $cpuPct
    }
    $rowsAll.Add($row)
    Write-Note ("{0,-14} WS={1,8}MB Priv={2,8}MB TH={3,3} HD={4,5} CPU={5,6}%" -f $stage, $row.WS_MB, $row.Priv_MB, $row.Threads, $row.Handles, $row.CpuPct)
    return $cpuNow
}

function Settle-Metrics($procId, $stage, $samples) {
    $prevCpu = $null; $prevEl = $null
    foreach ($i in 1..$samples) {
        $prevEl = if ($i -eq 1) { Start-Sleep -Seconds 3; [DateTime]::UtcNow } else { Start-Sleep -Seconds 10; [DateTime]::UtcNow }
        if ($null -eq $prevCpu) {
            $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
            if ($null -ne $p) { $prevCpu = $p.TotalProcessorTime.TotalSeconds }
        } else {
            $prevCpu = Add-Sample $procId $stage $prevCpu $prevEl
        }
    }
}

try {
    # ---------------- startup: cold vs warm ----------------
    Write-Host "`n1) Startup timing (cold vs warm)" -ForegroundColor Yellow
    Kill-All; Start-Sleep -Seconds 3
    Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'False'; CloseToTray = 'False'; FloatingWidgetEnabled = 'False' }
    foreach ($kind in @('cold', 'warm')) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $p = Start-Process -FilePath $Exe -PassThru
        $win = Wait-MainWindow $p 30
        $sw.Stop()
        if (-not $win) { Fail "startup $kind" "no window in 30s"; Kill-All; continue }
        $startup.Add([pscustomobject]@{ Kind = $kind; MsToWindow = $sw.ElapsedMilliseconds; Pid = $p.Id })
        Write-Note ("{0} start: {1} ms to main window" -f $kind, $sw.ElapsedMilliseconds)
        # graceful exit for warm relaunch
        $hwnd = Get-MainHwnd $p
        [WinApiTl17]::SendMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        if (-not (Wait-ProcessExit $p.Id 15)) { Kill-All }
        Start-Sleep -Seconds 2
    }

    # ---------------- idle soak (tray-hidden) ----------------
    Write-Host "`n2) Idle soak: tray-hidden, $SoakMinutes min (15s sampling)" -ForegroundColor Yellow
    Kill-All; Start-Sleep -Seconds 2
    Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'True'; FloatingWidgetEnabled = 'False' }
    $p2 = Start-Process -FilePath $Exe -PassThru
    if (-not (Wait-MainWindow $p2 30)) { Fail "idle launch" "no window" }
    # hide to tray
    [WinApiTl17]::SendMessage((Get-MainHwnd $p2), 0x0112, [IntPtr]0xF020, [IntPtr]::Zero) | Out-Null
    Start-Sleep -Seconds 3
    $countersFile = Join-Path $runDir "gc.csv"
    Start-Counters $p2.Id $countersFile
    $prevCpu = $null; $prevEl = [DateTime]::UtcNow
    $maxSamples = [int]($SoakMinutes * 4)
    for ($i = 0; $i -lt $maxSamples; $i++) {
        Start-Sleep -Seconds 15
        $now = [DateTime]::UtcNow
        $cpuNow = (Get-Process -Id $p2.Id -ErrorAction SilentlyContinue).TotalProcessorTime.TotalSeconds
        $prevCpu = Add-Sample $p2.Id 'idle-tray' $prevCpu $prevEl
        $prevEl = $now
        if ($null -eq (Get-Process -Id $p2.Id -ErrorAction SilentlyContinue)) { Fail "idle soak" "process died"; break }
    }
    Stop-Counters
    Kill-All; Start-Sleep -Seconds 2

    # ---------------- dashboard ----------------
    Write-Host "`n3) Dashboard visible, $ScenarioMinutes min" -ForegroundColor Yellow
    Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'True'; FloatingWidgetEnabled = 'False' }
    $p3 = Start-Process -FilePath $Exe -PassThru
    if (-not (Wait-MainWindow $p3 30)) { Fail "dashboard launch" "no window" }
    Start-Sleep -Seconds 3
    $prevCpu = $null; $prevEl = [DateTime]::UtcNow
    for ($i = 0; $i -lt $ScenarioMinutes * 4; $i++) {
        Start-Sleep -Seconds 15
        $prevCpu = Add-Sample $p3.Id 'dashboard' $prevCpu $prevEl
        $prevEl = [DateTime]::UtcNow
    }
    Kill-All; Start-Sleep -Seconds 2

    # ---------------- Applications + Connections (navigation needs UIA) -------
    $uiaWin = $null
    Write-Host "`n4) Applications page, $ScenarioMinutes min" -ForegroundColor Yellow
    Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'True'; FloatingWidgetEnabled = 'False' }
    $p4 = Start-Process -FilePath $Exe -PassThru
    if (-not (Wait-MainWindow $p4 30)) { Fail "apps launch" "no window" }
    Start-Sleep -Seconds 2
    $uiaWin = Get-WindowElement $p4
    if (-not (Invoke-Nav $uiaWin 'Applications')) { Info "nav to Applications failed" }
    Start-Sleep -Seconds 3
    $prevCpu = $null; $prevEl = [DateTime]::UtcNow
    for ($i = 0; $i -lt $ScenarioMinutes * 4; $i++) {
        Start-Sleep -Seconds 15
        $prevCpu = Add-Sample $p4.Id 'applications' $prevCpu $prevEl
        $prevEl = [DateTime]::UtcNow
    }
    if (-not (Invoke-Nav (Get-WindowElement $p4) 'Connections')) { Info "nav to Connections failed" }
    Info "5) Connections page timer ($ScenarioMinutes min)"
    $prevCpu = $null; $prevEl = [DateTime]::UtcNow
    for ($i = 0; $i -lt $ScenarioMinutes * 4; $i++) {
        Start-Sleep -Seconds 15
        $prevCpu = Add-Sample $p4.Id 'connections' $prevCpu $prevEl
        $prevEl = [DateTime]::UtcNow
    }
    Kill-All; Start-Sleep -Seconds 2

    # ---------------- active traffic ----------------
    Write-Host "`n6) Active traffic on dashboard, $TrafficSeconds s" -ForegroundColor Yellow
    Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'True'; FloatingWidgetEnabled = 'False' }
    $p6 = Start-Process -FilePath $Exe -PassThru
    if (-not (Wait-MainWindow $p6 30)) { Fail "traffic launch" "no window" }
    Start-Sleep -Seconds 3
    $job = Start-Traffic $TrafficSeconds
    $prevCpu = $null; $prevEl = [DateTime]::UtcNow
    $trafficEnd = (Get-Date).AddSeconds($TrafficSeconds)
    while ((Get-Date) -lt $trafficEnd) {
        Start-Sleep -Seconds 10
        $prevCpu = Add-Sample $p6.Id 'traffic' $prevCpu $prevEl
        $prevEl = [DateTime]::UtcNow
    }
    Wait-Job $job -Timeout 90 | Out-Null
    Remove-Job $job -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3
    $prevCpu = Add-Sample $p6.Id 'traffic-settle' $prevCpu $prevEl
    Kill-All; Start-Sleep -Seconds 2

    # ---------------- summary ----------------
    Csv-Export $rowsAll (Join-Path $runDir "metrics.csv")
    Csv-Export $startup (Join-Path $runDir "startup.csv")
    Write-Host "`n=== Baseline summary ===" -ForegroundColor Cyan
    $startup | ForEach-Object { Write-Note ("startup {0}: {1} ms" -f $_.Kind, $_.MsToWindow) }
    $idle = @($rowsAll | Where-Object { $_.Stage -eq 'idle-tray' })
    if ($idle.Count -ge 2) {
        $f = $idle[0]; $l = $idle[-1]
        $wsD = [math]::Round($l.WS_MB - $f.WS_MB, 2)
        Write-Note ("idle-tray: WS {0} -> {1} MB (delta {2}) | Priv {3} -> {4} | threads {5}->{6} | handles {7}->{8}" -f $f.WS_MB, $l.WS_MB, $wsD, $f.Priv_MB, $l.Priv_MB, $f.Threads, $l.Threads, $f.Handles, $l.Handles)
        $cpus = @($idle | Where-Object { $_.CpuPct -gt 0 })
        if ($cpus.Count -gt 0) {
            $cpuAvg = [math]::Round(($cpus | Measure-Object CpuPct -Average).Average, 3)
            $cpuMax = [math]::Round(($cpus | Measure-Object CpuPct -Maximum).Maximum, 3)
            Write-Note ("idle-tray CPU%: avg {0} max {1}" -f $cpuAvg, $cpuMax)
        }
    }
    foreach ($stage in @('dashboard','applications','connections','traffic','traffic-settle')) {
        $rows = @($rowsAll | Where-Object { $_.Stage -eq $stage })
        if ($rows.Count -gt 0) {
            $cpus = @($rows | Where-Object { $_.CpuPct -gt 0 })
            $cpuTxt = if ($cpus.Count -gt 0) { "avg CPU $([math]::Round(($cpus | Measure-Object CpuPct -Average).Average,3))% / max $([math]::Round(($cpus | Measure-Object CpuPct -Maximum).Maximum,3))%" } else { "CPU <0.1%" }
            Write-Note ("{0}: WS {1}MB Priv {2}MB threads {3} handles {4} | {5}" -f $stage, ($rows | Measure-Object WS_MB -Average).Average, ($rows | Measure-Object Priv_MB -Average).Average, ($rows | Measure-Object Threads -Average).Average, ($rows | Measure-Object Handles -Average).Average, $cpuTxt)
        }
    }
    $etw = Get-EtwSessions
    $elevTxt = if ($script:isElevated) { 'elevated' } else { 'non-elevated' }
    Write-Note ("ETW sessions at end: $($etw.Count) ($elevTxt)")
    if ($Counters -and (Test-Path $countersFile)) {
        Write-Note ("dotnet-counters CSV written: $countersFile")
        $gcRows = @(Import-Csv $countersFile | Where-Object { $_.Provider -eq 'System.Runtime' -and $_.Counter -like 'gen-*' })
        Info ("GC counters (first/last): ")
        $gcRows | Select-Object -Last 1 | Format-List Provider,Counter,Value | Out-String | Write-Host
    }
    Write-Note ("Artifacts: $runDir")
} finally {
    Kill-All
    if ($hadSettings -and (Test-Path "$Settings.tl017.bak")) { Copy-Item "$Settings.tl017.bak" $Settings -Force; Remove-Item "$Settings.tl017.bak" -Force }
    elseif (-not $hadSettings) { Remove-Item $Settings -ErrorAction SilentlyContinue }
    Stop-Counters
}

Write-Host ""
if ($script:anyFail) { Write-Host "TL-017 BASELINE: FAIL" -ForegroundColor Red; exit 1 }
Write-Host "TL-017 BASELINE: DONE (data saved above)" -ForegroundColor Green
exit 0