; Script Inno Setup 6 (optionnel si Inno Setup est installé)
[Setup]
AppName=ChatP2P Desktop
AppVersion=2.0.0
AppPublisher=JoHn28-3
DefaultDirName={localappdata}\Programs\ChatP2P Desktop
DefaultGroupName=ChatP2P Desktop
SetupIconFile=app.ico
UninstallDisplayIcon={app}\Chatp2pWPF.exe
OutputDir=.
OutputBaseFilename=ChatP2P_InnoSetup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest

[Files]
Source: "..\bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "app.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\ChatP2P Desktop"; Filename: "{app}\Chatp2pWPF.exe"; IconFilename: "{app}\app.ico"
Name: "{autodesktop}\ChatP2P Desktop"; Filename: "{app}\Chatp2pWPF.exe"; IconFilename: "{app}\app.ico"

[Run]
Filename: "{app}\Chatp2pWPF.exe"; Description: "Lancer ChatP2P Desktop"; Flags: nowait postinstall skipifsilent
