[Setup]
AppName=Windows Audio Host Service
AppVersion=1.0
DefaultDirName={autopf}\WinAudioHost
DisableProgramGroupPage=yes
OutputBaseFilename=AudioHostSetup_v1
Compression=lzma
SolidCompression=yes
; Prevents multiple instances from running simultaneously
AppMutex=Global\WinAudioHostMutex

[Files]
; Pulls your standalone, Costura-compiled executable
Source: "SecretNotepad\bin\Release\WinAudioHost.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Creates a silent startup shortcut so it runs automatically when you log into Windows
Name: "{autostartup}\Win Audio Host"; Filename: "{app}\WinAudioHost.exe"

[Run]
; Optionally starts the service immediately after installation finishes
Filename: "{app}\WinAudioHost.exe"; Description: "Start Audio Host Service"; Flags: nowait postinstall skipifsilent