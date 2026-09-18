param(
    [string]$Exe = "C:\Users\ali\Documents\New folder\TrafficLens\artifacts\publish\win-x64\TrafficLens.exe",
    [int]$Minutes = 3,
    [string]$OutDir = "C:\Users\ali\Documents\New folder\TrafficLens\artifacts\tl017-baseline"
)
$ErrorActionPreference = 'Stop'
if ([System.Threading.Thread]::CurrentThread.ApartmentState -ne [System.Threading.ApartmentState]::STA) {
    powershell.exe -NoProfile -Sta -ExecutionPolicy Bypass -File $PSCommandPath @args
    exit $LASTEXITCODE
}
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Net.Sockets;
public static class TableCount2 {
    [DllImport("iphlpapi.dll")] public static extern uint GetExtendedTcpTable(IntPtr t, ref uint s, bool o, uint af, uint cls, uint r);
    [DllImport("iphlpapi.dll")] public static extern uint GetExtendedUdpTable(IntPtr t, ref uint s, bool o, uint af, uint cls, uint r);
    public static long Count(uint af, bool tcp) {
        uint size = 65536; IntPtr h = Marshal.AllocHGlobal(65536);
        try {
            uint rc = tcp ? GetExtendedTcpTable(h, ref size, false, af, 5, 0) : GetExtendedUdpTable(h, ref size, false, af, 1, 0);
            if (rc != 0) return -1;
            return (long)Marshal.ReadInt32(h);
        } finally { Marshal.FreeHGlobal(h); }
    }
}
"@
Get-Process -Name TrafficLens -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
$p = Start-Process -FilePath $Exe -PassThru
Start-Sleep -Seconds 8
$root = [System.Windows.Automation.AutomationElement]::RootElement
$win = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id))
$nav = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, 'Connections'))
if ($nav) { $nav.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
Start-Sleep -Seconds 5

# Verify the Connections page actually loaded real rows: scan descendant names that
# match running process names. If we see several, the page is active and rendering.
$procNames = @(Get-Process | Select-Object -ExpandProperty ProcessName -Unique)
$win2 = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id))
$count = 0
if ($win2) {
    $all = $win2.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($el in $all) {
        $n = $el.Current.Name
        if ($n -and ($procNames -contains $n)) { $count++ }
        if ($count -ge 3) { break }
    }
}
"Connections row-name hits: $count"
if ($count -lt 3) {
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    throw "Connections nav verification failed (row-name hits=$count). Aborting measurement."
}
$rows = [System.Collections.Generic.List[object]]::new()
$prev = $null; $prevT = $null
$until = (Get-Date).AddMinutes($Minutes)
$i = 0
while ((Get-Date) -lt $until) {
    Start-Sleep -Seconds 5
    $pr = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
    if (-not $pr) { break }
    $cpu = $pr.TotalProcessorTime.TotalSeconds
    $cpuPct = 0.0
    if ($null -ne $prev) {
        $wall = [DateTime]::UtcNow.Subtract($prevT).TotalSeconds
        if ($wall -gt 0) { $cpuPct = [math]::Round([math]::Max(0.0, ($cpu - $prev)) / $wall * 100, 3) }
    }
    $rows.Add([pscustomobject]@{
        T      = [DateTime]::UtcNow.ToString('HH:mm:ss')
        WS_MB  = [math]::Round($pr.WorkingSet64 / 1MB, 2)
        Priv_MB= [math]::Round($pr.PrivateMemorySize64 / 1MB, 2)
        Thr    = @($pr.Threads).Count
        Hnd    = $pr.HandleCount
        Cpu    = $cpuPct
        Tcp4   = [TableCount2]::Count(2, $true)
        Tcp6   = [TableCount2]::Count(23, $true)
        Udp4   = [TableCount2]::Count(2, $false)
        Udp6   = [TableCount2]::Count(23, $false)
    })
    $prev = $cpu; $prevT = [DateTime]::UtcNow
    $i++
}
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
$out = Join-Path $OutDir ("conn-before-{0}.csv" -f (Get-Date -Format 'HHmmss'))
$rows | ConvertTo-Csv -NoTypeInformation | Set-Content $out -Encoding ASCII
$first = $rows[0]; $last = $rows[-1]
[pscustomobject]@{
    File     = $out
    WS_first = $first.WS_MB
    WS_last  = $last.WS_MB
    WS_drift = [math]::Round($last.WS_MB - $first.WS_MB, 2)
    Priv_drift = [math]::Round($last.Priv_MB - $first.Priv_MB, 2)
    Cpu_avg  = [math]::Round(($rows | Measure-Object Cpu -Average -ErrorAction SilentlyContinue).Average, 3)
    Cpu_max  = [math]::Round(($rows | Measure-Object Cpu -Maximum).Maximum, 3)
    Thr_last = $last.Thr
    Hnd_last = $last.Hnd
} | Format-List