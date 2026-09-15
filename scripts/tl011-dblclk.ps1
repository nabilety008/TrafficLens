$ErrorActionPreference = 'Stop'

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
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
    public static IntPtr[] WindowsOf(int pid) {
        var list = new System.Collections.Generic.List<IntPtr>();
        EnumWindows((h, l) => { int p; GetWindowThreadProcessId(h, out p); if (p == pid) list.Add(h); return true; }, IntPtr.Zero);
        return list.ToArray();
    }
}
'@
Add-Type -TypeDefinition $code

$exe = (Resolve-Path "src\TrafficLens.App\bin\Release\net8.0-windows\TrafficLens.App.exe").Path
Get-Process TrafficLens.App -EA SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 6

function TlWindows([int]$targetPid) {
    $out = @()
    foreach ($h in [P]::WindowsOf($targetPid)) {
        $sb = New-Object System.Text.StringBuilder 128
        [P]::GetClassName($h, $sb, 128) | Out-Null
        $cls = $sb.ToString()
        $v = [P]::IsWindowVisible($h)
        $t = ''
        if ($v) {
            $len = [P]::GetWindowTextLength($h)
            if ($len -gt 0) {
                $tsb = New-Object System.Text.StringBuilder ($len + 1)
                [P]::GetWindowText($h, $tsb, $tsb.Capacity) | Out-Null
                $t = $tsb.ToString()
            }
        }
        $out += [pscustomobject]@{ h = $h; cls = $cls; vis = $v; title = $t }
    }
    $out
}

$wins = TlWindows $proc.Id
Write-Output "--- Windows at start ---"
$wins | ForEach-Object { Write-Output ("  h=$($_.h) vis=$($_.vis) cls=$($_.cls) title='$($_.title)'") }

# NotifyIcon message window = WindowsForms10.Window...
$notify = $wins | Where-Object { $_.cls -like 'WindowsForms10.Window*' } | Select-Object -First 1
Write-Output ("NotifyIcon window: " + $notify.h + " cls=" + $notify.cls)

# main window = visible with title TrafficLens
$main = $wins | Where-Object { $_.vis -and $_.title -match 'TrafficLens' } | Select-Object -First 1
Write-Output ("Main window: " + $main.h + " title='" + $main.title + "'")

# Mininimize main
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class M {
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    public const uint WM_SYSCOMMAND = 0x0112;
    public static readonly IntPtr SC_MINIMIZE = new IntPtr(0xF020);
}
"@
[M]::SendMessage($main.h, [M]::WM_SYSCOMMAND, [M]::SC_MINIMIZE, [IntPtr]::Zero) | Out-Null
Start-Sleep -Seconds 2

$wins2 = TlWindows $proc.Id
$main2 = $wins2 | Where-Object { $_.vis -and $_.title -match 'TrafficLens' } | Select-Object -First 1
Write-Output ("After minimize: main visible = " + ($null -ne $main2))

# Now post WM_LBUTTONDBLCLK to the NotifyIcon window
[P]::PostMessage($notify.h, 0x0203, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
Write-Output "Posted WM_LBUTTONDBLCLK to NotifyIcon window"
Start-Sleep -Seconds 2

$wins3 = TlWindows $proc.Id
$main3 = $wins3 | Where-Object { $_.vis -and $_.title -match 'TrafficLens' } | Select-Object -First 1
Write-Output ("After DBLCLK: main visible = " + ($null -ne $main3) + " sameHandle=" + ($main3.h -eq $main.h))

Get-Process TrafficLens.App -EA SilentlyContinue | Stop-Process -Force
