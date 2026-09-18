param(
    [string]$Exe = "$PSScriptRoot\..\artifacts\publish\win-x64\TrafficLens.exe",
    [int]$Minutes = 1,
    [string]$OutDir = "$PSScriptRoot\..\artifacts\tl017-baseline"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
Get-Process -Name TrafficLens -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

$p = Start-Process -FilePath $Exe -PassThru
$root = [System.Windows.Automation.AutomationElement]::RootElement
$win = $null
for ($t = 0; $t -lt 30 * 20; $t++) {
    $win = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id))
    if ($win) { break }
    Start-Sleep -Milliseconds 50
}
if (-not $win) { throw "window not found" }

$nav = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
    [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::NameProperty, 'Connections'))
if ($nav) { $nav.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
Start-Sleep -Seconds 5

$sw = [System.Diagnostics.Stopwatch]::StartNew()

$outName = "conn-gc-after-$([DateTime]::Now.ToString('HHmmss')).csv"
$args = @('collect','-p',"$($p.Id)",'--format','csv','--output',('"{0}"' -f (Join-Path $OutDir $outName)),'--duration',"$($Minutes*60)")
$counters = Start-Process -FilePath "$env:USERPROFILE\.dotnet\tools\dotnet-counters.exe" `
    -ArgumentList $args `
    -WindowStyle Hidden -PassThru

for ($t = 0; $t -lt 150; $t++) {
    Start-Sleep -Seconds 1
    if (Test-Path "$OutDir\$outName") {
        $lastSize = 0
        for ($flush = 0; $flush -lt 20; $flush++) {
            Start-Sleep -Seconds 1
            $size = (Get-Item "$OutDir\$outName").Length
            if ($size -eq $lastSize -and $size -gt 0) { break }
            $lastSize = $size
        }
        break
    }
}

Stop-Process -Id $counters.Id -Force -ErrorAction SilentlyContinue
$sw.Stop()
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

"capture done ($($sw.Elapsed.TotalSeconds - 3) s elapsed)"
"counters file: $OutDir\$outName"
"counters err: $(Get-Content "$OutDir\counters-err.txt" -ErrorAction SilentlyContinue | Select-Object -First 1)"