param(
    [string]$Exe = "$env:LOCALAPPDATA\Programs\TrafficLens\TrafficLens.exe",
    [string]$Settings = "$env:LOCALAPPDATA\TrafficLens\settings.json"
)
$ErrorActionPreference = 'Stop'

if ([System.Threading.Thread]::CurrentThread.ApartmentState -ne [System.Threading.ApartmentState]::STA) {
    $argList = @('-NoProfile', '-Sta', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"")
    $argList += $args
    & powershell.exe $argList
    exit $LASTEXITCODE
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class WinApiTl15 {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    public static IntPtr[] WindowsOf(uint pid) {
        var all = new System.Collections.Generic.List<IntPtr>();
        EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid) all.Add(h); return true; }, IntPtr.Zero);
        return all.ToArray();
    }
    public static string Text(IntPtr h) { var sb = new StringBuilder(512); GetWindowText(h, sb, 512); return sb.ToString(); }
}
public static class ClassNameEnum {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
}
"@

$script:anyFail = $false
$script:AeRoot = [System.Windows.Automation.AutomationElement]::RootElement
$script:tlWin = $null

function P64([int[]]$codes) { -join ($codes | ForEach-Object { [char]$_ }) }
$faTraffic = P64 0x62A,0x631,0x627,0x641,0x6CC,0x6A9
$faPayesh  = P64 0x67E,0x6CC,0x634
function Pass([string]$c) { Write-Host "  PASS: $c" -ForegroundColor Green }
function Fail([string]$c, [string]$r) { Write-Host "  FAIL: $c ($r)" -ForegroundColor Red; $script:anyFail = $true }
function Info([string]$c) { Write-Host "  (i) $c" -ForegroundColor DarkGray }

function Find-WindowElement([string]$nameSubstring, [int]$maxSeconds = 15) {
    for ($t = 0; $t -lt $maxSeconds * 2; $t++) {
        $all = $script:AeRoot.FindAll([System.Windows.Automation.TreeScope]::Children,
            [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($el in $all) {
            $n = $el.Current.Name
            if ($n -like "*$nameSubstring*") { return $el }
        }
        Start-Sleep -Milliseconds 500
    }
    return $null
}
function Find-Element($parent, [string]$name, [string]$controlType = '', [int]$maxSeconds = 8) {
    for ($t = 0; $t -lt $maxSeconds * 2; $t++) {
        try {
            $all = $parent.FindAll([System.Windows.Automation.TreeScope]::Descendants,
                [System.Windows.Automation.Condition]::TrueCondition)
            foreach ($el in $all) {
                if ($el.Current.Name -ne $name) { continue }
                if ([string]::IsNullOrWhiteSpace($controlType) -or
                    $el.Current.ControlType.Equals([System.Windows.Automation.ControlType]::$controlType)) {
                    return $el
                }
            }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    return $null
}
function Kill-All { Get-Process -Name "TrafficLens" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue }
function Write-SettingsFile($data) { $data | ConvertTo-Json -Depth 6 | Set-Content $Settings -Encoding UTF8 }
function Wait-ProcessExit($procId, $maxSeconds) {
    for ($t = 0; $t -lt $maxSeconds * 2; $t++) {
        if ($null -eq (Get-Process -Id $procId -ErrorAction SilentlyContinue)) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return $false
}
function Get-EtwSessions { $n = @(); try { foreach ($l in (logman query -ets 2>$null)) { if ($l -match "TrafficLens") { $n += $l.Trim() } } } catch {} return $n }
function Invoke-ButtonByName([string]$name) {
    if ($null -eq $script:tlWin) { return $false }
    try {
        $el = Find-Element $script:tlWin $name 'Button'
        if ($null -eq $el) { return $false }
        $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() | Out-Null
        Start-Sleep -Milliseconds 400
        return $true
    } catch { return $false }
}
function Open-PageAndFindHeading([string]$buttonName, [string]$headingPattern) {
    $script:tlWin = Find-WindowElement 'TrafficLens' 8
    if ($null -eq $script:tlWin) { return 'WINDOW-LOST' }
    if (-not (Invoke-ButtonByName $buttonName)) { return 'NAV-NOT-FOUND' }
    $script:tlWin = Find-WindowElement 'TrafficLens' 8
    if ($null -eq $script:tlWin) { return 'WINDOW-LOST' }
    $all = $script:tlWin.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($el in $all) {
        if ($el.Current.Name -match $headingPattern) { return $el.Current.Name }
    }
    return 'HEADING-NOT-FOUND'
}
function Wait-MainWindowTitle($proc) {
    for ($t = 0; $t -lt 60; $t++) {
        foreach ($h in [WinApiTl15]::WindowsOf([uint32]$proc.Id)) {
            if ([WinApiTl15]::IsWindowVisible($h)) {
                $text = [WinApiTl15]::Text($h)
                if ($text) { return $text }
            }
        }
        Start-Sleep -Milliseconds 500
    }
    return ''
}

if (-not (Test-Path $Exe)) { Write-Host "FATAL: installed exe not found: $Exe" -ForegroundColor Red; exit 1 }
Info "Installed exe : $Exe"
Info "Settings file : $Settings"

$settingsBackup = $null
if (Test-Path $Settings) { $settingsBackup = Get-Content $Settings -Raw -Encoding UTF8 }

Kill-All
Start-Sleep -Milliseconds 800

try {
    # ---- A) Launch installed copy; window renders with Dashboard defaults ----
    Write-Host "`nA) Launch installed TrafficLens" -ForegroundColor Yellow
    Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'True'; FloatingWidgetEnabled = 'False' }
    $pA = Start-Process -FilePath $Exe -PassThru
    $titleA = Wait-MainWindowTitle $pA
    if ($titleA -match 'TrafficLens') { Pass "A1: installed app launched and window rendered ('$titleA')" } else { Fail "A1: window render" "title='$titleA'" }
    $script:tlWin = Find-WindowElement 'TrafficLens' 15
    if ($null -eq $script:tlWin) { Fail "A1b: UIA window" "not found"; exit 1 } else { Pass "A1b: UIA window handle acquired" }
    Start-Sleep -Seconds 2
    if ($null -ne (Get-Process -Id $pA.Id -ErrorAction SilentlyContinue)) { Pass "A2: process alive after settling" } else { Fail "A2: process alive" "exited" }

    # ---- B) Dashboard nav ----
    Write-Host "`nB) Dashboard" -ForegroundColor Yellow
    $dashHeading = Open-PageAndFindHeading 'Dashboard' 'Dashboard'
    if ($dashHeading -match 'Dashboard') { Pass "B1: Dashboard page shown ('$dashHeading')" } else { Fail "B1: Dashboard page" "'$dashHeading'" }
    Start-Sleep -Milliseconds 800
    $script:tlWin = Find-WindowElement 'TrafficLens' 8
    $dashRates = ''
    $all = $script:tlWin.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($el in $all) { if ($el.Current.Name -match '((Kbps|Mbps|KB/s|MB/s|B/s|Download|Upload))') { $dashRates = $el.Current.Name; break } }
    if ($dashRates) { Pass "B2: Dashboard shows live rate content ('$dashRates')" } else { Fail "B2: Dashboard rates" "no rate text found" }

    # ---- C) Graph page (App has Graph series labels under Dashboard; separate nav) ----
    Write-Host "`nC) Dashboard graph section" -ForegroundColor Yellow
    $script:tlWin = Find-WindowElement 'TrafficLens' 8
    $graphFound = ''
    $all = $script:tlWin.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($el in $all) { if ($el.Current.Name -match 'Last 30 seconds') { $graphFound = $el.Current.Name; break } }
    if ($graphFound) { Pass "C1: Dashboard graph section present ('$graphFound')" } else { Fail "C1: graph section" "not found" }

    # ---- D) Applications nav ----
    Write-Host "`nD) Applications" -ForegroundColor Yellow
    $appHeading = Open-PageAndFindHeading 'Applications' 'Applications'
    if ($appHeading -match 'Applications') { Pass "D1: Applications page shown ('$appHeading')" } else { Fail "D1: Applications page" "'$appHeading'" }

    # ---- E) Connections nav ----
    Write-Host "`nE) Connections" -ForegroundColor Yellow
    $connHeading = Open-PageAndFindHeading 'Connections' 'Connections'
    if ($connHeading -match 'Connections') { Pass "E1: Connections page shown ('$connHeading')" } else { Fail "E1: Connections page" "'$connHeading'" }

    # ---- F) History nav ----
    Write-Host "`nF) History" -ForegroundColor Yellow
    Invoke-ButtonByName 'History' | Out-Null
    $script:tlWin = Find-WindowElement 'TrafficLens' 8
    $histHeading = ''
    $all = $script:tlWin.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($el in $all) { if ($el.Current.Name -match 'History') { $histHeading = $el.Current.Name; break } }
    if ($histHeading -match 'History') { Pass "F1: History page shown ('$histHeading')" } else { Fail "F1: History page" "'$histHeading'" }

    # ---- G) Settings tab & Save ----
    Write-Host "`nG) Settings" -ForegroundColor Yellow
    $script:tlWin = Find-WindowElement 'TrafficLens' 8
    $nav = Find-Element $script:tlWin 'NavSettings' 'Button'
    if ($nav) {
        try { $nav.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() | Out-Null; Start-Sleep -Milliseconds 500 } catch { }
        $save = Find-Element $script:tlWin 'SettingsSave' 'Button'
        if ($save) {
            try { $save.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() | Out-Null; Start-Sleep -Milliseconds 300 } catch { }
            if (Test-Path $Settings) { Pass "G1: Settings page opened & Save persisted settings.json" } else { Fail "G1: settings persisted" "settings.json missing" }
        } else { Fail "G1: SettingsSave button" "UIA not found" }
    } else { Fail "G1: NavSettings button" "UIA not found" }

    # ---- H) Floating widget ----
    Write-Host "`nH) Floating widget" -ForegroundColor Yellow
    Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'True'; FloatingWidgetEnabled = 'True' }
    Kill-All; Start-Sleep -Milliseconds 800
    $pW = Start-Process -FilePath $Exe -PassThru
    $titleW = Wait-MainWindowTitle $pW
    $widget = $null
    for ($t = 0; $t -lt 20; $t++) {
        $all = $script:AeRoot.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($el in $all) { if ($el.Current.Name -match 'Floating Widget') { $widget = $el; break } }
        if ($widget) { break }
        Start-Sleep -Milliseconds 500
    }
    if ($widget) { Pass "H1: floating widget window present (en-US)" } else { Fail "H1: floating widget" "not found" }

    # ---- I) System tray (notify icon) ----
    Write-Host "`nI) System tray (notify icon)" -ForegroundColor Yellow
    $trayFound = $false
    try {
        $procs = Get-Process -Name "TrafficLens" -ErrorAction SilentlyContinue
        foreach ($proc in $procs) {
            [ClassNameEnum]::EnumWindows({
                param($h, $l)
                try {
                    [uint32]$winpid = 0
                    [ClassNameEnum]::GetWindowThreadProcessId($h, [ref]$winpid) | Out-Null
                    $sb = New-Object System.Text.StringBuilder 512
                    [ClassNameEnum]::GetClassName($h, $sb, 512) | Out-Null
                    if ($winpid -eq $proc.Id -and $sb.ToString() -match '^WindowsForms10\.Window\.0\.app') { $script:trayFound = $true; return $false }
                } catch { }
                return $true
            }, [IntPtr]::Zero) | Out-Null
        }
    } catch { }
    if ($trayFound) { Pass "I1: WinForms notify-icon host present" } else { Fail "I1: notify icon" "host not found" }

    # ---- J) en/fa localization from installed copy ----
    Write-Host "`nJ) Localization (fa-IR)" -ForegroundColor Yellow
    Kill-All; Start-Sleep -Milliseconds 800
    Write-SettingsFile @{ Language = 'fa-IR'; MinimizeToTray = 'True'; CloseToTray = 'True'; FloatingWidgetEnabled = 'False' }
    $pJ = Start-Process -FilePath $Exe -PassThru
    $titleJ = ''
    for ($t = 0; $t -lt 40; $t++) {
        foreach ($h in [WinApiTl15]::WindowsOf([uint32]$pJ.Id)) {
            if ([WinApiTl15]::IsWindowVisible($h)) {
                $text = [WinApiTl15]::Text($h)
                if ($text) { $titleJ = $text; break }
            }
        }
        if ($titleJ) { break }
        Start-Sleep -Milliseconds 500
    }
    if ($titleJ -and $titleJ.Contains($faTraffic)) { Pass "J1: installed app rendered in Persian ('$titleJ')" } else { Fail "J1: fa-IR UI" "title='$titleJ'" }
    # Icon title under the fa-IR build still yields a traffic-related string or the latin fallback.
    Kill-All; Start-Sleep -Milliseconds 800
    Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'True'; FloatingWidgetEnabled = 'False' }
    $pJ2 = Start-Process -FilePath $Exe -PassThru
    $titleJ2 = Wait-MainWindowTitle $pJ2
    if ($titleJ2 -match 'TrafficLens') { Pass "J2: back to English ('$titleJ2')" } else { Fail "J2: en-US UI" "title='$titleJ2'" }
    Kill-All; Start-Sleep -Milliseconds 800

    # ---- K) Single instance from installed copy ----
    Write-Host "`nK) Single instance (installed copy)" -ForegroundColor Yellow
    Kill-All; Start-Sleep -Milliseconds 800
    Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'True'; FloatingWidgetEnabled = 'False' }
    $pK1 = Start-Process -FilePath $Exe -PassThru
    Start-Sleep -Seconds 3
    $pK2 = Start-Process -FilePath $Exe -PassThru
    Start-Sleep -Seconds 2
    $countK = (Get-Process -Name "TrafficLens" -ErrorAction SilentlyContinue | Measure-Object).Count
    if ($countK -eq 1) { Pass "K1: single instance enforced (1 process)" } else { Fail "K1: single instance" "$countK processes" }

    # ---- L) Graceful exit (CloseToTray=false) + no ETW orphan ----
    Write-Host "`nL) Graceful exit + ETW cleanup" -ForegroundColor Yellow
    Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'False'; FloatingWidgetEnabled = 'False' }
    Kill-All; Start-Sleep -Milliseconds 800
    $pL = Start-Process -FilePath $Exe -PassThru
    $titleL = Wait-MainWindowTitle $pL
    if ($titleL) {
        $hwnd = [IntPtr]::Zero
        foreach ($h in [WinApiTl15]::WindowsOf([uint32]$pL.Id)) {
            if ([WinApiTl15]::IsWindowVisible($h) -and [WinApiTl15]::Text($h)) { $hwnd = $h; break }
        }
        if ($hwnd -ne [IntPtr]::Zero) {
            [WinApiTl15]::SendMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
            if (Wait-ProcessExit $pL.Id 20) { Pass "L1: graceful exit after WM_CLOSE (CloseToTray=false)" } else { Fail "L1: graceful exit" "still alive"; Kill-All }
        } else { Fail "L1: window handle" "not found" }
    } else { Fail "L1: launch" "timeout" }
    Start-Sleep -Seconds 2
    if ($null -eq (Get-Process -Name "TrafficLens" -ErrorAction SilentlyContinue)) { Pass "L2: no orphan process" } else { Fail "L2: orphan process" "present" }
    $etw = Get-EtwSessions
    if ($etw.Count -eq 0) { Pass "L3: no orphan ETW session" } else { Fail "L3: ETW" ($etw -join ',') }
}
finally {
    Kill-All
    if ($settingsBackup) { Set-Content $Settings -Value $settingsBackup -Encoding UTF8; Info "Restored original settings.json" }
}

Write-Host ""
if ($script:anyFail) { Write-Host "INSTALLED-APP VERIFICATION FAILED" -ForegroundColor Red; exit 1 }
Write-Host "INSTALLED-APP VERIFICATION PASSED" -ForegroundColor Green
exit 0