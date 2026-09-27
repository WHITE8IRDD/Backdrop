# TECH_STACK.md

## 1. Decision Matrix

| Option | Pros | Cons | Verdict |
|---|---|---|---|
| **WinUI 3 / Windows App SDK 1.5+** | True Windows 11 native (Mica, rounded corners, dark/light), modern, supported till 2030+, best DPI/multimonitor | App SDK deployment complexity, MSIX advantage | **CHOSEN for Main UI** |
| **WPF** | Mature, easy, stable | Feels dated, no native Mica, poor WebView2 airspace, not future | Rejected |
| **Electron / Tauri** | Fast web dev | Huge RAM, slow startup, non-native feel | Rejected |
| **C# .NET 8 (LTS)** | Excellent perf, AOT-trim ready, vast Win32 interop, maintainable | - | **CHOSEN runtime** |
| **Media Foundation (MF) via `Windows.Media.Playback.MediaPlayer`** | Zero extra dependency, HW accelerated D3D11, low RAM, preinstalled codecs | Limited codec support vs VLC | **CHOSEN primary for V1** |
| **LibVLC / mpv** | Plays everything | +80MB binaries, license complexity, extra process | **Fallback interface reserved, not bundled V1** |
| **WebView2** | Modern Chromium, sandbox, HW accel, supports HTML/WebGL | ~120MB runtime (usually preinstalled on Win11) | **CHOSEN for HTML wallpapers** |
| **SQLite + EF Core** | Fast, ACID, queryable, migration support | Overhead vs JSON | **CHOSEN for library** |
| **JSON file** | Simple | No queries, corruption risk | Chosen for `settings.json` only |

## 2. Final Stack

```
Language:       C# 12 / .NET 8 (LTS)
UI Framework:   WinUI 3 (Windows App SDK 1.5.5+)
Rendering:      Windows Composition + Direct3D 11
Video Playback: Media Foundation (MediaPlayer) -> IWallpaperPlayer abstraction
HTML:           WebView2 (Evergreen runtime, fixed version not bundled)
Interop:        C++/Win32 Helper Project (P/Invoke via CsWin32) - Desktop Integration
Storage:        SQLite (EF Core 8) for Library, JSON for Settings
Logging:        Microsoft.Extensions.Logging + Serilog (file sink + ETW)
IPC:            Named Pipes (local only) for CLI/API
Installer:      MSIX (Store/sideload) PRIMARY + Inno Setup 6 EXE FALLBACK
```

**Helper Native Project:** `Backdrop.DesktopHost` (C# Console / CsWin32 generated P/Invoke, no C++ required unless performance profiling shows need). Host processes are `Backdrop.WallpaperHost.exe` per wallpaper instance.

**Why NOT pure C++:** Maintainability and OpenCode Agent practicality. C# with CsWin32 gives 95% of performance with 3x development speed.

## 3. Dependencies (Minimal)
* `Microsoft.WindowsAppSDK`
* `Microsoft.Web.WebView2`
* `Microsoft.EntityFrameworkCore.Sqlite`
* `CommunityToolkit.Mvvm` (MVVM)
* `CommunityToolkit.WinUI` (helpers)
* `Serilog.*`
* `CsWin32` (source generator)

No Prism, no heavy DI container beyond `Microsoft.Extensions.Hosting`.