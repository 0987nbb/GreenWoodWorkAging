; ============================================================================
;  Green Wood Works — installer definition
;
;  Do not run ISCC by hand. installer\build-installer.ps1 publishes the app
;  first and compiles this script with the version read from the csproj:
;
;      powershell -File installer\build-installer.ps1
;
;  Output: installer\Output\GreenWoodWorks-Setup-<version>.exe — the single file
;  a customer double-clicks. Nothing else has to be installed: the app ships as
;  a self-contained build, so .NET, SQLite, QuestPDF and the OCR engine all come
;  inside it.
; ============================================================================

#define AppName      "Green Wood Works"
#define AppExeName   "GreenWoodWorkAging.exe"

; build-installer.ps1 always passes this; the default only keeps ISCC usable on its own.
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
; Stable identity: an update of the same product replaces this install instead of
; sitting beside it. Never change it once a customer has the first setup.
AppId=GreenWoodWorkAging
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppName}

; A shop counter PC has no admin password. Lowest privileges installs into the
; signed-in user's own folder; the dialog still lets an IT person choose
; "for all users" if they can authenticate.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\GreenWoodWorkAging
DisableProgramGroupPage=yes
DisableWelcomePage=no

; .NET 9 desktop runs on Windows 10 and later, 64-bit only.
MinVersion=10.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; The brand mark on the setup file itself, on the shortcuts and in Add/Remove Programs.
SetupIconFile=..\Resources\app.ico
UninstallDisplayIcon={app}\{#AppExeName}

; The app is 140 MB unpacked; maximum compression keeps the file the customer downloads small.
Compression=lzma2/max
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=GreenWoodWorks-Setup-{#AppVersion}

; The database is a live file while the app is open. Refuse to write over it quietly.
CloseApplications=yes
RestartApplications=no

[Files]
; The whole published folder: exe, .NET runtime, Resources\logo.jpg + app.ico, tessdata\,
; the OCR native DLLs and the C++ runtime they need. ignoreversion means an update always
; lands the new files over the old ones.
Source: "..\publish\GreenWoodWorkAging\*"; DestDir: "{app}"; \
    Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
; The folder the customer database lives in. Creating it here is the whole "database setup"
; the installer needs to do: the application adds greenwoodwork.db and its tables on first
; launch, and repeats that safely if the folder already holds years of bills.
; Deliberately NOT listed under [UninstallDelete] — bills outlive an uninstall.
Name: "{userappdata}\GreenWoodWorkAging"

[Icons]
; IconFilename is left out on purpose: Windows takes the shortcut's picture from the exe,
; and that exe already carries the brand mark.
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; \
    Tasks: desktopicon
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; \
    Flags: nowait postinstall skipifsilent
