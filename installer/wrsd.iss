; WRSD installer. Build with scripts\build-installer.ps1 (publishes a self-contained build to dist\stage first).
#define AppName "WRSD"
#define AppFullName "WRSD - Windows Registry Snapshot Diff"
#ifndef AppVersion
  #define AppVersion "2.1.0"
#endif
#define StageDir "..\dist\stage"

[Setup]
AppId={{CF6FA73F-6C16-54AD-9C5F-1D51E2A10167}
AppName={#AppFullName}
AppVersion={#AppVersion}
AppPublisher=Md Anamul Hasan
AppPublisherURL=https://github.com/md-anamul-hasan/wrsd
AppSupportURL=https://github.com/md-anamul-hasan/wrsd/issues
DefaultDirName={autopf}\WRSD
DefaultGroupName=WRSD
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\Wrsd.UI.exe
UninstallDisplayName={#AppFullName}
SetupIconFile=..\src/Wrsd.UI/Assets/app_icon.ico
LicenseFile=..\LICENSE
OutputDir=..\dist\installer
OutputBaseFilename=WRSD-Setup-{#AppVersion}
Compression=lzma2/ultra
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "{#StageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{autoprograms}\WRSD"; Filename: "{app}\Wrsd.UI.exe"
Name: "{autodesktop}\WRSD"; Filename: "{app}\Wrsd.UI.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Wrsd.UI.exe"; Description: "Launch WRSD"; Flags: nowait postinstall skipifsilent
