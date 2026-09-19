param(
    [string]$Exe = "C:\Users\ali\Documents\New folder\TrafficLens\artifacts\publish\win-x64\TrafficLens.exe",
    [int]$Minutes = 5,
    [string]$OutDir = "C:\Users\ali\Documents\New folder\TrafficLens\artifacts\tl017-baseline"
)
$ErrorActionPreference = 'Stop'
if ([System.Threading.Thread]::CurrentThread.ApartmentState -ne [System.Threading.ApartmentState]::STA) {
    powershell.exe -NoProfile -Sta -ExecutionPolicy Bypass -File $PSCommandPath @args
    exit $LASTEXITCODE
}

Get-Process -Name TrafficLens -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

$env:TRAFFICLENS_STARTUP_TRACE = '1'
$p = Start-Process -FilePath $Exe -ArgumentList '--minimized' -PassThru
Start-Sleep -Seconds 10

$until = (Get-Date).AddMinutes($Minutes)
$i = 0
$rows = @()

while ((Get-Date) -lt $until) {
    Start-Sleep -Seconds 5
    $pr = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
    if (-not $pr) { break }
    $cpu = $pr.TotalProcessorTime.TotalSeconds
    $cpuPct = 0.0
    if ($i -gt 0) {
        $wall = [DateTime]::UtcNow.Subtract($prevT).TotalSeconds
        if ($wall -gt 0) { $cpuPct = [math]::Round([math]::Max(0.0, ($cpu - $prev)) / $wall * 100, 3) }
    }
    $rows += [pscustomobject]@{
        T      = [DateTime]::UtcNow.ToString('HH:mm:ss')
        WS_MB  = [math]::Round($pr.WorkingSet64 / 1MB, 2)
        Priv_MB= [math]::Round($pr.PrivateMemorySize64 / 1MB, 2)
        Thr    = @($pr.Threads).Count
        Hnd    = $pr.HandleCount
        Cpu    = $cpuPct
        Gen0   = $pr.HandleCount # placeholder
    }
    $prev = $cpu; $prevT = [DateTime]::UtcNow
    $i++
}

Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

$out = Join-Path $OutDir ("idle-tray-{0}.csv" -f (Get-Date -Format 'HHmmss'))
$rows | ConvertTo-Csv -NoTypeInformation | Set-Content $out -Encoding ASCII
$first = $rows[0]; $last = $rows[-1]
[pscustomobject]@{
    File     = $out
    WS_first = $first.WS_MB
    WS_last  = $last.WS_MB
    WS_drift = [math]::Round($last.WS_MB - $first.WS_MB, 2)
    Priv_drift = [math]::Round($last.Priv_MB - $first.Priv_MB, 2)
    Cpu_avg  = [math]::Round(($rows | Measure-Object Cpu -Average).Average, 3)
    Cpu_max  = [math]::Round(($rows | Measure-Object Cpu -Maximum).Maximum, 3)
    Thr_last = $last.Thr
    Hnd_last = $last.Hnd
} | Format-List