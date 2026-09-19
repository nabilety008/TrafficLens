param(
    [string]$Exe = "$PSScriptRoot\..\artifacts\publish\win-x64\TrafficLens.exe",
    [string]$Settings = "$env:LOCALAPPDATA\TrafficLens\settings.json",
    [string]$Database = "$env:LOCALAPPDATA\TrafficLens\data\trafficlens.db"
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
public static class WinApiTl18 {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);
    public static IntPtr[] WindowsOf(uint pid) {
        var all = new System.Collections.Generic.List<IntPtr>();
        EnumWindows((h, l) => { uint p; GetWindowThreadProcessId(h, out p); if (p == pid) all.Add(h); return true; }, IntPtr.Zero);
        return all.ToArray();
    }
    public static string Text(IntPtr h) { var sb = new StringBuilder(512); GetWindowText(h, sb, 512); return sb.ToString(); }
}
"@

$script:anyFail = $false
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
    foreach ($h in [WinApiTl18]::WindowsOf([uint32]$proc.Id)) {
        if ([WinApiTl18]::IsWindowVisible($h) -and [WinApiTl18]::Text($h)) { return $h }
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
        foreach ($h in [WinApiTl18]::WindowsOf([uint32]$proc.Id)) {
            if ([WinApiTl18]::IsWindowVisible($h)) {
                $text = [WinApiTl18]::Text($h)
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
function Kill-All { Get-Process -Name "TrafficLens" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue }
function Write-SettingsFile($data) { $data | ConvertTo-Json -Depth 6 | Set-Content $Settings -Encoding UTF8 }
function Has-Persian([string]$s) { return $s -match "[\u0600-\u06FF]" }

if (-not (Test-Path $Exe)) { Write-Host "FATAL: published exe not found: $Exe" -ForegroundColor Red; exit 1 }
if (-not (Test-Path $Database)) { Write-Host "FATAL: history database not found: $Database" -ForegroundColor Red; exit 1 }
Info "Published exe : $Exe"
Info "Settings file : $Settings"
Info "Database      : $Database"

$en = Get-EnResx
$fa = Get-FaResx
$enHistoryNav = $en.HistoryLabel
$faHistoryNav = $fa.HistoryLabel

$settingsBackup = $null
if (Test-Path $Settings) { $settingsBackup = Get-Content $Settings -Raw -Encoding UTF8 }

Kill-All
Start-Sleep -Milliseconds 800

try {
    # ---- A) en-US History page: hourly title (Today default) + range buttons ----
    Write-Host ""
    Write-Host "A) en-US History page: hourly/daily chart titles + range buttons" -ForegroundColor Yellow
    Write-SettingsFile @{ Language = 'en-US'; MinimizeToTray = 'True'; CloseToTray = 'False'; FloatingWidgetEnabled = 'False' }
    $pA = Start-Process -FilePath $Exe -PassThru
    $titleA = Wait-MainWindowTitle $pA
    if (-not $titleA) { Fail "A1: window render" "timeout" }
    $script:tlWin = Get-AppWindowElement $pA
    if ($null -eq $script:tlWin) { Fail "A2: UIA window" "not found" }

    Start-Sleep -Milliseconds 800
    if (-not (Invoke-ButtonByName $enHistoryNav)) { Fail "A2: History nav invoke" "not found" }
    Start-Sleep -Milliseconds 800
    $script:tlWin = Get-AppWindowElement $pA
    if ($null -ne $script:tlWin) {
        $set = Get-DescendantNames $script:tlWin
        # Today is the default range -> chart title must be the hourly label.
        if ($set.Contains($en.HistoryHourlyTrafficLabel)) { Pass "A3: default Today shows 'Hourly Traffic' title" } else { Fail "A3: hourly title on Today" "missing '$($en.HistoryHourlyTrafficLabel)'" }
        $buttons = @($en.TodayLabel, $en.YesterdayLabel, $en.Last7DaysLabel, $en.Last30DaysLabel, $en.LifetimeLabel)
        $missingB = @($buttons | Where-Object { -not $set.Contains($_) })
        if ($missingB.Count -eq 0) { Pass "A4: all 5 range buttons present" } else { Fail "A4: range buttons" "missing: $($missingB -join ' | ')" }
        foreach ($lbl in @($en.DownloadLabel, $en.UploadLabel, $en.TotalRateLabel)) {
            if (-not $set.Contains($lbl)) { Fail "A4b: summary legend labels" "missing '$lbl'"; break }
        }
        # Click Yesterday -> title switches to Daily Traffic, hourly gone.
        if (Invoke-ButtonByName $en.YesterdayLabel) {
            Start-Sleep -Milliseconds 600
            $script:tlWin = Get-AppWindowElement $pA
            $setY = Get-DescendantNames $script:tlWin
            if ($setY.Contains($en.HistoryDailyTrafficLabel)) {
                Pass "A5: Yesterday shows 'Daily Traffic' title"
                if (-not $setY.Contains($en.HistoryHourlyTrafficLabel)) { Pass "A5b: 'Hourly Traffic' absent on Yesterday" } else { Fail "A5b: hourly title leaked on Yesterday" "present" }
            } else { Fail "A5: daily title on Yesterday" "missing '$($en.HistoryDailyTrafficLabel)'" }
        } else { Fail "A5: Yesterday button invoke" "not found" }
        # Back to Today -> hourly title returns.
        if (Invoke-ButtonByName $en.TodayLabel) {
            Start-Sleep -Milliseconds 600
            $script:tlWin = Get-AppWindowElement $pA
            $setT = Get-DescendantNames $script:tlWin
            if ($setT.Contains($en.HistoryHourlyTrafficLabel)) { Pass "A6: back to Today restores hourly title" } else { Fail "A6: hourly title after back to Today" "missing" }
        } else { Fail "A6: Today button invoke" "not found" }
    } else { Fail "A2: UIA window" "not found after nav" }

    # ---- C) Window resize: no crash, still renders ----
    Write-Host ""
    Write-Host "C) Window resize stays healthy" -ForegroundColor Yellow
    $hwndC = Get-MainHwnd $pA
    if ($hwndC -ne [IntPtr]::Zero) {
        [WinApiTl18]::MoveWindow($hwndC, 60, 40, 1280, 780, $true) | Out-Null
        Start-Sleep -Milliseconds 1000
        [WinApiTl18]::MoveWindow($hwndC, 60, 40, 900, 560, $true) | Out-Null
        Start-Sleep -Milliseconds 1000
        if ($null -ne (Get-Process -Id $pA.Id -ErrorAction SilentlyContinue)) {
            $script:tlWin = Get-AppWindowElement $pA
            if ($null -ne $script:tlWin) {
                $setR = Get-DescendantNames $script:tlWin
                if ($setR.Contains($en.HistoryHourlyTrafficLabel)) { Pass "C1: app healthy + Today hourly title survives resize" } else { Fail "C1: hourly title after resize" "missing" }
            } else { Fail "C1: UIA window after resize" "not re-acquired" }
        } else { Fail "C1: process died on resize" "unexpected exit" }
    } else { Fail "C1: window handle" "not found" }

    # ---- D) Data chain: real traffic -> flushed history DB -> reconciliation ----
    Write-Host ""
    Write-Host "D) Data chain: real traffic, flushed DB rows, hourly-capable storage" -ForegroundColor Yellow
    $tmpFile = Join-Path $env:TEMP "tl018-download.bin"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $urls = @('https://proof.ovh.net/files/1Mb.dat', 'https://proof.ovh.net/files/10Mb.dat', 'https://speed.hetzner.de/10MB.bin')
    $deadline = (Get-Date).AddSeconds(40)
    $dlCount = 0
    while ((Get-Date) -lt $deadline) {
        foreach ($u in $urls) {
            try {
                Invoke-WebRequest -UseBasicParsing -Uri $u -OutFile $tmpFile -TimeoutSec 20 -ErrorAction Stop | Out-Null
                $dlCount++
                break
            } catch { }
        }
        Start-Sleep -Milliseconds 300
    }
    Info "download iterations: $dlCount"
    if ($dlCount -gt 0) { Pass "D1: real traffic generated" } else { Fail "D1: traffic generation" "no download succeeded" }
    # Let the minute containing the last bytes finish and a 30s flush pass.
    Info "waiting 65s for a completed minute + one 30s flush..."
    Start-Sleep -Seconds 65

    # Graceful close this instance before reading the DB.
    $hwndD = Get-MainHwnd $pA
    if ($hwndD -ne [IntPtr]::Zero) {
        [WinApiTl18]::SendMessage($hwndD, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    }
    if (Wait-ProcessExit $pA.Id 25) { Pass "D2: graceful exit" } else { Fail "D2: graceful exit" "still alive"; Kill-All; Start-Sleep -Seconds 2 }

    # Read the history DB through a read-only helper (on a copy, never the live file).
    $copyDir = Join-Path $env:TEMP "tl018-dbcheck-$(Get-Date -Format 'HHmmss')"
    New-Item -ItemType Directory -Force -Path $copyDir | Out-Null
    Copy-Item -LiteralPath $Database -Destination (Join-Path $copyDir "trafficlens.db")
    foreach ($s in @('-wal', '-shm')) {
        if (Test-Path "$Database$s") { Copy-Item -LiteralPath "$Database$s" -Destination (Join-Path $copyDir "trafficlens.db$s") }
    }
    $checkDir = Join-Path $PSScriptRoot "..\artifacts\tl018-dbcheck"
    New-Item -ItemType Directory -Force -Path $checkDir | Out-Null
    $proj = Join-Path $checkDir "DbCheck.csproj"
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$(Join-Path $PSScriptRoot '..\src\TrafficLens.Infrastructure\TrafficLens.Infrastructure.csproj')" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $proj -Encoding UTF8
    @"
using System;
using Microsoft.Data.Sqlite;

class DbCheck
{
    static int Main(string[] args)
    {
        var dbPath = args[0];
        var cs = new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly }.ToString();
        using var conn = new SqliteConnection(cs);
        conn.Open();

        var localNow = DateTime.Now;
        var todayLocal = new DateTime(localNow.Year, localNow.Month, localNow.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var midnightUtc = TimeZoneInfo.ConvertTimeToUtc(todayLocal, TimeZoneInfo.Local);
        var midnightUnix = new DateTimeOffset(DateTime.SpecifyKind(midnightUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        var nowUnix = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        var localDate = todayLocal.ToString("yyyy-MM-dd");

        long dailyDownload = 0, dailyUpload = 0;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COALESCE(MAX(download_bytes),0), COALESCE(MAX(upload_bytes),0) FROM daily_usage WHERE local_date = `$d;";
            cmd.Parameters.AddWithValue("`$d", localDate);
            using var r = cmd.ExecuteReader();
            if (r.Read()) { dailyDownload = r.GetInt64(0); dailyUpload = r.GetInt64(1); }
        }

        long count = 0, sDownload = 0, sUpload = 0;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*), COALESCE(SUM(download_bytes),0), COALESCE(SUM(upload_bytes),0) FROM traffic_samples WHERE bucket_start_utc >= `$s AND bucket_start_utc < `$e;";
            cmd.Parameters.AddWithValue("`$s", midnightUnix);
            cmd.Parameters.AddWithValue("`$e", nowUnix);
            using var r = cmd.ExecuteReader();
            if (r.Read()) { count = r.GetInt64(0); sDownload = r.GetInt64(1); sUpload = r.GetInt64(2); }
        }

        Console.WriteLine("LOCALDATE=" + localDate);
        Console.WriteLine("DAILY=" + dailyDownload + "," + dailyUpload);
        Console.WriteLine("SAMPLES=" + count + "," + sDownload + "," + sUpload);
        return 0;
    }
}
"@ | Set-Content -LiteralPath (Join-Path $checkDir "Program.cs") -Encoding UTF8
    $prevEap = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    $runOut = & "C:\dotnet\dotnet.exe" run --project "$proj" -c Release -- "$(Join-Path $copyDir 'trafficlens.db')" 2>&1
    $ErrorActionPreference = $prevEap
    $runOut = $runOut | ForEach-Object { "$_" }
    $runOut | ForEach-Object { Write-Host $_ }
    $out = $runOut | Where-Object { $_ -match '^(LOCALDATE|DAILY|SAMPLES)=' }
    $map = @{}
    foreach ($line in $out) { $kv = $line.ToString().Split('='); $map[$kv[0]] = $kv[1] }
    if ($map['SAMPLES']) {
        $parts = $map['SAMPLES'].Split(',')
        $cnt = [long]$parts[0]; $sD = [long]$parts[1]; $sU = [long]$parts[2]
        if ($cnt -gt 0) { Pass "D3: local-day raw samples flushed ($cnt traffic_samples row(s))" } else { Fail "D3: traffic_samples local-day rows" "none (traffic likely too few or no eligible adapter)" }
        $dParts = $map['DAILY'].Split(',')
        $dD = [long]$dParts[0]; $dU = [long]$dParts[1]
        if ($dD -gt 0 -or $dU -gt 0) { Pass "D4: daily_usage Today row > 0 ($dD down / $dU up)" } else { Fail "D4: daily_usage Today" "zero" }
        # Exactly-reconcile: today's daily totals == sum of today's raw samples.
        if ($dD -eq $sD -and $dU -eq $sU) { Pass "D5: daily_usage Today == sum of today's traffic_samples (exact reconciliation)" } else { Fail "D5: daily/sample reconciliation" "daily=($dD,$dU) samples=($sD,$sU)" }
    } else { Fail "D3: DB read-back helper" "no output" }
    Remove-Item -Recurse -Force $copyDir -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $checkDir -ErrorAction SilentlyContinue

    # ---- B) fa-IR History page: localized hourly/daily titles, no English leak ----
    Write-Host ""
    Write-Host "B) fa-IR History page localized (hourly/daily titles)" -ForegroundColor Yellow
    Kill-All; Start-Sleep -Milliseconds 800
    Write-SettingsFile @{ Language = 'fa-IR'; MinimizeToTray = 'True'; CloseToTray = 'False'; FloatingWidgetEnabled = 'False' }
    $pB = Start-Process -FilePath $Exe -PassThru
    $titleB = Wait-MainWindowTitle $pB
    if (Has-Persian $titleB) { Pass "B1: fa-IR window title Persian" } else { Fail "B1: fa-IR title" "'$titleB'" }
    $script:tlWin = Get-AppWindowElement $pB
    if ($null -ne $script:tlWin) {
        Start-Sleep -Milliseconds 800
        if (-not (Invoke-ButtonByName $faHistoryNav)) { Fail "B2: fa History nav invoke" "not found" }
        Start-Sleep -Milliseconds 800
        $script:tlWin = Get-AppWindowElement $pB
        $faSet = Get-DescendantNames $script:tlWin
        if ($faSet.Contains($fa.HistoryHourlyTrafficLabel)) { Pass "B2: default Today shows Persian hourly title" } else { Fail "B2: fa hourly title on Today" "missing '$($fa.HistoryHourlyTrafficLabel)'" }
        $faButtons = @($fa.TodayLabel, $fa.YesterdayLabel, $fa.Last7DaysLabel, $fa.Last30DaysLabel, $fa.LifetimeLabel)
        $missingFa = @($faButtons | Where-Object { -not $faSet.Contains($_) })
        if ($missingFa.Count -eq 0) { Pass "B3: all 5 fa range buttons present" } else { Fail "B3: fa range buttons" "missing: $($missingFa -join ' | ')" }
        if (Invoke-ButtonByName $fa.YesterdayLabel) {
            Start-Sleep -Milliseconds 600
            $script:tlWin = Get-AppWindowElement $pB
            $faSetY = Get-DescendantNames $script:tlWin
            if ($faSetY.Contains($fa.HistoryDailyTrafficLabel)) { Pass "B4: Yesterday shows Persian daily title" } else { Fail "B4: fa daily title on Yesterday" "missing '$($fa.HistoryDailyTrafficLabel)'" }
            $leak = @($en.HistoryDailyTrafficLabel, $en.HistoryHourlyTrafficLabel | Where-Object { $faSetY.Contains($_) })
            if ($leak.Count -eq 0) { Pass "B5: no English chart titles leaked in fa-IR" } else { Fail "B5: English leak on fa History" ($leak -join ' | ') }
        } else { Fail "B4: fa Yesterday invoke" "not found" }
    } else { Fail "B1: UIA window" "not found" }

    # ---- E) Graceful exit + orphan checks ----
    Write-Host ""
    Write-Host "E) Graceful exit + cleanup" -ForegroundColor Yellow
    $hwndE = Get-MainHwnd $pB
    if ($hwndE -ne [IntPtr]::Zero) {
        [WinApiTl18]::SendMessage($hwndE, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
        if (Wait-ProcessExit $pB.Id 25) { Pass "E1: graceful exit (fa-IR instance)" } else { Fail "E1: graceful exit" "still alive"; Kill-All }
    } else { Fail "E1: window handle" "not found" }
    Start-Sleep -Seconds 2
    if ($null -eq (Get-Process -Name "TrafficLens" -ErrorAction SilentlyContinue)) { Pass "E2: no orphan process" } else { Fail "E2: orphan process" "present" }
}
finally {
    Kill-All
    if ($settingsBackup) { Set-Content $Settings -Value $settingsBackup -Encoding UTF8; Info "Restored original settings.json" }
}

Write-Host ""
if ($script:anyFail) { Write-Host "TL-018 VERIFICATION FAILED" -ForegroundColor Red; exit 1 }
Write-Host "TL-018 VERIFICATION PASSED" -ForegroundColor Green
exit 0