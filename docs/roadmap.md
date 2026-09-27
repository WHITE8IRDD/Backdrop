# ROADMAP.md

## Phase 0: Architecture (Week 1)
* Create solution, projects, CI, lint, `AGENTS.md`, DB schema.
* Deliverable: Builds, tests green, docs reviewed.
* Criteria: `dotnet build` passes.

## Phase 1: Core Windows App Shell (Week 2)
* WinUI 3 NavigationView, Mica, Pages stubs, SettingsService JSON, Tray (H.NotifyIcon), StartupService.
* Criteria: App launches to empty Library, tray works, settings persist.

## Phase 2: Wallpaper Engine & Desktop Integration (Week 3-4)
* DesktopInterop, Host process, MediaFoundationPlayer, WorkerW attach.
* Criteria: `Apply` MP4 to primary monitor works after restart.

## Phase 3: Library / Import (Week 4-5)
* SQLite catalog, ImportService, ThumbnailService, DragDrop, Library Grid.
* Criteria: Drag MP4 -> appears in grid with thumbnail.

## Phase 4: Multi-Monitor (Week 5-6)
* MonitorService, per-monitor + span modes, Displays page UI, hotplug recovery, DPI.
* Criteria: 2 monitors, different wallpapers simultaneously; unplug rest