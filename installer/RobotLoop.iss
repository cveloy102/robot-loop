#define AppVersion "1.2.3"

[Setup]
AppId={{2496BC21-4BF2-42C1-8A25-3F99594A2819}
AppName=Robot Loop
AppVersion={#AppVersion}
AppPublisher=Robot Loop
DefaultDirName={localappdata}\Programs\Robot Loop
DefaultGroupName=Robot Loop
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
DisableDirPage=no
OutputDir=..\dist
OutputBaseFilename=RobotLoop-Setup-{#AppVersion}
SetupIconFile=..\assets\icon-a-loop.ico
UninstallDisplayIcon={app}\RobotLoop.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
AppMutex=RobotLoop.SingleInstance
CloseApplications=yes
RestartApplications=no
DisableWelcomePage=no
VersionInfoVersion={#AppVersion}.0
VersionInfoDescription=Robot Loop Setup

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Ярлыки:"; Flags: unchecked

[Files]
Source: "..\dist\RobotLoop.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\fonts\OFL.txt"; DestDir: "{app}\licenses"; DestName: "Fonts-OFL.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Robot Loop"; Filename: "{app}\RobotLoop.exe"
Name: "{autodesktop}\Robot Loop"; Filename: "{app}\RobotLoop.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\RobotLoop.exe"; Description: "Запустить Robot Loop"; Flags: nowait postinstall skipifsilent
