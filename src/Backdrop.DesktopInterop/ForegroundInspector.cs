// Backdrop.DesktopInterop — fullscreen / maximize detection (PERFORMANCE.md §3).
// IsForegroundFullscreen: rect == monitor bounds AND no WS_BORDER/WS_CAPTION.
using Backdrop.DesktopInterop.Native;

namespace Backdrop.DesktopInterop;

public static class ForegroundInspector
{
    public static bool IsForegroundFullscreen(IReadOnlyList<Core.MonitorInfo> monitors)
    {
        nint fg = User32.GetForegroundWindow();
        if (fg == 0)
            return false;
        if (!User32.GetWindowRect(fg, out var rc))
            return false;
        int style = User32.GetWindowLong(fg, User32.GWL_STYLE);
        bool borderless = (style & User32.WS_BORDER) == 0 || (style & User32.WS_CAPTION) == 0;

        int w = rc.Right - rc.Left, h = rc.Bottom - rc.Top;
        foreach (var m in monitors)
        {
            if (rc.Left == m.Bounds.X && rc.Top == m.Bounds.Y &&
                w == m.Bounds.Width && h == m.Bounds.Height)
                return true;
            // Borderless window covering full monitor counts even with 1px tolerance.
            if (borderless && w >= m.Bounds.Width - 1 && h >= m.Bounds.Height - 1 &&
                Math.Abs(rc.Left - m.Bounds.X) <= 1 && Math.Abs(rc.Top - m.Bounds.Y) <= 1)
                return true;
        }
        return false;
    }

    public static bool IsForegroundMaximized()
    {
        nint fg = User32.GetForegroundWindow();
        return fg != 0 && User32.IsZoomed(fg);
    }
}
