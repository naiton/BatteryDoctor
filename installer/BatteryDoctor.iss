#define MyAppName "Battery Doctor"
#define MyAppVersion "0.9.0"
#define MyAppPublisher "Battery Doctor Community"
#define MyAppExeName "BatteryDoctor.exe"
#define PublishDir "..\artifacts\publish\win-x64"
#define ReleaseDir "..\artifacts\release"

[Setup]
AppId={{A9395E56-6368-493B-9CA6-E0CE9DCE29A4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\Battery Doctor
DefaultGroupName=Battery Doctor
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#ReleaseDir}
OutputBaseFilename=BatteryDoctor-v{#MyAppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#MyAppName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Battery Doctor"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Battery Doctor"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Battery Doctor"; Flags: nowait postinstall skipifsilent
