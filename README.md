# Backdrop

Lightweight, native Windows 11 live-wallpaper app. Original implementation inspired by
Lively's feature set. Reliability + Performance + Clean UI > Feature Bloat.

## V1 scope
Video (MP4/WebM), GIF, offline HTML (sandboxed WebView2), Library, Preview,
per-monitor / span / duplicate, tray, startup, event-driven pause/resume
(fullscreen, maximize-optional, battery, lock, RDP, display-off),
mute/volume, loop, fill/fit/stretch, drag-drop import, thumbnails, file picker, minimal CLI.

## Structure
- `src/Backdrop.App` — WinUI 3 main process (never renders video)
- `src/Backdrop.WallpaperHost` — per-wallpaper renderer, parented under WorkerW
- `src/Backdrop.Core` — models, `Result<T>`, pause evaluator, validators
- `src/Backdrop.Infrastructure` — SQLite catalog, import, settings, engine, perf manager, pipe IPC
- `src/Backdrop.DesktopInterop` — Progman/WorkerW, monitor enum, fullscreen detection
- `tests/Backdrop.Tests` — xUnit + FluentAssertions gate
- `installer/` — MSIX manifest + Inno Setup fallback
- `docs/` — spec source of truth

## Commands
```powershell
dotnet build Backdrop.sln -c Release
dotnet test tests/Backdrop.Tests -c Release
pwsh build/publish.ps1
pwsh build/verify.ps1
```

Runtime data lives only in `%AppData%\Backdrop\` (settings.json, wallpapers.db,
Library\{guid}\, Cache\Thumbnails\, Logs\, WebView2\{wallId}\).

## Build notes (SDK-only agents, no VS AppX workload)
- WinAppSDK pinned to `1.5.240802000` (latest 1.5 servicing): `1.5.240627000`'s
  `XamlCompiler.exe` exits silently (code 1) in this environment.
- `Backdrop.App.csproj` pins `RuntimeIdentifiers=win-x64` (MrtCore injects
  `win10-*-aot` RIDs unknown to the .NET 8 SDK → NETSDK1083) and disables
  VS-only packaging targets (`AppxGeneratePriEnabled=false`,
  `_PrepareMsixPackage=false`, `CopyLocalFilesOutputGroup` override in
  `src/Backdrop.App/Directory.Build.targets`). VS/MSIX flows can re-enable
  with `-p:` flags. MSIX packaging itself runs via `installer/`.
- WinUI 3 has no `SearchBox` (UWP-only; the XAML compiler crashes on it).
  Library search uses `AutoSuggestBox`.
- `OverlappedPresenter.PreferredMinimumWidth/Height` is not in the resolved
  projection here; min window size (960x640) is enforced via a
  `WM_GETMINMAXINFO` subclass in `MainWindow` instead.
- WinUI selection events fire during InitializeComponent: code-behind Bind()
  must null-guard x:Name fields (LibraryPage).
- Page XBFs deploy via a BackdropCopyXbfToOutput target (the stock PRI pipeline
  needs VS tasks); without it only App/MainWindow pages load.

## Run notes (Dynamic Dependency)
- Unpackaged startup needs a COMPLETE 1.5 runtime set (Framework + Main +
  Singleton + DDLM). Symptom of a broken set: silent exit 0x80670016
  ("Package dependency criteria could not be resolved") from the bootstrap
  auto-initializer before managed code runs. Repair with the official 1.5
  runtime installer, verify Main.1.5/DDLM presence, then relaunch.
- Exit-code forensics: -2140733418 = 0x80670016 (NOT 0x80070422); verify with
  `"0x{0:X8}" -f $code` before theorizing. certutil -error decodes HRESULTs.
- App.Trace() writes %AppData%\Backdrop\Logs\startup.log synchronously and is
  the first-line diagnostic for startup failures (Serilog flushes too late).
