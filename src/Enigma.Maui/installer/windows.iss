; Instalador Windows de Enigma (Inno Setup 6).
; El action de CI lo invoca con /D flags:
;   iscc windows.iss /DSourceDir=<abs dist/win> /DAppVersion=1.0
; Sin /D usan los defaults de abajo (publicación local de prueba).

#ifndef SourceDir
#define SourceDir "..\..\..\dist\win"
#endif

#ifndef AppVersion
#define AppVersion "1.0"
#endif

[Setup]
AppId={{aa850cf9-ca0b-4d31-a2d2-ea38a15697f4}}
AppName=Enigma
AppVersion={#AppVersion}
DefaultDirName={autopf}\Enigma
DisableProgramGroupPage=yes
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#SourceDir}\..\installer-out
OutputBaseFilename=Enigma-setup-{#AppVersion}
PrivilegesRequired=admin

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\Enigma"; Filename: "{app}\Enigma.Maui.exe"

[Run]
Filename: "{app}\Enigma.Maui.exe"; Description: "Ejecutar Enigma ahora"; Flags: nowait postinstall skipifsilent
