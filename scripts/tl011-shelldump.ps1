$ErrorActionPreference = 'Stop'

$code = @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class TrayAPI {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc f, IntPtr l);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc f, IntPtr l);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int p);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("owner32.dll")] public static extern IntPtr Fake();
    public delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
}
'@
Add-Type -TypeDefinition $code

function Get-Class([IntPtr]$h) {
    $sb = New-Object System.Text.StringBuilder 256
    [TrayAPI]::GetClassName($h, $sb, $sb.Capacity) | Out-Null
    $sb.ToString()
}
function Get-Title([IntPtr]$h) {
    $len = [TrayAPI]::GetWindowTextLength($h)
    if ($len -eq 0) { return '' }
    $sb = New-Object System.Text.StringBuilder ($len + 1)
    [TrayAPI]::GetWindowText($h, $sb, $sb.Capacity) | Out-Null
    $sb.ToString()
}
function Get-Pid([IntPtr]$h) {
    $p = 0; [TrayAPI]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null; $p
}

$script:_all = [System.Collections.Generic.List[object]]::new()
$cb = [TrayAPI+EnumWindowsProc]{
    param($h, $l)
    $script:_all.Add([PSCustomObject]@{ h = $h; cls = Get-Class $h })
    return $true
}
[TrayAPI]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null

Write-Output "--- Top-level windows matching Tray/Notify/Icon/Toolbar ---"
foreach ($w in $script:_all) {
    if ($w.cls -match 'Tray|Notify|Icon|Toolbar|SysPager|ReBar|SystemTray') {
        Write-Output ("  hwnd=$($w.h) class='$($w.cls)'")
    }
}

# Deep dive into each interesting window's descendants
$interesting = $script:_all | Where-Object { $_.cls -match 'Tray|Notify|SysPager|SystemTray' }
foreach ($w in $interesting) {
    Write-Output ("--- Descendants of '$($w.cls)' ($($w.h)) ---")
    $script:_kids = @()
    $kcb = [TrayAPI+EnumWindowsProc]{
        param($h, $l)
        $script:_kids += $h
        return $true
    }
    [TrayAPI]::EnumChildWindows($w.h, $kcb, [IntPtr]::Zero) | Out-Null
    foreach ($k in $script:_kids) {
        $c = Get-Class $k
        $t = Get-Title $k
        $v = [TrayAPI]::IsWindowVisible($k)
        Write-Output ("    hwnd=$k class='$c' title='$t' vis=$v pid=$(Get-Pid $k)")
    }
    if ($script:_kids.Count -eq 0) { Write-Output "    (no children)" }
}