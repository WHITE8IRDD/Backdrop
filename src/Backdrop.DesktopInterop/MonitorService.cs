// Backdrop.DesktopInterop — monitor enumeration (MULTI_MONITOR.md §1).
// Uses EnumDisplayMonitors + GetMonitorInfoEx. NEVER Screen.AllScreens (DPI bugs).
using Backdrop.Core;
using Backdrop.DesktopInterop.Native;
using System.Runtime.InteropServices;

namespace Backdrop.DesktopInterop;

public sealed class MonitorService : IMonitorService
{
    public event EventHandler? MonitorsChanged;

    public void NotifyMonitorsChanged() => MonitorsChanged?.Invoke(this, EventArgs.Empty);

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();
        User32.MonitorEnumProc callback = (nint hMon, nint hdc, ref User32.RECT rc, nint data) =>
        {
            var mi = new User32.MONITORINFOEX { cbSize = Marshal.SizeOf<User32.MONITORINFOEX>() };
            if (User32.GetMonitorInfo(hMon, ref mi))
            {
                bool primary = (mi.dwFlags & 1) == 1; // MONITORINFOF_PRIMARY
                list.Add(new MonitorInfo(
                    DeviceId: mi.szDevice,
                    FriendlyName: mi.szDevice,
                    Bounds: new RectPx(mi.rcMonitor.Left, mi.rcMonitor.Top,
                        mi.rcMonitor.Right - mi.rcMonitor.Left, mi.rcMonitor.Bottom - mi.rcMonitor.Top),
                    WorkArea: new RectPx(mi.rcWork.Left, mi.rcWork.Top,
                        mi.rcWork.Right - mi.rcWork.Left, mi.rcWork.Bottom - mi.rcWork.Top),
                    IsPrimary: primary,
                    Dpi: 96, Scale: 1.0, RefreshHz: 60, AdapterName: string.Empty));
            }
            return true;
        };
        User32.EnumDisplayMonitors(0, 0, callback, 0);
        return list.OrderByDescending(m => m.IsPrimary).ToList();
    }
}
