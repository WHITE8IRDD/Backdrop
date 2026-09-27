# TESTING.md

## 1. Strategy

| Level | Framework | Scope |
|---|---|---|
| Unit | xUnit + FluentAssertions | Catalog, Settings, MonitorService logic, Performance evaluation pure functions |
| Integration | xUnit (with TestHost) | SQLite migrations, Import pipeline (temp folder), Thumbnail generation |
| UI | WinAppDriver + Appium / FlaUI | Navigation, Apply wallpaper, Tray menu |
| Perf | BenchmarkDotNet + custom harness | Startup, RAM, pause latency |
| Manual QA | Checklist | Multi-monitor hotplug, Explorer crash, Game fullscreen |

## 2. Key Test Cases

* **WallpaperEngineTests:** Apply-PerMonitor, Span, RemoveWhilePlaying, HostCrashRecovery
* **MonitorTests:** Simulate `WM_DISPLAYCHANGE`, ensure reattachment, mixed DPI
* **PerformanceTests:** Foreground fullscreen -> Pause, battery saver -> Pause, RemoteDesktop -> Pause (`MockWtsApi`)
* **ImportTests:** Corrupt MP4, missing codec, Zip Slip path traversal, duplicate hash, 600MB file rejected
* **SecurityTests:** Html wallpaper attempts `fetch('https://evil.com')` blocked, navigation to `file://c:/` blocked
* **StartupTests:** App cold start < 900ms, tray appears, autostart registry

## 3. Acceptance Criteria (Release Gate)
* 80%+ line coverage on Core
* No crash on 10 corrupt files suite
* Passes on Win11 22H2 + 24H2, single + dual + triple monitor, 125%/150% DPI
* Pause on fullscreen verified with 3 games (borderless + exclusive)
* Explorer kill/restart -> wallpaper restores within 2s
* Memory leak: 30 min play/pause cycles no > 10% growth

## 4. Automation
* CI: GitHub Actions Windows runner (`windows-11` image). Build MSIX, run unit+integration.
* UI tests run nightly (requires interactive session).