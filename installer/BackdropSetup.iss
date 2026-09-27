; Backdrop Inno Setup 6 fallback installer (INSTALLER.md §3).
; Per-user, no admin. Includes WebView2 bootstrapper check.
#define AppVersion "1.0.0"

[Setup]
AppName=Backdrop
AppVersion={#AppVersion}
AppPublisher=Backdrop
DefaultDirName={localappdata}\Backdrop
PrivilegesRequired=lowest
OutputBaseFilename=BackdropSetup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "..\out\publish-app\*"; DestDir: "{app}"; Flags: recursesubdirs
Source: "..\out\publish-host\*"; DestDir: "{app}\host"; Flags: recursesubdirs

[Icons]
Name: "{userprograms}\Backdrop"; Filename: "{app}\Backdrop.App.exe"
Name: "{userstartup}\Backdrop"; Filename: "{app}\Backdrop.App.exe"; Parameters: "--minimized"; Tasks: startup

[Tasks]
Name: startup; Description: "Run at startup"; Flags: unchecked
Name: desktop; Description: "Desktop shortcut"; Flags: unchecked

[Icons]
Name: "{userdesktop}\Backdrop"; Filename: "{app}\Backdrop.App.exe"; Tasks: desktop

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
function IsWebView2Installed(): Boolean;
var
  Key: string;
begin
  Result := RegKeyExists(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\ClientState\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}')
         or RegKeyExists(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\ClientState\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}');
end;

function InitializeSetup(): Boolean;
var
  Res: Integer;
begin
  Result := True;
  if not IsWebView2Installed() then
  begin
    if MsgBox('WebView2 runtime was not detected. Download it now?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://go.microsoft.com/fwlink/p/?LinkId=2124703', '', '', SW_SHOW, ewNoWait, Res);
    end;
  end;
end;
