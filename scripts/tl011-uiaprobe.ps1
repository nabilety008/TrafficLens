$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

# Launch TrafficLens
$exe = (Resolve-Path "src\TrafficLens.App\bin\Release\net8.0-windows\TrafficLens.App.exe").Path
Get-Process TrafficLens.App -EA SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 6

Write-Output ("PID: " + $proc.Id + " alive=" + (-not $proc.HasExited))

# 1) WinForms message-window proxy check
$code = @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class W32 {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc f, IntPtr l);
    [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int p);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
    public static string[] ClassesOf(int pid) {
        var names = new System.Collections.Generic.List<string>();
        EnumWindows((h, l) => {
            int p; GetWindowThreadProcessId(h, out p);
            if (p == pid) {
                var sb = new StringBuilder(128);
                GetClassName(h, sb, sb.Capacity);
                names.Add(sb.ToString() + (IsWindowVisible(h) ? " [vis]" : " [hidden]"));
            }
            return true;
        }, IntPtr.Zero);
        return names.ToArray();
    }
}
'@
Add-Type -TypeDefinition $code

Write-Output "--- Top-level windows of TrafficLens process ---"
foreach ($c in [W32]::ClassesOf($proc.Id)) { Write-Output ("  " + $c) }

# 2) UIA probe for the tray icon button named TrafficLens
Write-Output "--- UIA: searching for tray icon ---"
$root = [System.Windows.Automation.AutomationElement]::RootElement
$condName = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, "TrafficLens")
$condName2 = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::NameProperty, "TrafficLens")

try {
    $el = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condName)
    if ($null -eq $el) {
        Write-Output "UIA: element named 'TrafficLens' NOT found in descendants"
    } else {
        Write-Output ("UIA: found element: " + $el.Current.ControlType.ProgrammaticName + " name='" + $el.Current.Name + "' className='" + $el.Current.ClassName + "'")
        Write-Output ("     AutomationId='" + $el.Current.AutomationId + "' pid=" + $el.Current.ProcessId)
    }
} catch {
    Write-Output ("UIA error: " + $_.Exception.Message)
}

Get-Process TrafficLens.App -EA SilentlyContinue | Stop-Process -Force