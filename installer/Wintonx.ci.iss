; ---------- CI overrides (safe defaults) ----------
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
; --------------------------------------------------

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppId={#MyAppName}                     ; use a stable GUID later if you want
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir={#MyOutputDir}
OutputBaseFilename={#MyAppName}-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
DisableDirPage=no
WizardStyle=modern



[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Check: not IsAdminInstallMode

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
