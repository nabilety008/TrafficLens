param([string]$Exe, [string]$OutFile, [int]$Seconds = 60)
$ErrorActionPreference = 'Stop'
$log = "$OutFile.log.txt"
"elevated=$(([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))" | Set-Content $log -Encoding UTF8
try {
    $p = Start-Process -FilePath $Exe -PassThru
    Start-Sleep -Seconds 8
    $sessions = (logman query -ets 2>$null | Out-String)
    "sessions`n$sessions" | Add-Content $log -Encoding UTF8
    $rows = [System.Collections.Generic.List[object]]::new()
    $prevCpu = (Get-Process -Id $p.Id).TotalProcessorTime.TotalSeconds
    $prevT = [DateTime]::UtcNow
    $end = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $end) {
        Start-Sleep -Seconds 10
        $now = [DateTime]::UtcNow
        $cpu = (Get-Process -Id $p.Id -ErrorAction SilentlyContinue).TotalProcessorTime.TotalSeconds
        if ($null -eq $cpu) { break }
        $wall = $now.Subtract($prevT).TotalSeconds
        $pct = if ($wall -gt 0) { [math]::Round([math]::Max(0.0, ($cpu - $prevCpu)) / $wall * 100, 3) } else { 0 }
        $pr = Get-Process -Id $p.Id
        $rows.Add([pscustomobject]@{ Ts = $now.ToString('HH:mm:ss'); CpuPct = $pct; WS_MB = [math]::Round($pr.WorkingSet64 / 1MB, 2); Threads = @($pr.Threads).Count; Handles = $pr.HandleCount })
        $prevCpu = $cpu; $prevT = $now
    }
    $tail = @($rows | Select-Object -Last 3)
    [pscustomobject]@{ EtwActive = [bool](($sessions -match 'TrafficLens')); Last3 = $tail; AvgCpu = [math]::Round(($rows | Measure-Object CpuPct -Average).Average, 3); MaxCpu = [math]::Round(($rows | Measure-Object CpuPct -Maximum).Maximum, 3) } | ConvertTo-Json -Depth 5 | Set-Content $OutFile -Encoding UTF8
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
} catch {
    "ERR: $($_.Exception.Message)" | Add-Content $log -Encoding UTF8
    "ERROR" | Set-Content $OutFile -Encoding UTF8
}