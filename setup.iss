; ALttP MSU-1 Music Switcher — Inno Setup Script
; Compile with: "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" setup.iss
; Or just run publish.bat which handles everything automatically.

[Setup]
AppId={{7C4B2A8F-3D1E-4F9C-A6B5-8E2D7F3C5A1B}
AppName=ALttP MSU-1 Music Switcher
AppVersion=1.0.0
AppVerName=ALttP MSU-1 Music Switcher 1.0.0
AppPublisher=LTTPMusicReplacer
AppPublisherURL=
AppSupportURL=
AppUpdatesURL=

; Install to per-user Programs folder — no admin rights required
DefaultDirName={localappdata}\Programs\LTTPMusicReplacer
DefaultGroupName=ALttP MSU-1 Music Switcher
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Output
OutputDir=installer
OutputBaseFilename=LTTPMusicReplacerSetup-1.0.0-win64
SetupIconFile=Resources\icon.ico
UninstallDisplayIcon={app}\LTTPMusicReplacer.exe

; Compression
Compression=lzma2/ultra64
SolidCompression=yes

; UI
WizardStyle=modern
WizardSmallImageFile=

; Windows version requirement
MinVersion=10.0.17763

; Architecture
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

; Misc
DisableProgramGroupPage=yes
DisableWelcomePage=no
UninstallDisplayName=ALttP MSU-1 Music Switcher

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; Main application executable (self-contained, no .NET required on target machine)
Source: "bin\Release\net8.0-windows\win-x64\publish\LTTPMusicReplacer.exe"; \
    DestDir: "{app}"; \
    Flags: ignoreversion

[Icons]
; Start Menu
Name: "{group}\ALttP MSU-1 Music Switcher"; \
    Filename: "{app}\LTTPMusicReplacer.exe"; \
    Comment: "Manage MSU-1 music packs for ALttP Randomizer"

Name: "{group}\Uninstall ALttP MSU-1 Music Switcher"; \
    Filename: "{uninstallexe}"

; Desktop (optional, unchecked by default)
Name: "{autodesktop}\ALttP MSU-1 Music Switcher"; \
    Filename: "{app}\LTTPMusicReplacer.exe"; \
    Tasks: desktopicon

[Run]
; Offer to launch after install
Filename: "{app}\LTTPMusicReplacer.exe"; \
    Description: "Launch ALttP MSU-1 Music Switcher"; \
    Flags: nowait postinstall skipifsilent
