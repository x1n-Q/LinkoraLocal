#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif
#ifndef PublishDir
  #error PublishDir must be provided.
#endif
#ifndef CloudflaredPath
  #error CloudflaredPath must be provided.
#endif
#ifndef AppIconPath
  #error AppIconPath must be provided.
#endif
#ifndef OutputDir
  #error OutputDir must be provided.
#endif

[Setup]
AppId={{1A7C0EA6-B893-4AFB-A0AF-2A8ED2C13B19}
AppName=Linkora Local
AppVersion={#MyAppVersion}
AppVerName=Linkora Local {#MyAppVersion}
AppPublisher=Linkora
AppPublisherURL=https://linkora.top
AppSupportURL=https://github.com/x1n-Q/LinkoraLocal/issues
AppUpdatesURL=https://github.com/x1n-Q/LinkoraLocal/releases
DefaultDirName={autopf}\Linkora Local
DefaultGroupName=Linkora Local
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir={#OutputDir}
OutputBaseFilename=Linkora-Local-Windows-x64
SetupIconFile={#AppIconPath}
UninstallDisplayIcon={app}\Linkora.Local.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
AppMutex=Linkora.Local.SingleInstance
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany=Linkora
VersionInfoDescription=Secure localhost publishing for Linkora
VersionInfoProductName=Linkora Local
VersionInfoProductVersion={#MyAppVersion}
VersionInfoCopyright=Copyright (c) 2026 Daniel Depaor and Linkora contributors
LicenseFile={#SourcePath}\..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PublishDir}\Linkora.Local.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#CloudflaredPath}"; DestDir: "{app}"; DestName: "cloudflared.exe"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Linkora Local"; Filename: "{app}\Linkora.Local.exe"
Name: "{autodesktop}\Linkora Local"; Filename: "{app}\Linkora.Local.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Run]
Filename: "{app}\Linkora.Local.exe"; Description: "Launch Linkora Local"; Flags: nowait postinstall skipifsilent
