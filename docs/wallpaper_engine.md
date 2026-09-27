# WALLPAPER_ENGINE.md

## 1. How Wallpaper Becomes Desktop Background (Win32)

Classic Progman technique (stable since Win7, used by Lively/WE):

1. Find `Progman` window (`FindWindow("Progman", "Program Manager")`)
2. Send `0x052C` message to spawn `WorkerW` behind icons: `SendMessageTimeout(Progman, 0x052C, 0,0, SMTO_ABORTIFHUNG, 1000, out _)`
3. Enumerate `WorkerW` windows: `EnumWindows` -> find `WorkerW` with `SHELLDLL_DefView` child. Next `WorkerW` sibling is our target.
4. Create host HWND as child of that `WorkerW` via `SetParent(hostHwnd, workerW)`
5. Position host to monitor bounds via `SetWindowPos(hostHwnd, HWND_BOTTOM, x,y,w,h, SWP_NOACTIVATE)`

**Spanning:** Create one host sized to virtual screen rect (`GetSystemMetrics(SM_CXVIRTUALSCREEN)`).

**Per-Monitor:** One host per monitor.

**DPI Awareness:** Process is Per-Monitor V2 DPI aware (`SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)`).

## 2. Playback Layer (`IWallpaperPlayer` Implementations)

* `MediaFoundationPlayer` : Uses `MediaPlayer` + `MediaSource.CreateFromUri` inside host. Renders to `SwapChainPanel` or `MediaPlayerElement`. HW acceleration `MediaPlayer.IsHardwareAccelerated = true`. Loop: `MediaPlayer.IsLoopingEnabled = true`. Handles MP4, WebM (if system codec via WebM VP9 extension), WMV, MOV, AVI where codec available.

* `GifPlayer` : Decodes via `Windows.Graphics.Imaging` or treats as video; loop via `MediaPlayer` or `AnimatedVisualPlayer` (Lottie). Prefer MP4 path.

* `WebView2Player` : Host contains `WebView2` control docked fill. Init with user data folder `%AppData%\Backdrop\WebView2\{wallId}` isolated. `CoreWebView2.Settings.AreDevToolsEnabled = false`, `IsWebMessageEnabled = false` unless wallpaper manifest allows.

**Factory:**
```csharp
IWallpaperPlayer Create(WallpaperType t) => t switch {
  WallpaperType.Video or WallpaperType.Gif => new MediaFoundationPlayer(),
  WallpaperType.HtmlLocal or WallpaperType.HtmlRemoteUrl => new WebView2Player(),
  _ => throw new NotSupportedException()
};
```

## 3. Lifecycle State Machine

```
[Stopped] --Load--> [Buffering] --Ready--> [Playing]
[Playing] --Pause(PauseReason)--> [Paused]
[Paused] --Resume--> [Playing]
[Playing/Paused] --Stop--> [Stopped]
[Any] --Error--> [Error] --Reload--> [Buffering]
```

All transitions via `IWallpaperEngine`. Engine debounces rapid monitor changes (500ms).

## 4. Import Pipeline

1. DragDrop/FilePicker -> `ImportService.ImportAsync(pathOrUrl)`
2. Validate extension whitelist: `.mp4 .webm .mov .avi .mkv .m4v .gif .html .htm .zip` (zip contains html wallpapers like Lively wallpapers)
3. Compute SHA256, check duplicate hash -> prompt.
4. Probe metadata: `MediaProbe` (using `Windows.Media.Editing.MediaClip` or FFprobe-lite if missing) gets Width/Height/Duration/FPS.
5. Copy to Library folder.
6. Generate thumbnail: Seek to 15% duration, render to bitmap via `MediaPlayer` offscreen, save JPG 400x225.
7. Insert DB row.
8. Return result.

* Zip import: unzip to Library\{guid}\project\, look for `LivelyInfo.json` or `project.json` analog? For V1 support simple: if single .html at root, treat as HtmlLocal.

## 5. Resource Discipline
* Players `Dispose()` media source on Stop.
* WebView2 `Close()` on host exit.
* Host process exits when no wallpaper assigned (no idle host).