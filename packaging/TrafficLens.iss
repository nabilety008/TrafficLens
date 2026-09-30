; TrafficLens Inno Setup installer script (TL-015)
;
; Compiled via:
;   scripts\build-release.ps1   (preferred — passes /D defines)
;
; Or manually (using defines with defaults):
;   ISCC.exe /DAppVersion=0.1.5 /DSourceDir=... /DOutputDir=... /DOutputFile=TrafficLens-Setup-0.1.5-win-x64.exe TrafficLens.iss
;
; Per-user install into %LOCALAPPDATA%\Programs\TrafficLens — NO elevation.
; Program files only; user data (%LOCALAPPDATA%\TrafficLens) is never touched.

#ifndef AppVersion
  #define AppVersion "0.1.5"
#endif
#ifndef AppVersionShort
  #define AppVersionShort "0.1.5"
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
; Release entry point. v0.1.4 ships the WinUI 3 application; the WPF
; TrafficLens.exe is retained in the repository as rollback/reference only and
; is never the release entry point. The publish script passes the actual
; executable name discovered from the published output.
#ifndef AppExeName
  #define AppExeName "TrafficLens.WinUI.exe"
#endif

#define AppName "TrafficLens"
#define AppId "{{8F0E8A8F-7B1D-4A5E-9C2D-3E5F6A7B8C9D}}"
#define AppPublisher "TrafficLens Contributors"
; Note: use {#AppExeName} directly at each use site. ISPP does not expand
; constants nested inside another #define, so an alias defined as
; #define AppExe <AppExeName> would resolve to literal text and fail to compile.
; Shared brand icon produced by scripts\generate-icons.ps1 (replaceable asset).
#define BrandIcon SourcePath + "\..\assets\branding\TrafficLens.ico"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
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
UninstallDisplayIcon={app}\{#AppExeName}
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
Name: "desktopicon"; Description: "{cm:TaskDesktopShortcut}"; GroupDescription: "{cm:TaskGroupDescription}"; Flags: unchecked

[Files]
; Program files only. PDBs (debug symbols) are not shipped.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[InstallDelete]
; Upgrade continuity only: v0.1.3 and earlier shipped the WPF executable. The
; v0.1.4 release entry point is the WinUI executable, so the superseded WPF
; launcher is removed on upgrade to avoid leaving (or accidentally launching) a
; second, older application in the install directory. User data under
; %LOCALAPPDATA%\TrafficLens is never touched.
Type: files; Name: "{app}\TrafficLens.exe"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Only remove the empty install-dir shell (files themselves are removed by
; uninstaller). Nothing under %LOCALAPPDATA%\TrafficLens is ever deleted.
Type: dirifempty; Name: "{app}"

[CustomMessages]
english.AppRunningWarning=TrafficLens is currently running. Please close it before continuing so the new files can be installed safely.%n%n(Your settings and history are never affected by an upgrade.)
persian.AppRunningWarning=TrafficLens در حال حاضر در حال اجراست. لطفاً قبل از ادامه آن را ببندید تا پرونده‌های جدید به صورت ایمن نصب شوند.%n%n(تنظیمات و تاریخچه شما تحت تأثیر ارتقا قرار نمی‌گیرند.)
english.TaskDesktopShortcut=Create a &desktop shortcut
english.TaskGroupDescription=Additional shortcuts:
persian.TaskDesktopShortcut=ایجاد میانبر روی دسکتاپ
persian.TaskGroupDescription=میانبرهای اضافی:

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
      'SELECT Name FROM Win32_Process WHERE Name = ''{#AppExeName}''');
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
//     (an entry written by an older release points at
//     %LOCALAPPDATA%\Programs\TrafficLens\TrafficLens.exe, and the app
//     re-writes it from Environment.ProcessPath if the user changes the
//     setting, so a stale path self-corrects on next save).