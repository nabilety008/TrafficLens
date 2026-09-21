; TrafficLens Inno Setup installer script (TL-015)
;
; Compiled via:
;   scripts\build-release.ps1   (preferred — passes /D defines)
;
; Or manually (using defines with defaults):
;   ISCC.exe /DAppVersion=0.1.2 /DSourceDir=... /DOutputDir=... /DOutputFile=TrafficLens-Setup-0.1.2-win-x64.exe TrafficLens.iss
;
; Per-user install into %LOCALAPPDATA%\Programs\TrafficLens — NO elevation.
; Program files only; user data (%LOCALAPPDATA%\TrafficLens) is never touched.

#ifndef AppVersion
  #define AppVersion "0.1.2"
#endif
#ifndef AppVersionShort
  #define AppVersionShort "0.1.2"
#endif
#ifndef SourceDir
  #error "Define /DSourceDir=<publish directory>"
#endif
#ifndef OutputDir
  #error "Define /DOutputDir=<artifacts\installer>"
#endif
#ifndef OutputFile
  #error "Define /DOutputFile=TrafficLens-Setup-<version>-win-x64.exe"
#endif

#define AppName "TrafficLens"
#define AppId "{{8F0E8A8F-7B1D-4A5E-9C2D-3E5F6A7B8C9D}}"
#define AppPublisher "TrafficLens Contributors"
#define AppExe "TrafficLens.exe"
; Shared brand icon produced by scripts\generate-icons.ps1 (replaceable asset).
#define BrandIcon SourcePath + "\..\assets\branding\TrafficLens.ico"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersionShort}
AppPublisher={#AppPublisher}
AppCopyright=Copyright (C) 2026 TrafficLens Contributors
; Embedded product metadata for the Setup executable.
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Network Monitor Setup
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\TrafficLens
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename={#OutputFile}
SetupIconFile={#BrandIcon}
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Program files only — user data is left entirely alone by design.
UsePreviousAppDir=yes
UsePreviousGroup=yes
; Never auto-close the running app; the user is asked to close it first.
CloseApplications=no
RestartApplications=no
; Stable uninstall key across versions (no duplicate uninstall entries).
UsePreviousPrivileges=yes
Uninstallable=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "persian"; MessagesFile: "Persian.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; Program files only. PDBs (debug symbols) are not shipped.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Only remove the empty install-dir shell (files themselves are removed by
; uninstaller). Nothing under %LOCALAPPDATA%\TrafficLens is ever deleted.
Type: dirifempty; Name: "{app}"

[CustomMessages]
english.AppRunningWarning=TrafficLens is currently running. Please close it before continuing so the new files can be installed safely.%n%n(Your settings and history are never affected by an upgrade.)
persian.AppRunningWarning=TrafficLens در حال حاضر در حال اجراست. لطفاً قبل از ادامه آن را ببندید تا پرونده‌های جدید به صورت ایمن نصب شوند.%n%n(تنظیمات و تاریخچه شما تحت تأثیر ارتقا قرار نمی‌گیرند.)

[Code]
// ---- Run check ------------------------------------------------------------
// If TrafficLens is already running, ask the user to close it before the
// installer replaces binaries. We never force-kill; the user drives it and
// the built-in "file in use" dialog is a second safety net.
function TrafficLensIsRunning(): Boolean;
var
  WbemLocator: Variant;
  WbemService: Variant;
  WbemObjectSet: Variant;
begin
  Result := False;
  try
    WbemLocator := CreateOleObject('WbemScripting.SWbemLocator');
    WbemService := WbemLocator.ConnectServer('localhost', 'root\cimv2');
    WbemObjectSet := WbemService.ExecQuery(
      'SELECT Name FROM Win32_Process WHERE Name = ''TrafficLens.exe''');
    Result := WbemObjectSet.Count > 0;
  except
    // WMI unavailable (rare) — fall through; Inno's file-in-use dialog still
    // protects the live binaries.
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpReady) and TrafficLensIsRunning() then
  begin
    MsgBox(
      CustomMessage('AppRunningWarning'),
      mbInformation, MB_OK);
  end;
end;

// ---- User data policy -----------------------------------------------------
// Normal uninstall MUST preserve user data: settings.json, the SQLite history
// database and logs live under %LOCALAPPDATA%\TrafficLens and are APP-OWNED
// (not installer-owned). No uninstall task removes them, and no
// [UninstallDelete] entry references them. Users who want to remove retained
// data can delete the %LOCALAPPDATA%\TrafficLens folder manually.
//
// Startup integration is APP-CONTROLLED (HKCU\...\Run value "TrafficLens"
// managed by StartupRegistrationService, TL-013). The installer:
//   - never creates a second startup mechanism
//   - never auto-enables start-with-Windows
//   - never deletes the user's existing Run value on upgrade/uninstall
//     (it points at %LOCALAPPDATA%\Programs\TrafficLens\TrafficLens.exe and
//     the app re-writes it from Environment.ProcessPath if the user changes
//     the setting, so a stale path self-corrects on next save).