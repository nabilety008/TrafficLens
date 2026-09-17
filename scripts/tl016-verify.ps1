param(
    [string]$Exe = "$PSScriptRoot\..\artifacts\publish\win-x64\TrafficLens.exe",
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
public static class WinApiTl16 {
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
"@

$script:anyFail = $false
$script:AeRoot = [System.Windows.Automation.AutomationElement]::RootElement
$script:tlWin = $null

function Pass([string]$c) { Write-Host "  PASS: $c" -ForegroundColor Green }
function Fail([string]$c, [string]$r) { Write-Host "  FAIL: $c ($r)" -ForegroundColor Red; $script:anyFail = $true }
function Info([string]$c) { Write-Host "  (i) $c" -ForegroundColor DarkGray }

function Get-Resx([string]$Path) {
    [xml]$x = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $map = @{}
    foreach ($d in $x.root.data) { $map[$d.name] = $d.value }
    return $map
}
function Get-FaResx { Get-Resx (Join-Path $PSScriptRoot "..\src\TrafficLens.App\Resources\Strings.fa-IR.resx") }
function Get-EnResx { Get-Resx (Join-Path $PSScriptRoot "..\src\TrafficLens.App\Resources\Strings.resx") }

function Get-MainHwnd($proc) {
    foreach ($h in [WinApiTl16]::WindowsOf([uint32]$proc.Id)) {
        if ([WinApiTl16]::IsWindowVisible($h) -and [WinApiTl16]::Text($h)) { return $h }
    }
    return [IntPtr]::Zero
}
function Get-AppWindowElement($proc) {
    for ($t = 0; $t -lt 40; $t++) {
        $hwnd = Get-MainHwnd $proc
        if ($hwnd -ne [IntPtr]::Zero) {
            try { return [System.Windows.Automation.AutomationElement]::FromHandle($hwnd) } catch { }
        }
        Start-Sleep -Milliseconds 500
    }
    return $null
}
function Get-DescendantNames([System.Windows.Automation.AutomationElement]$rootEl) {
    $set = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    try {
        $all = $rootEl.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($el in $all) {
            $n = $el.Current.Name
            if (-not [string]::IsNullOrWhiteSpace($n)) { [void]$set.Add($n.Trim()) }
        }
    } catch { }
    return $set
}
function Invoke-ButtonByName([string]$name) {
    if ($null -eq $script:tlWin) { return $false }
    try {
        $all = $script:tlWin.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($el in $all) {
            if ($el.Current.Name -eq $name -and
                $el.Current.ControlType.Equals([System.Windows.Automation.ControlType]::Button)) {
                $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() | Out-Null
                Start-Sleep -Milliseconds 500
                return $true
            }
        }
    } catch { }
    return $false
}
function Wait-MainWindowTitle($proc) {
    for ($t = 0; $t -lt 80; $t++) {
        foreach ($h in [WinApiTl16]::WindowsOf([uint32]$proc.Id)) {
            if ([WinApiTl16]::IsWindowVisible($h)) {
                $text = [WinApiTl16]::Text($h)
                if ($text) { return $text }
            }
        }
        Start-Sleep -Milliseconds 500
    }
    return ''
}
function Wait-ProcessExit($procId, $maxSeconds) {
    for ($t = 0; $t -lt $maxSeconds * 2; $t++) {
        if ($null -eq (Get-Process -Id $procId -ErrorAction SilentlyContinue)) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return $false
}
function Get-EtwSessions { $n = @(); try { foreach ($l in (logman query -ets 2>$null)) { if ($l -match "TrafficLens") { $n += $l.Trim() } } } catch {} return $n }
function Kill-All { Get-Process -Name "TrafficLens" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue }
function Write-SettingsFile($data) { $data | ConvertTo-Json -Depth 6 | Set-Content $Settings -Encoding UTF8 }
function Has-Persian([string]$s) { return $s -match "[\u0600-\u06FF]" }

if (-not (Test-Path $Exe)) { Write-Host "FATAL: published exe not found: $Exe" -ForegroundColor Red; exit 1 }
Info "Published exe : $Exe"
Info "Settings file : $Settings"

$en = Get-EnResx
$fa = Get-FaResx

$enNav  = @($en.DashboardLabel, $en.ApplicationsLabel, $en.ConnectionsLabel, $en.HistoryLabel, $en.AlertsNavLabel, $en.SettingsNavLabel, $en.AboutNavLabel)
$faNav  = @($fa.DashboardLabel, $fa.ApplicationsLabel, $fa.ConnectionsLabel, $fa.HistoryLabel, $fa.AlertsNavLabel, $fa.SettingsNavLabel, $fa.AboutNavLabel)
$enPage = @($en.GraphLiveTrafficLabel, $en.GraphLast30SecondsLabel, $en.DownloadLabel, $en.UploadLabel)
$faPage = @($fa.GraphLiveTrafficLabel, $fa.GraphLast30SecondsLabel, $fa.DownloadLabel, $fa.UploadLabel)

$settingsBackup = $null
if (Test-Path $Settings) { $settingsBackup = Get-Content $Settings -Raw -Encoding UTF8 }

Kill-All
Start-Sleep -Milliseconds 800

try {
    # ---- A) en-US: nav + dashboard labels render from Strings.resx ----
    Write-Host ""
    Write-Host "A) en-US UI labels render from Strings.resx" -ForegroundColor Yellow
    Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'True'; FloatingWidgetEnabled = 'False' }
    $pA = Start-Process -FilePath $Exe -PassThru
    $titleA = Wait-MainWindowTitle $pA
    if ($titleA) { Pass "A1: window rendered ('$titleA')" } else { Fail "A1: window render" "timeout" }
    $script:tlWin = Get-AppWindowElement $pA
    if ($null -eq $script:tlWin) { Fail "A2: UIA window" "not found"; } else {
        Pass "A2: UIA window acquired"
        Start-Sleep -Milliseconds 800
        $set = Get-DescendantNames $script:tlWin
        $missing = @($enNav + $enPage | Where-Object { -not $set.Contains($_) })
        if ($missing.Count -eq 0) {
            Pass "A3: all nav + dashboard labels present in English"
        } else {
            Fail "A3: en nav/dashboard labels" "missing: $($missing -join ' | ')"
        }
    }

    # ---- B) fa-IR: full Persian UI (content scan, not just title) ----
    Write-Host ""
    Write-Host "B) fa-IR: full Persian UI (content scan)" -ForegroundColor Yellow
    Kill-All; Start-Sleep -Milliseconds 800
    Write-SettingsFile @{ Language = 'fa-IR'; MinimizeToTray = 'True'; CloseToTray = 'True'; FloatingWidgetEnabled = 'False' }
    $pB = Start-Process -FilePath $Exe -PassThru
    $titleB = Wait-MainWindowTitle $pB
    if (Has-Persian $titleB) { Pass "B1: window title is Persian ('$titleB')" } else { Fail "B1: fa-IR title" "'$titleB'" }
    $script:tlWin = Get-AppWindowElement $pB
    if ($null -eq $script:tlWin) { Fail "B2: UIA window" "not found" } else {
        Pass "B2: UIA window acquired"
        Start-Sleep -Milliseconds 800
        $faSet = Get-DescendantNames $script:tlWin
        $faMissing = @($faNav + $faPage | Where-Object { -not $faSet.Contains($_) })
        if ($faMissing.Count -eq 0) {
            Pass "B3: all nav + dashboard labels present in Persian"
        } else {
            Fail "B3: fa nav/dashboard labels" "missing: $($faMissing -join ' | ')"
        }
        $enLeak = @($enNav + $enPage | Where-Object { $faSet.Contains($_) })
        if ($enLeak.Count -eq 0) {
            Pass "B4: no English nav/dashboard labels leaked in fa-IR UI"
        } else {
            Fail "B4: English leaked in fa-IR" ($enLeak -join ' | ')
        }
    }

    # ---- C) fa-IR History page navigation + heading ----
    Write-Host ""
    Write-Host "C) fa-IR History page is reachable and localized" -ForegroundColor Yellow
    if ($null -ne $script:tlWin) {
        if (Invoke-ButtonByName $fa.HistoryLabel) {
            Pass "C1: nav button '$($fa.HistoryLabel)' invoked"
        } else {
            Fail "C1: fa History nav button" "not found/invocable"
        }
        Start-Sleep -Milliseconds 800
        $script:tlWin = Get-AppWindowElement $pB
        $histSet = Get-DescendantNames $script:tlWin
        $histExpected = @($fa.HistoryLabel, $fa.HistoryDailyTrafficLabel)
        $histMissing = @($histExpected | Where-Object { -not $histSet.Contains($_) })
        if ($histMissing.Count -eq 0) {
            Pass "C2: History page content in Persian"
        } else {
            Fail "C2: fa History page labels" "missing: $($histMissing -join ' | ')"
        }
        $histEnLeak = @($en.HistoryLabel | Where-Object { $histSet.Contains($_) })
        if ($histEnLeak.Count -eq 0) { Pass "C3: no English 'History' leak on fa History page" } else { Fail "C3: English leak" "History" }
    } else {
        Fail "C1: UIA window" "not found"
    }

    # ---- E) About page: reachable + localized (fa-IR then en-US) ----
    Write-Host ""
    Write-Host "E) About page reachable and localized" -ForegroundColor Yellow
    if ($null -ne $script:tlWin) {
        if (Invoke-ButtonByName "NavAbout") {
            Pass "E1: 'NavAbout' nav button invoked (fa-IR)"
        } else {
            Fail "E1: NavAbout nav button" "not found/invocable"
        }
        Start-Sleep -Milliseconds 900
        $script:tlWin = Get-AppWindowElement $pB
        $aboutSet = Get-DescendantNames $script:tlWin
        $enAbout  = @($en.AboutTitleLabel, $en.VersionLabel, $en.RuntimeLabel, $en.OsLabel, $en.DiagnosticsHeaderLabel, $en.CopyDiagnosticsLabel, $en.OpenLogFolderLabel)
        $faAbout  = @($fa.AboutTitleLabel, $fa.VersionLabel, $fa.RuntimeLabel, $fa.OsLabel, $fa.DiagnosticsHeaderLabel, $fa.CopyDiagnosticsLabel, $fa.OpenLogFolderLabel)
        $missingFa = @($faAbout | Where-Object { -not $aboutSet.Contains($_) })
        if ($missingFa.Count -eq 0) {
            Pass "E2: About page content localized (fa-IR)"
        } else {
            Fail "E2: fa About page labels" "missing: $($missingFa -join ' | ')"
        }
        $leakFa = @($enAbout | Where-Object { $aboutSet.Contains($_) })
        if ($leakFa.Count -eq 0) {
            Pass "E3: no English About labels leaked in fa-IR"
        } else {
            Fail "E3: English leak on fa About page" ($leakFa -join ' | ')
        }
        if ($aboutSet.Contains("AboutProductName") -or $aboutSet.Contains($fa.ProductNameLabel)) {
            Pass "E4: brand identity present on About page (TrafficLens)"
        } else {
            Fail "E4: product name on fa About page" "not found"
        }
    } else {
        Fail "E1: UIA window" "not found"
    }

    # ---- E') About page in en-US ----
    Kill-All; Start-Sleep -Milliseconds 800
    Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'True'; FloatingWidgetEnabled = 'False' }
    $pE = Start-Process -FilePath $Exe -PassThru
    $titleE = Wait-MainWindowTitle $pE
    $script:tlWin = Get-AppWindowElement $pE
    if ($null -ne $script:tlWin -and $titleE) {
        if (Invoke-ButtonByName "NavAbout") {
            Pass "E5: 'NavAbout' invoked (en-US)"
        } else {
            Fail "E5: NavAbout nav button" "not found/invocable"
        }
        Start-Sleep -Milliseconds 900
        $script:tlWin = Get-AppWindowElement $pE
        $enSet = Get-DescendantNames $script:tlWin
        $missingEn = @($enAbout | Where-Object { -not $enSet.Contains($_) })
        if ($missingEn.Count -eq 0) {
            Pass "E6: About page content in English"
        } else {
            Fail "E6: en About page labels" "missing: $($missingEn -join ' | ')"
        }
        $leakEn = @($faAbout | Where-Object { $enSet.Contains($_) })
        if ($leakEn.Count -eq 0) {
            Pass "E7: no Persian About labels leaked in en-US"
        } else {
            Fail "E7: Persian leak on en About page" ($leakEn -join ' | ')
        }
    } else {
        Fail "E5: en-UIA window" "not found"
    }

    # ---- D) Graceful exit + cleanup ----
    Write-Host ""
    Write-Host "D) Graceful exit + cleanup" -ForegroundColor Yellow
    Kill-All; Start-Sleep -Milliseconds 800
    Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'False'; FloatingWidgetEnabled = 'False' }
    $pD = Start-Process -FilePath $Exe -PassThru
    $titleD = Wait-MainWindowTitle $pD
    if ($titleD) {
        $hwnd = [IntPtr]::Zero
        foreach ($h in [WinApiTl16]::WindowsOf([uint32]$pD.Id)) {
            if ([WinApiTl16]::IsWindowVisible($h) -and [WinApiTl16]::Text($h)) { $hwnd = $h; break }
        }
        if ($hwnd -ne [IntPtr]::Zero) {
            [WinApiTl16]::SendMessage($hwnd, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
            if (Wait-ProcessExit $pD.Id 20) { Pass "D1: graceful exit after WM_CLOSE (CloseToTray=false)" } else { Fail "D1: graceful exit" "still alive"; Kill-All }
        } else { Fail "D1: window handle" "not found" }
    } else { Fail "D1: launch" "timeout" }
    Start-Sleep -Seconds 2
    if ($null -eq (Get-Process -Name "TrafficLens" -ErrorAction SilentlyContinue)) { Pass "D2: no orphan process" } else { Fail "D2: orphan process" "present" }
    $etw = Get-EtwSessions
    if ($etw.Count -eq 0) { Pass "D3: no orphan ETW session" } else { Fail "D3: ETW" ($etw -join ',') }
}
finally {
    Kill-All
    if ($settingsBackup) { Set-Content $Settings -Value $settingsBackup -Encoding UTF8; Info "Restored original settings.json" }
}

Write-Host ""
if ($script:anyFail) { Write-Host "TL-016 VERIFICATION FAILED" -ForegroundColor Red; exit 1 }
Write-Host "TL-016 VERIFICATION PASSED" -ForegroundColor Green
exit 0