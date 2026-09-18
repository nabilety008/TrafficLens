param(
    [string]$Exe = "C:\Users\ali\Documents\New folder\TrafficLens\artifacts\publish\win-x64\TrafficLens.exe",
    [int]$Runs = 4,
    [switch]$Minimized = $false,
    [string]$OutDir = "C:\Users\ali\Documents\New folder\TrafficLens\artifacts\tl017-baseline"
)
$ErrorActionPreference = 'Stop'
if ([System.Threading.Thread]::CurrentThread.ApartmentState -ne [System.Threading.ApartmentState]::STA) {
    powershell.exe -NoProfile -Sta -ExecutionPolicy Bypass -File $PSCommandPath @args
    exit $LASTEXITCODE
}

$tracePath = Join-Path ([System.IO.Path]::GetTempPath()) 'trafficlens-startup-trace.csv'
$target = if ($Minimized) { 'tray-show' } else { 'first-render-ready' }
$skipStages = @('app-start')

Get-Process -Name TrafficLens -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

function Get-TraceRows {
    if (-not (Test-Path -LiteralPath $tracePath)) { return @() }
    $rows = @()
    try {
        foreach ($line in (Get-Content -LiteralPath $tracePath)) {
            $parts = $line.Split(',')
            if ($parts.Length -lt 3) { continue }
            $rows += [pscustomobject]@{
                Seq    = [int]$parts[0]
                Stage  = $parts[1]
                Ms     = [double]$parts[2]
            }
        }
    } catch { }
    return $rows | Sort-Object Seq
}

function Invoke-StartRun {
    param([int]$RunNumber)
    Get-Process -Name TrafficLens -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 400
    if (Test-Path -LiteralPath $tracePath) { Remove-Item -LiteralPath $tracePath -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Milliseconds 300

    $env:TRAFFICLENS_STARTUP_TRACE = '1'
    $appArgs = @()
    if ($Minimized) { $appArgs += '--minimized' }
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    if ($appArgs.Count -gt 0) {
        $p = Start-Process -FilePath $Exe -ArgumentList $appArgs -PassThru
    } else {
        $p = Start-Process -FilePath $Exe -PassThru
    }
    $procId = $p.Id

    $rows = @()
    $wall = -1.0
    $deadline = (Get-Date).AddSeconds(120)
    while ((Get-Date) -lt $deadline) {
        $rows = Get-TraceRows
        if ($rows.Count -gt 0 -and @($rows | Where-Object Stage -eq $target).Count -gt 0) {
            $sw.Stop()
            $wall = $sw.Elapsed.TotalMilliseconds
            break
        }
        if (-not (Get-Process -Id $procId -ErrorAction SilentlyContinue)) {
            if ($rows.Count -eq 0) {
                Start-Sleep -Milliseconds 200
                $rows = Get-TraceRows
            }
            break
        }
        Start-Sleep -Milliseconds 100
    }
    Start-Sleep -Milliseconds 300
    $rows = Get-TraceRows

    Remove-Item Env:TRAFFICLENS_STARTUP_TRACE -ErrorAction SilentlyContinue
    Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500

    if ($rows.Count -eq 0) {
        return [pscustomobject]@{
            Run = $RunNumber; Stage = 'FAILED'; Diff = $null; Wall = $wall
        }
    }

    $result = @()
    $prev = $null
    foreach ($r in ($rows | Sort-Object Seq)) {
        if ($skipStages -contains $r.Stage) { continue }
        $diff = if ($null -ne $prev) { [math]::Round($r.Ms - $prev, 1) } else { 0.0 }
        $result += [pscustomobject]@{
            Run   = $RunNumber
            Stage = $r.Stage
            Diff  = $diff
            Ms    = $r.Ms
            Wall  = $wall
        }
        $prev = $r.Ms
    }
    return $result
}

$latest = @()
for ($i = 1; $i -le $Runs; $i++) {
    $latest += Invoke-StartRun $i
    Start-Sleep -Milliseconds 500
}

$latest | Format-Table -AutoSize Stage, Run, Diff, Wall

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$out = Join-Path $OutDir (("startup-{0}-{1}.csv" -f ($(if ($Minimized) { 'tray' } else { 'win' })), (Get-Date -Format 'HHmmss')))
$latest | Export-Csv -Path $out -NoTypeInformation -Encoding ASCII
"Runs    : $Runs"
"Mode    : $(if ($Minimized) { 'tray (--minimized)' } else { 'windowed' })"
"Output  : $out"
$wallRows = @($latest | Where-Object { $null -ne $_.Wall } | Select-Object -ExpandProperty Wall -Unique)
"Wall-ms launch->target by run: $($wallRows -join ', ')"
if (-not $Minimized) {
    function StageMs([string]$name) { @($latest | Where-Object Stage -eq $name | Select-Object -ExpandProperty Ms) }
    $f = StageMs 'first-render-ready';    "first-render-ready ms by run: $($f -join ', ')"
    $mwr = StageMs 'mainwindow-resolved'; "mainwindow-resolved ms by run: $($mwr -join ', ')"
    $mvb = StageMs 'mainviewmodel-ctor-begin'
    $mcb = StageMs 'mainwindow-ctor-begin'
    $n = [math]::Min($mvb.Count, $mcb.Count)
    if ($n -eq $Runs) {
        $block = @(); $views = @()
        for ($i = 0; $i -lt $n; $i++) {
            $block += [math]::Round($mvb[$i] - $mwr[$i], 1)
            $views += [math]::Round($mcb[$i] - $mvb[$i], 1)
        }
        "page-VM construction ms by run: $($block -join ', ')"
        "mainviewmodel+6 view XAML ms by run: $($views -join ', ')"
    }
    $diff = @()
    for ($i = 0; $i -lt $n; $i++) { $diff += [math]::Round($f[$i] - $mwr[$i], 1) }
    "first-render after resolution ms by run: $($diff -join ', ')"
}
