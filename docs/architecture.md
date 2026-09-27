# ARCHITECTURE.md

## 1. High-Level Architecture

```
[ WinUI 3 Main Process: Backdrop.exe ]  ---- Named Pipe ---> [ Host Process(es): Backdrop.WallpaperHost.exe x N ]
        |   UI, Library, Settings, Tray, Performance Manager, Monitor Service
        |
        |----> Config ( %AppData%\Backdrop\ )
        |----> SQLite DB
        |----> Cache (thumbnails)
        |
        V
Win32 Desktop Integration (Progman -> WorkerW -> Host HWND parent)
```

**Isolation Principle:** Wallpaper rendering runs OUT OF PROCESS. If codec crashes, main UI stays alive. Main process is broker.

## 2. Solution Structure

```
/src
  /Backdrop.App                 # WinUI 3 App (entry point, App.xaml, MainWindow)
  /Backdrop.Core                # Domain models, interfaces, pure logic (no UI)
  /Backdrop.Infrastructure      # SQLite, FileSystem, Win32 Interop, MediaProbe, Thumbnailer
  /Backdrop.WallpaperHost       # Minimal WinUI-less window host for video/WebView2 rendering
  /Backdrop.DesktopInterop      # CsWin32 wrappers: User32, Gdi32, DwmApi, Shcore
/tests
/assets
/docs
/build
/installer (MSIX manifest, Inno script)
```

## 3. Core Modules & Services

| Service | Interface | Responsibility |
|---|---|---|
| `WallpaperCatalogService` | `IWallpaperCatalog` | CRUD library, hash, duplicate detection |
| `WallpaperEngineService` | `IWallpaperEngine` | Manage host process lifecycle, assign to monitors |
| `MonitorService` | `IMonitorService` | EnumDisplayMonitors, WM_DISPLAYCHANGE, DPI, ranking |
| `DesktopIntegrationService` | `IDesktopIntegration` | Find Progman, Create WorkerW, parent host HWND, Z-order |
| `PerformanceManager` | `IPerformanceManager` | Subscribe to system events, decide Pause/Resume |
| `PlaybackService` | `IWallpaperPlayer` (per host) | Play/Pause/Volume/Loop abstraction |
| `ThumbnailService` | `IThumbnailService` | FFmpeg-like? Use MediaPlayer seek + RenderTargetBitmap |
| `SettingsService` | `ISettingsService` | JSON read/write with file watcher, defaults |
| `TrayService` | `ITrayService` | NotifyIcon, context menu via H.NotifyIcon.WinUI |
| `StartupService` | `IStartupService` | Registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` or StartupTask (MSIX) |
| `Logger` | `ILogger<T>` | Serilog |

## 4. Data Layer

* Settings: `%AppData%\Backdrop\settings.json` (atomic write: write tmp -> move)
* Library: `%AppData%\Backdrop\wallpapers.db` (SQLite)
* Assets: `%AppData%\Backdrop\Library\{guid}\` (original file copied, not moved)
* Thumbnails: `%AppData%\Backdrop\Cache\Thumbnails\{hash}.jpg`
* Logs: `%AppData%\Backdrop\Logs\backdrop-*.log` (rolling, 10MB max)

No `Program Files` writes at runtime. All user-scope.

## 5. Interface Definitions (Pseudocode C#)

```csharp
enum WallpaperType { Video, Gif, HtmlLocal, HtmlRemoteUrl }
enum PlaybackState { Stopped, Playing, Paused, Error, Buffering }
enum SpawnMode { PerMonitor, Span, Duplicate }

record WallpaperRecord(
  Guid Id, string Title, WallpaperType Type, string OriginalPath,
  string LibraryPath, string ThumbnailPath, int Width, int Height,
  double? DurationSec, double? Fps, long FileSizeBytes, string Sha256,
  DateTime AddedAt, DateTime LastUsedAt, string[] Tags);

interface IWallpaperPlayer : IAsyncDisposable {
  Task LoadAsync(WallpaperRecord wall, CancellationToken ct);
  Task PlayAsync();
  Task PauseAsync();
  Task ResumeAsync();
  Task StopAsync();
  Task SetVolumeAsync(double vol); // 0-1
  Task SetMuteAsync(bool mute);
  Task SeekAsync(TimeSpan position);
  PlaybackState State { get; }
}

interface IWallpaperEngine {
  Task ApplyAsync(WallpaperRecord wall, string monitorDeviceId, SpawnMode mode);
  Task ApplySpanAsync(WallpaperRecord wall, IReadOnlyList<string> monitors);
  Task PauseAllAsync(PauseReason reason);
  Task ResumeAllAsync();
  Task RemoveAsync(Guid wallpaperId);
  IReadOnlyList<WallpaperInstance> ActiveInstances { get; }
}

record MonitorInfo(string DeviceId, string FriendlyName, Rect Bounds, Rect WorkArea,
  bool IsPrimary, uint Dpi, double Scale, int RefreshHz, string AdapterName);

interface IMonitorService {
  IReadOnlyList<MonitorInfo> GetMonitors();
  event EventHandler MonitorsChanged;
}

enum PauseReason { User, FullscreenApp, MaximizedApp, BatterySaver, RemoteDesktop, LockScreen, InactiveWindow }
```

## 6. Process Model
* Main process never renders video itself.
* `Backdrop.WallpaperHost.exe --monitor \\.\DISPLAY1 --wallpaper-id {guid} --parent-hwnd 0x1234 --pipe backdrop-host-xyz`
* Host is a borderless WS_POPUP | WS_CHILD window owned by WorkerW. No title bar.
* IPC: Named Pipe JSON-Rpc 2.0 lightweight (play/pause/stop/heartbeat). Heartbeat every 5s; if host dies -> engine restarts or shows fallback static.

## 7. Error Handling Strategy
* All services return `Result<T>` pattern; never throw across process boundary.
* Global handlers: `AppDomain.UnhandledException`, `Application.UnhandledException`
* Host crash -> log dump, notify UI toast, fallback to solid color, offer retry.