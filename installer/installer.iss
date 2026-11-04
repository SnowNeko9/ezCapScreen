#define AppVersion "1.0.3"

[Setup]
AppId={{7E2B9C14-5D3A-4F6E-9A1B-3C8D2E7F4A60}
AppName=ezCapScreen
AppVersion={#AppVersion}
AppPublisher=SnowNeko9
AppPublisherURL=https://github.com/SnowNeko9/ezCapScreen
DefaultDirName={autopf}\ezCapScreen
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0.15063
OutputDir=..
OutputBaseFilename=ezCapScreen-Setup
SetupIconFile=..\assets\ezCapScreen.ico
UninstallDisplayIcon={app}\ezCapScreen.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "thai"; MessagesFile: "compiler:Languages\Thai.isl"

[CustomMessages]
english.StartWithWindows=Start ezCapScreen when Windows starts
thai.StartWithWindows=เปิดโปรแกรมอัตโนมัติตอนเข้า Windows
english.DesktopShortcut=Create a desktop shortcut
thai.DesktopShortcut=สร้างทางลัดบนเดสก์ท็อป

[Tasks]
Name: "startup"; Description: "{cm:StartWithWindows}"; Flags: unchecked
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; Flags: unchecked

[Files]
Source: "..\ezCapScreen.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\NOTICE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\ezCapScreen"; Filename: "{app}\ezCapScreen.exe"
Name: "{autodesktop}\ezCapScreen"; Filename: "{app}\ezCapScreen.exe"; Tasks: desktopicon

[Registry]
; Both entries can be created later from the app's Settings, so uninstall removes them either way.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "ezCapScreen"; ValueType: none; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\ezCapScreen"; ValueType: none; Flags: dontcreatekey uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "ezCapScreen"; ValueType: string; ValueData: """{app}\ezCapScreen.exe"" --autostart"; Tasks: startup

[Run]
Filename: "{app}\ezCapScreen.exe"; Description: "{cm:LaunchProgram,ezCapScreen}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\ezCapScreen.exe"; Parameters: "--exit"; RunOnceId: "ExitRunningCopy"; Flags: runhidden waituntilterminated

[Code]
// An upgrade replaces the exe, so any running copy must exit first. Using the new exe also covers a copy run from elsewhere.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  ExtractTemporaryFile('ezCapScreen.exe');
  Exec(ExpandConstant('{tmp}\ezCapScreen.exe'), '--exit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;
