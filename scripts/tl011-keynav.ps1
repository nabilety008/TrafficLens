$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms

$code = @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class P {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc f, IntPtr l);
    [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int p);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
    public static IntPtr[] WindowsOf(int targetPid) {
        var list = new System.Collections.Generic.List<IntPtr>();
        EnumWindows((h, l) => { int p; GetWindowThreadProcessId(h, out p); if (p == targetPid) list.Add(h); return true; }, IntPtr.Zero);
        return list.ToArray();
    }
}
'@
Add-Type -TypeDefinition $code

function TlMain([int]$targetPid) {
    $out = @()
    foreach ($h in [P]::WindowsOf($targetPid)) {
        $sb = New-Object System.Text.StringBuilder 128
        [P]::GetClassName($h, $sb, 128) | Out-Null
        if ($sb.ToString() -notmatch 'HwndWrapper') { continue }
        $v = [P]::IsWindowVisible($h)
        if ($v) {
            $len = [P]::GetWindowTextLength($h)
            if ($len -gt 0) {
                $tsb = New-Object System.Text.StringBuilder ($len + 1)
                [P]::GetWindowText($h, $tsb, $tsb.Capacity) | Out-Null
                if ($tsb.ToString() -match 'TrafficLens') { $out += $h }
            }
        }
    }
    $out
}

$exe = (Resolve-Path "src\TrafficLens.App\bin\Release\net8.0-windows\TrafficLens.App.exe").Path
Get-Process TrafficLens.App -EA SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 6
$mains = TlMain $proc.Id
$mainHwnd = $mains[0]
Write-Output ("Main visible at start: " + ($mains.Count -gt 0) + " hwnd=" + $mainHwnd)

# minimize main
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class M {
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    public const uint WM_SYSCOMMAND = 0x0112;
    public static readonly IntPtr SC_MINIMIZE = new IntPtr(0xF020);
}
"@
[M]::SendMessage($mainHwnd, [M]::WM_SYSCOMMAND, [M]::SC_MINIMIZE, [IntPtr]::Zero) | Out-Null
Start-Sleep -Seconds 2
Write-Output ("Main visible after minimize: " + ((TlMain $proc.Id).Count -gt 0))

# Win+B to focus notification area
[System.Windows.Forms.SendKeys]::SendWait("+{TAB}")
Write-Output "sent Win+B"
Start-Sleep -Milliseconds 800
$fg = [P]::GetForegroundWindow()
$sb = New-Object System.Text.StringBuilder 128
[P]::GetClassName($fg, $sb, 128) | Out-Null
Write-Output ("Foreground after Win+B: " + $fg + " class=" + $sb.ToString())

# Arrow right a few times, then Enter
foreach ($arrow in 1..6) {
    [System.Windows.Forms.SendKeys]::SendWait("{RIGHT}")
    Start-Sleep -Milliseconds 300
}
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Write-Output "sent ENTER"
Start-Sleep -Seconds 2
Write-Output ("Main visible after ENTER: " + ((TlMain $proc.Id).Count -gt 0))

Get-Process TrafficLens.App -EA SilentlyContinue | Stop-Process -Force