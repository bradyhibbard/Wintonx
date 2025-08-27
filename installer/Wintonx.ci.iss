; Inno Setup script for CI builds (Wintonx)
; Defines passed by CI:
;   MyAppName, MyAppVersion, MyPublishDir, MyOutputDir

; ---- defaults, overridden by /D... passed from ISCC ----
#ifndef MyAppName
  #define MyAppName "Wintonx"
#endif
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef MyPublishDir
  #define MyPublishDir "publish"
#endif
#ifndef MyOutputDir
  #define MyOutputDir "installer_out"
#endif
#ifndef MyAppExeName
  #define MyAppExeName "Wintonx.exe"
#endif
; --------------------------------------------------------
)

[Setup]
AppId={{1F75C715-9D8E-4B77-9D1E-9C5B8E0A0ABC}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Hibbard Company
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir={#MyOutputDir}
OutputBaseFilename={#MyAppName}-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
