<#
.SYNOPSIS
    TL-017 Phase 6 Long-Run Stability Soak
.DESCRIPTION
    Runs TrafficLens for an extended period, sampling resource metrics at regular intervals.
    Designed to detect memory leaks, handle leaks, thread leaks, runaway CPU, etc.
.PARAMETER Exe
    Path to TrafficLens.exe
.PARAMETER DurationMinutes
    Total soak duration in minutes (default 120 = 2 hours)
.PARAMETER IntervalSeconds
    Sampling interval in seconds (default 30)
.PARAMETER Minimized
    Start minimized to tray (default true for idle tray soak)
.PARAMETER OutDir
    Output directory for CSV results
.PARAMETER Elevated
    Run elevated (for ETW collector validation)
#>
param(
    [string]$Exe = "C:\Users\ali\Documents\New folder\TrafficLens\artifacts\publish\win-x64\TrafficLens.exe",
    [int]$DurationMinutes = 120,
    [int]$IntervalSeconds = 30,
    [switch]$Minimized = $true,
    [string]$OutDir = "C:\Users\ali\Documents\New folder\TrafficLens\artifacts\tl017-soak",
    [switch]$Elevated = $false
)

$ErrorActionPreference = 'Stop'
if ([System.Threading.Thread]::CurrentThread.ApartmentState -ne [System.Threading.ApartmentState]::STA) {
    powershell.exe -NoProfile -Sta -ExecutionPolicy Bypass -File $PSCommandPath @args
    exit $LASTEXITCODE
}

function Write-Log { param([string]$Message) Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" }

# Clean up any existing TrafficLens processes
Get-Process -Name TrafficLens -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Prepare output
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$csvPath = Join-Path $OutDir ("tl017-soak-{0}.csv" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
$logPath = Join-Path $OutDir ("tl017-soak-{0}.log" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))

$header = "Timestamp,UptimeMinutes,WS_MB,Priv_MB,CPU_Pct,Threads,Handles,Gen0,Gen1,Gen2,Alloc_MB_per_sec,DB_KB,WAL_KB"
$header | Set-Content -Path $csvPath -Encoding ASCII

Write-Log "Starting TL-017 Phase 6 soak"
Write-Log "Duration: $DurationMinutes minutes, Interval: $IntervalSeconds seconds"
Write-Log "Exe: $Exe"
Write-Log "Minimized: $Minimized"
Write-Log "Elevated: $Elevated"
Write-Log "Output: $csvPath"

$env:TRAFFICLENS_STARTUP_TRACE = '0'  # Disable startup trace for soak

$args = @()
if ($Minimized) { $args += '--minimized' }

$proc = Start-Process -FilePath $Exe -ArgumentList $args -PassThru
$procId = $proc.Id
$startTime = Get-Date
$prevCpu = 0
$prevTime = $startTime

Write-Log "TrafficLens started with PID $procId"

try {
    $deadline = $startTime.AddMinutes($DurationMinutes)
    $interval = [TimeSpan]::FromSeconds($IntervalSeconds)

    while ((Get-Date) -lt $deadline) {
        $pr = Get-Process -Id $procId -ErrorAction SilentlyContinue
        if (-not $pr) {
            Write-Log "Process $procId exited unexpectedly"
            break
        }

        $now = Get-Date
        $uptimeMin = [math]::Round(($now - $startTime).TotalMinutes, 1)
        $cpu = $pr.TotalProcessorTime.TotalSeconds
        $cpuPct = 0.0
        if ($prevTime -ne $startTime) {
            $wall = ($now - $prevTime).TotalSeconds
            if ($wall -gt 0) { $cpuPct = [math]::Round([math]::Max(0.0, ($cpu - $prevCpu)) / $wall * 100, 2) }
        }

        $ws = [math]::Round($pr.WorkingSet64 / 1MB, 2)
        $priv = [math]::Round($pr.PrivateMemorySize64 / 1MB, 2)
        $threads = @($pr.Threads).Count
        $handles = $pr.HandleCount

        # Try to get GC metrics via dotnet-counters if available
        $gen0 = 0; $gen1 = 0; $gen2 = 0; $allocRate = 0
        # Note: dotnet-counters requires the process to be running and accessible
        # For now we'll leave these as 0 and rely on periodic manual capture if needed

        # DB sizes
        $dbDir = Join-Path $env:LOCALAPPDATA 'TrafficLens'
        $dbSize = 0; $walSize = 0
        $dbFile = Join-Path $dbDir 'history.db'
        $walFile = $dbFile + '-wal'
        if (Test-Path $dbFile) { $dbSize = [math]::Round((Get-Item $dbFile).Length / 1KB, 1) }
        if (Test-Path $walFile) { $walSize = [math]::Round((Get-Item $walFile).Length / 1KB, 1) }

        $row = "$($now.ToString('yyyy-MM-dd HH:mm:ss')),$uptimeMin,$ws,$priv,$cpuPct,$threads,$handles,$gen0,$gen1,$gen2,$allocRate,$dbSize,$walSize"
        $row | Add-Content -Path $csvPath -Encoding ASCII

        Write-Log "t=$uptimeMin min  WS=$ws MB  Priv=$priv MB  CPU=$cpuPct%  Threads=$threads  Handles=$handles  DB=$dbSize KB  WAL=$walSize KB"

        $prevCpu = $cpu
        $prevTime = $now
        Start-Sleep -Seconds $IntervalSeconds
    }
}
finally {
    Write-Log "Soak duration reached or interrupted. Stopping TrafficLens..."
    Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3
    Get-Process -Name TrafficLens -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Log "Soak complete. Results: $csvPath"
}

# Quick summary
$data = Import-Csv $csvPath
$first = $data[0]; $last = $data[-1]
$wsDrift = [math]::Round($last.WS_MB - $first.WS_MB, 2)
$privDrift = [math]::Round($last.Priv_MB - $first.Priv_MB, 2)
$cpuAvg = [math]::Round(($data | Measure-Object CPU_Pct -Average).Average, 2)
$cpuMax = [math]::Round(($data | Measure-Object CPU_Pct -Maximum).Maximum, 2)
$handlesMax = ($data | Measure-Object Handles -Maximum).Maximum
$threadsMax = ($data | Measure-Object Threads -Maximum).Maximum

Write-Log "=== SOAK SUMMARY ==="
Write-Log "Duration: $($data[-1].UptimeMinutes) minutes"
Write-Log "WS: $($first.WS_MB) -> $($last.WS_MB) MB (drift $wsDrift MB)"
Write-Log "Priv: $($first.Priv_MB) -> $($last.Priv_MB) MB (drift $privDrift MB)"
Write-Log "CPU avg: $cpuAvg%, max: $cpuMax%"
Write-Log "Handles: max $handlesMax"
Write-Log "Threads: max $threadsMax"
Write-Log "DB: $($last.DB_KB) KB, WAL: $($last.WAL_KB) KB"