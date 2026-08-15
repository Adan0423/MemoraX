; =====================================================================
; MemoraX (Standby Memory Manager) - Inno Setup Script
; =====================================================================

#define MyAppName "MemoraX"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Adan0423"
#define MyAppURL "https://github.com/Adan0423/MemoraX"
#define MyAppExeName "StandbyMemoryManager.exe"

[Setup]
; Identificador único de la aplicación
AppId={{D37E88A1-8E24-4C21-99B7-1A86105D12F1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=
OutputBaseFilename=MemoraX_Setup_v1.0.0
OutputDir=dist
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=Assets\app_icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
WizardStyle=modern

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Ejecutar MemoraX al iniciar Windows"; GroupDescription: "Opciones adicionales:"; Flags: unchecked

[Files]
Source: "bin\x64\Release\net9.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
