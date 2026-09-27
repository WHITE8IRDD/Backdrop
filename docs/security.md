# SECURITY.md

## 1. Threat Model
Wallpapers are untrusted content. HTML wallpapers = arbitrary JS. Video could be malformed exploit.

## 2. Video/GIF
* Only decode via hardened MF pipeline (sandboxed by Windows).
* Validate extension + MIME, reject executables `.exe .scr .bat`.
* File size limit: 500MB default (settings adjustable).
* Path traversal protection: When unzipping, ensure `Path.GetFullPath(entry) .StartsWith(libraryRoot)`; reject absolute paths or `..`.

## 3. HTML Wallpapers (Critical)

* WebView2 runs with:
  * `CoreWebView2.Settings.IsScriptEnabled = true` (needed) but `AreHostObjectsAllowed = false` initially
  * `IsWebMessageEnabled = false` unless manifest explicitly allows.
  * Disabled `AreDefaultContextMenusEnabled = false`, `IsZoomControlEnabled = false`
* **Origin lock:** For HtmlLocal, set `CoreWebView2.SetVirtualHostNameToFolderMapping("appassets", folder, HOST_RESOURCE_ACCESS_KIND.DENY_CORS)` . Load via `https://appassets/index.html`. No `file://` direct.
* **CSP:** Inject `<meta http-equiv="Content-Security-Policy" content="default-src 'self' 'unsafe-inline' data: blob:; script-src 'self' 'unsafe-inline'; connect-src 'none';">` if missing, or warn.
* **External URLs (HtmlRemoteUrl):** Whitelist requires user opt-in. Block `file://`, `http://localhost` by default. Show warning infobar: "This wallpaper loads online content."
* **No Node, no native:** WebView2 has no Node. Not enabling `AdditionalBrowserArguments --allow-file-access`.
* **Sandbox:** Host process runs as `Integrity Level: Medium` (standard user). No elevation ever. Manifest `asInvoker`.
* **Navigation restriction:** `CoreWebView2.NavigationStarting` -> cancel if navigating outside allowed folder/domain.

## 4. System
* Never run as admin.
* IPC named pipe: `DACL` restricted to current user SID, no remote.
* CLI/API token: Simple random token stored in settings, required for pipe commands (prevents other user processes abuse).

## 5. Validation Checklist (ImportService)
* Extension allowlist
* Magic bytes check (MP4 `ftyp`, GIF `GIF89a`, HTML `<html`)
* Antivirus: optional `IAttachmentExecute` scan?
* Remove Zone.Identifier? Preserve for SmartScreen but don't block.