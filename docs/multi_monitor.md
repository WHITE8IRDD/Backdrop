# MULTI_MONITOR.md

## 1. Enumeration
Use `EnumDisplayMonitors` + `GetMonitorInfoEx` via CsWin32. Also subscribe to `Microsoft.UI.Windowing.DisplayArea` watcher (WinAppSDK). Store `DeviceId` = `\\.\DISPLAY1` as key. Also store `AdapterLuid` for spanning correctness.

**Do not use Screen.AllScreens (WinForms) due to DPI bugs.**

## 2. Modes

* **Per-Monitor:** Dictionary `MonitorDeviceId -> WallpaperId`. Engine creates N hosts.
* **Duplicate:** Same WallpaperId for all; still N hosts (simpler sync) or single virtual host. For V1: N hosts with same source (memory * N but safe).
* **Span:** One host covering virtual screen. Wallpaper scaling: `UniformToFill` (crop) or `Uniform` (letterbox). Configurable.

## 3. Handling Changes

| Event | Action |
|---|---|
| Monitor added/removed | `MonitorsChanged` -> recalculate assignments -> re-parent hosts. Removed monitor's wallpaper moves to primary or pauses. |
| Resolution / DPI / Orientation change | Reposition host with `SetWindowPos` to new Bounds. Re-evaluate scaling. |
| RefreshRate change | No action; MediaFoundation adapts. |
| Mixed DPI | Each host DPI-aware; use physical pixels for SetWindowPos (`GetDpiForWindow` scale). |
| Ultrawide (21:9) | `Fill` mode ensures no bars; thumbnail aspect respects monitor aspect. |

**Graceful Recovery:** If WorkerW destroyed (explorer restart), listen to `TaskbarCreated` registered message (`RegisterWindowMessage("TaskbarCreated")`). Re-run `InitializeWorkerW()` and reattach.

**Arrangement:** Do not assume monitors in row; use virtual screen coordinates. Test with negative X (left of primary).

## 4. UI for Displays
Settings -> Displays page shows interactive minimap (rectangles to scale, primary indicator). Click monitor to assign wallpaper. Show DeviceId, Resolution, Scaling.