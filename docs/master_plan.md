# MASTER_PLAN.md

## 1. Executive Summary
**Backdrop** is a lightweight, native Windows 11 live-wallpaper application. It allows users to import, manage, and display video/GIF/HTML wallpapers per-monitor, with intelligent automatic pause/resume to preserve battery and foreground performance. It is *inspired by feature set of [Lively Wallpaper](https://github.com/rocksdanister/lively)* but is an original implementation, branding, and architecture.

**Core Principle:** Reliability + Performance + Clean UI > Feature Bloat.

## 2. Vision Statement
Feel like a first-party Microsoft utility. Not a web dashboard, not a gamer tool. Calm, premium, fast, invisible when it should be.

## 3. Feature Classification (Lively Study)

After study of `rocksdanister/lively` wiki (Wallpaper types, performance, automation, cef/webview):

| Category | Feature | Verdict for V1 |
|---|---|---|
| **Essential (MUST HAVE)** | Video MP4/WebM, GIF, Wallpaper Library, Preview, Apply/Remove, Per-monitor assignment, Span across monitors, System Tray, Startup, Pause/Resume, Fullscreen Pause, Maximize Pause (optional), Battery Pause, Lock Screen Pause | **V1** |
| **Should Have** | Web/HTML (local + offline), WebView2 with sandbox, Audio Mute/Volume, Loop, Scaling Fill/Fit/Stretch, Drag & Drop Import, Thumbnail generation, File picker | **V1** |
| **Expensive / Optional** | Application/Game wallpapers (screen capture of app -> wallpaper), Shader wallpapers, ScreenSaver integration, Interactive wallpapers, AI wallpaper, Taskbar fluent/ acrylic customization | **Architect for later (V2+), DO NOT implement in V1** |
| **Nice to Have (V1 if trivial)** | YouTube URL import (via yt-dlp is heavy -> defer), CLI/API, Multi-monitor DPI hot-swap | **CLI in V1 minimal, yt-dlp in V2** |

**Rationale:** Video/GIF/HTML covers 95% of user value. App/Screensaver/Shaders require heavy process isolation and bring security/instability for low ROI.

## 4. Non-Goals for V1
* No online wallpaper store/marketplace
* No shaderToy / custom HLSL editor
* No exe capture wallpapers
* No cloud sync

## 5. Success Criteria
* Cold startup < 900ms to tray on mid-range NVMe / i5
* Idle RAM (paused) < 90MB main process, < 60MB per wallpaper host
* CPU 0% when paused
* Resume latency < 300ms
* Never crashes explorer.exe

## 6. Document Map
See `architecture.md`, `tech_stack.md`, `wallpaper_engine.md`, `performance.md`, `multi_monitor.md`, `ui_ux_spec.md`, `security.md`, `testing.md`, `installer.md`, `roadmap.md`