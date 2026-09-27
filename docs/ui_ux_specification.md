# UI_UX_SPEC.md

## 1. Design Philosophy
* Windows 11 native: Mica backdrop ( `MicaController` ), acrylic not overused, rounded 8px, subtle elevation.
* Palette: Neutral. `WinUI` system theme (light/dark auto). Accent color follows Windows accent. No neon gradients.
* Typography: `Segoe UI Variable`. Hierarchy via weight/size, not color explosion.
* Motion: 150-200ms ease-out, only for page transitions and hover. No bounce.
* Density: Comfortable (like Settings app). Excellent whitespace. List cards, not dense table.

## 2. Layout

**Window:** Minimum 960x640, default 1120x720, center. TitleBar: `ExtendsContentIntoTitleBar` with integrated caption buttons.

**NavigationView (Left):**
* Library (Grid)
* Displays
* Performance
* Settings
* About

Top header: Search box + Import button (primary).

**Library Page:**
* Top Filter chips: All | Video | GIF | HTML
* Sort: Last Used | Date Added | Name
* Grid of `WallpaperCard`: 320x180 thumbnail, title, subtitle (1920x1080 • MP4 • 12s), hover shows Play icon, active indicator dot + "Display 1" badge, context menu (Apply to.., Span, Remove, Show in Explorer, Properties)
* Empty state: elegant illustration + "Drop video or folder here"

**Preview / Details Flyout:** When card selected, right pane (or dialog) shows larger preview (auto-play muted loop), metadata (path, size, hash, duration), buttons: `Apply`, `Apply to Monitor...`, `Remove`.

**Import Flow:** Drag overlay (dashed border + "Drop to import"), File Picker fallback, progress bar, success toast, error inline.

**Displays Page:** Minimap + per-monitor cards with dropdown "Choose wallpaper". Span toggle.

**Performance Page:** Toggles for each pause rule with explanatory subtitles. FPS limiter dropdown.

**Settings Page:** Grouped via `SettingsExpander` pattern like Windows 11 Settings app. Sections: General, Wallpaper, Performance, Advanced (cache/logs).

## 3. Interaction Details
* Apply is instant, no confirmation.
* Remove requires confirmation dialog.
* Right-click tray -> immediate pause toggle (no UI lag).
* All actions show `InfoBar` or `TeachingTip` not modal.

## 4. Accessibility
* High contrast support, Narrator labels, keyboard NavView navigation, focus visuals.
* Minimum contrast 4.5:1.

## 5. Iconography
* Fluent Icons (Segoe Fluent Icons / SymbolIcon). No custom gamer icons.