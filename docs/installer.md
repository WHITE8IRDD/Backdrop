# INSTALLER.md

## 1. Decision
**Primary: MSIX (via Windows App SDK)** - Best for Win11, clean uninstall, auto-update via AppInstaller, no admin if sideload allowed, Defender friendly, supports StartupTask API.

**Fallback: Inno Setup 6** for users without MSIX sideload or needing portable. Produces `BackdropSetup.exe` (per-user, no admin). Includes WebView2 bootstrapper check.

V1 ships both artifacts from same build.

## 2. MSIX Details
* Package Identity: `YourCompany.Backdrop` Publisher `CN=...`
* Capabilities: `runFullTrust` (for WorkerW interop), `internetClient` (optional HTML remote)
* Dependencies: `Microsoft.WindowsAppRuntime.1.5`, `Microsoft.Web.WebView2` (declare, but runtime usually present)
* StartupTask: `Startup` task entry, user can disable in Settings->Startup.
* Update: `.appinstaller` file on GitHub Releases; Windows checks every 24h.

## 3. Inno Setup
* `PrivilegesRequired=lowest`, `InstallDir={localappdata}\Backdrop`
* Tasks: Create Start Menu shortcut, Startup shortcut via `shell:startup`, optional desktop shortcut
* Uninstall: Removes files, optionally asks "Keep library & settings?" -> if no, deletes `%AppData%\Backdrop`
* WebView2 check: `if not IsWebView2Installed() then Download Evergreen installer`

## 4. Runtime Requirements
* Windows 11 22000+ (Win10 supported but not primary)
* .NET 8 Desktop Runtime (included as self-contained single-file publish `self-contained=true` + `ReadyToRun` to avoid dependency) OR framework-dependent if MSIX.

**Publish:** Self-contained, trimmed, single-file for Host; Main app trimmable cautiously (EF Core needs trimming annotations).

## 5. SmartScreen
* Sign MSIX/.exe with EV cert (or at least OV). Unsigned builds will trigger SmartScreen; docs explain "More info -> Run anyway" for beta.