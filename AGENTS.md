# AGENTS.md — Backdrop Contributor Rules (AI + Human)

Source of truth: `docs/` (`master_plan.md`, `architecture.md`, `tech_stack.md`, `wallpaper_engine.md`,
`performance.md`, `multi_monitor.md`, `ui_ux_specification.md`, `security.md`, `testing.md`,
`installer.md`, `roadmap.md`).

## 1. Core principle
Reliability + Performance + Clean UI > Feature Bloat. Feel like a first-party Microsoft utility.

## 2. V1 scope (do NOT expand without updating roadmap)
IN: Video MP4/WebM, GIF, HTML offline (WebView2 sandboxed), Library, Preview, per-monitor / span /
duplicate, tray, startup, pause/resume (fullscreen, maximize-optional, battery, lock, RDP, display-off),
mute/volume, loop, fill/fit/stretch, drag-drop import, thumbnails, file picker, minimal CLI.
OUT (V2+): app/game capture wallpapers, shaders/HLSL, screensaver integration, interactive wallpapers,
AI wallpaper, taskbar acrylic customization, online store, cloud sync, yt-dlp.

## 3. Architecture rules
- Main process (`Backdrop.App`, WinUI 3) NEVER renders video itself. Rendering lives in
  `Backdrop.WallpaperHost.exe` x N, parented under WorkerW. Codec crash must not kill UI.
- All services return `Result<T>`; never throw across process boundary.
- Paths: `%AppData%\Backdrop\` only at runtime (`settings.json`, `wallpapers.db`,
  `Library\{guid}\`, `Cache\Thumbnails\`, `Logs\`, `WebView2\{wallId}\`). No `Program Files` writes.
- Settings writes are atomic (tmp -> move). Logs roll at 10 MB.
- IPC: local named pipe only, DACL = current user SID, token auth. Never elevate (`asInvoker`).

## 4. Performance rules (hard)
- NO high-frequency polling. Event-driven: `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`,
  `SystemEvents.PowerModeChanged/SessionSwitch`, `PowerSettingRegisterNotification`
  (`CONSOLE_DISPLAY_STATE`, `ACDC_POWER_SOURCE`, `POWER_SAVER_STATUS`), `WM_DISPLAYCHANGE`/`WM_DPICHANGED`.
  Debounce foreground re-eval 500 ms; fallback 1000 ms timer ONLY while a fullscreen candidate is active.
- Pause must yield 0% CPU/GPU; resume < 300 ms; cold start to tray < 900 ms.
- See `docs/performance.md` for the pause/resume flow. Any new trigger needs a row in that table + a test.

## 5. Security rules (hard)
- Wallpapers are untrusted. See `docs/security.md`. Enforce: extension allowlist
  (`.mp4 .webm .mov .avi .mkv .m4v .gif .html .htm .zip`), magic-bytes check, 500 MB default cap,
  zip-slip guard (`GetFullPath(entry).StartsWith(libraryRoot)`), reject `..`/absolute paths.
- HTML: `SetVirtualHostNameToFolderMapping("appassets", …DENY_CORS)`, load via
  `https://appassets/index.html` (never `file://`), inject CSP if missing, `AreHostObjectsAllowed=false`,
  `IsWebMessageEnabled=false` unless manifest allows, disable devtools/context-menu/zoom,
  `NavigationStarting` cancels outside allowlist, block `file://` + `http://localhost` by default,
  remote URL needs explicit opt-in infobar.
- Never run as admin. Pipe + CLI token required.

## 5b. Originality
Original implementation, branding, and architecture. Inspired by Lively's feature set only.
Do not copy Lively source/assets. No Lively binaries, no borrowed art.

## 6. UI rules
WinUI 3 native, Mica, Segoe UI Variable, 8 px radius, 150–200 ms ease-out only, Settings-app density.
Nav: Library | Displays | Performance | Settings | About. Min 960x640, default 1120x720.
All actions via InfoBar/TeachingTip, not modal (except Remove confirmation). Contrast ≥ 4.5:1,
Narrator labels, keyboard nav. See `docs/ui_ux_specification.md`.

## 7. Multi-monitor rules
`EnumDisplayMonitors` + `GetMonitorInfoEx` (never `Screen.AllScreens`). Key = `\\.\DISPLAYx`.
Use virtual-screen coords (negative X allowed). Per-monitor = N hosts; duplicate = N hosts same source;
span = 1 host over `SM_CXVIRTUALSCREEN`. Listen `TaskbarCreated` -> re-run `InitializeWorkerW`.
See `docs/multi_monitor.md`.

## 8. Testing gate (release blocker)
- xUnit + FluentAssertions; 80%+ line coverage on Core; BenchmarkDotNet + harness for perf.
- Must pass: corrupt-file suite (10 files, no crash), Win11 22H2 + 24H2, 1/2/3 monitors, 125%/150% DPI,
  fullscreen pause (borderless + exclusive), explorer kill/restart restores ≤ 2 s,
  30-min play/pause leak < 10% growth. See `docs/testing.md`.

## 9. Commands
- `dotnet build Backdrop.sln -c Release`
- `dotnet test tests/Backdrop.Tests -c Release`
- `pwsh build/publish.ps1` (self-contained host + app)
- `pwsh build/verify.ps1` (startup/RAM/pause smoke)

## 10. Definition of done per roadmap phase
Phase 0: builds, tests green. Phase 1: launches to empty Library, tray works, settings persist.
Phase 2: Apply MP4 to primary survives restart. Phase 3: drag MP4 -> grid + thumbnail.
Phase 4: 2 monitors different wallpapers; unplug recovers. Installer: MSIX + Inno artifacts.
