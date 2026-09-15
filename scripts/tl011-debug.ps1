$ErrorActionPreference = 'Stop'

$code = @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class TrayAPI {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc f, IntPtr l);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc f, IntPtr l);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern IntPtr FindWindow(string c, string w);
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

function Dump-Children([IntPtr]$parent, [string]$indent, [int]$depth) {
    if ($depth -gt 4) { return }
    $script:_childList = @()
    $cb = [TrayAPI+EnumWindowsProc]{
        param($h, $l)
        $script:_childList += $h
        return $true
    }
    [TrayAPI]::EnumChildWindows($parent, $cb, [IntPtr]::Zero) | Out-Null
    foreach ($h in $script:_childList) {
        $c = Get-Class $h
        $t = Get-Title $h
        $v = [TrayAPI]::IsWindowVisible($h)
        Write-Output ("$indent[$depth] hwnd=$h class='$c' title='$t' vis=$v")
        Dump-Children $h "$indent  " ($depth + 1)
    }
}

$shellTray = [TrayAPI]::FindWindow('Shell_TrayWnd', $null)
Write-Output ("Shell_TrayWnd: " + $shellTray)
Dump-Children $shellTray '  ' 1

# Also dump all top-level window classes to understand environment
Write-Output '--- Top-level window classes ---'
$cb2 = [TrayAPI+EnumWindowsProc]{
    param($h, $l)
    $script:_tlClasses += @(Get-Class $h)
    return $true
}
$script:_tlClasses = @()
[TrayAPI]::EnumWindows($cb2, [IntPtr]::Zero) | Out-Null
$script:_tlClasses | Group-Object | Sort-Object Count -Descending | ForEach-Object { Write-Output ("  " + $_.Name + " x" + $_.Count) }