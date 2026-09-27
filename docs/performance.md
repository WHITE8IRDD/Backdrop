# PERFORMANCE.md

## 1. Goals
Idle overhead of app (excluding wallpaper video decode) must be negligible. Wallpaper itself uses HW decode (~ 2-8% GPU for 1080p30, ~ 5-15% for 4K30 depending on GPU).

## 2. Pause Rules (Configured in Settings)

| Trigger | Default V1 | Resume Condition |
|---|---|---|
| Fullscreen exclusive app / Fullscreen borderless game | Pause | Foreground no longer fullscreen for 1s |
| Foreground app is maximized (covers work area) | Optional Pause (off by default) | Window restored |
| Battery Saver / Power > Battery | Pause when BatterySaver ON | AC or Saver off |
| Remote Desktop session (WTS) | Pause | Session local again |
| Lock Screen (`SessionSwitchReason.SessionLock`) | Pause | Unlock |
| Display Off / Screensaver | Pause | `WM_POWERBROADCAST PBT_POWERSETTINGCHANGE` monitor on |
| User idle > X? | Not in V1 | - |
| Multiple monitors: only visible monitors play | Auto | - |

## 3. Event-Driven Architecture (NO high-frequency polling)

**DO NOT poll every 100ms.**

Implement:

* **Foreground/fullscreen detection:** `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, ...)` + `WinEventHook` callback. On foreground change, check `IsForegroundFullscreen()`. `IsForegroundFullscreen` uses `GetForegroundWindow`, `GetWindowRect`, `GetMonitorInfo`, compare rect == monitor bounds, and style `WS_BORDER` absence. Debounce 500ms. Fallback timer only while a fullscreen candidate is active: 1000ms interval (not constant).

* **Maximize detection:** `EVENT_SYSTEM_MINIMIZEEND/START` + `IsWindowMaximized`.

* **Power:** `SystemEvents.PowerModeChanged` + `PowerSettingRegisterNotification` for `GUID_CONSOLE_DISPLAY_STATE`, `GUID_ACDC_POWER_SOURCE`, `GUID_POWER_SAVER_STATUS`.

* **Session:** `SystemEvents.SessionSwitch` (Lock/Unlock/RDP).

* **Display change:** `WM_DISPLAYCHANGE`, `WM_DPICHANGED`.

* **Battery:** `Windows.System.Power.PowerManager` UWP API.

**Pause Flow:**
```
WinEvent Foreground -> PerformanceManager.Evaluate() -> shouldPause? -> IWallpaperEngine.PauseAllAsync(PauseReason.FullscreenApp)
                     -> logs reason
                     -> host pauses media (MediaPlayer.Pause) and lowers process priority
```

**Resume Flow:** Same Evaluate, if no rule blocks -> ResumeAll.

**Optimization:** When paused, disconnect MediaPlayer `RealTimePlayback = false`, set `host process priority = IDLE`, optionally hide window.

## 4. FPS & Quality Controls
* Settings: `FpsLimit` (30/60/Unlimited) -> if 30, set `MediaPlayer.PlaybackRate = 1` but host throttles composition: `CompositionTarget.Rendering` not needed; MF handles rate. So we implement by lowering video FPS via `MediaPlaybackItem`? Simpler: V1 documents that FPS limit is advisory; actual via `DisplayInformation` vsync.
* `HardwareAcceleration: true` default. If false, uses software decode (not recommended).
* On 4K/ High Refresh: recommend quarter-res preview thumbnails, not full video.

## 5. Targets (on Intel UHD 620 / RTX 3060 reference)

* Host 1080p30 HW: 1-3% CPU, 5% GPU
* Paused: 0% CPU/GPU, 35MB private
* Main app idle: 0% CPU, 80-120MB