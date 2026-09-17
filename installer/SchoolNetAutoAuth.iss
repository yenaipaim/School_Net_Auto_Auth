#define MyAppName "校园网自动认证"
#define MyAppVersion "0.1.0"
#define MyAppExeName "SchoolNetAutoAuth.App.exe"

[Setup]
AppId={{8D736588-45D8-498B-83BB-E5D331282BDF}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={localappdata}\Programs\SchoolNetAutoAuth
DefaultGroupName={#MyAppName}
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer
OutputBaseFilename=校园网自动认证-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
SetupIconFile=..\img\图标.ico

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "CampusNetworkAutoAuth"; ValueData: "&quot;{app}\{#MyAppExeName}&quot; --background"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent
