// Backdrop.DesktopInterop — Progman -> WorkerW interop (WALLPAPER_ENGINE.md §1).
using Backdrop.Core;
using Backdrop.DesktopInterop.Native;

namespace Backdrop.DesktopInterop;

public sealed class DesktopIntegrationService : IDesktopIntegration
{
    private const uint SpawnWorkerW = 0x052C;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    public Result<nint> InitializeWorkerW()
    {
        try
        {
            nint progman = User32.FindWindow("Progman", "Program Manager");
            if (progman == 0)
                return Result<nint>.Fail("Progman window not found.");
            User32.SendMessageTimeout(progman, SpawnWorkerW, 0, 0, SMTO_ABORTIFHUNG, 1000, out _);
            return GetWorkerW();
        }
        catch (Exception ex)
        {
            return Result<nint>.Fail(ex.Message);
        }
    }

    public Result<nint> GetWorkerW()
    {
        try
        {
            nint shellView = 0;
            User32.EnumWindows((hWnd, _) =>
            {
                nint defView = User32.FindWindowEx(hWnd, 0, "SHELLDLL_DefView", null);
                if (defView != 0)
                {
                    shellView = User32.FindWindowEx(0, hWnd, "WorkerW", null);
                    return false; // stop
                }
                return true;
            }, 0);

            // The WorkerW *after* the one hosting SHELLDLL_DefView is our target.
            // EnumWindows order: next sibling WorkerW. Fallback: shellView itself if no sibling found.
            if (shellView == 0)
                return Result<nint>.Fail("WorkerW host not found (explorer may be restarting).");
            nint target = User32.FindWindowEx(0, shellView, "WorkerW", null);
            return Result<nint>.Ok(target != 0 ? target : shellView);
        }
        catch (Exception ex)
        {
            return Result<nint>.Fail(ex.Message);
        }
    }

    public Result AttachHost(nint hostHwnd, RectPx bounds)
    {
        try
        {
            var workerW = GetWorkerW();
            if (!workerW.IsSuccess)
                return Result.Fail(workerW.Error);
            User32.SetParent(hostHwnd, workerW.Value);
            User32.SetWindowPos(hostHwnd, User32.HWND_BOTTOM,
                bounds.X, bounds.Y, bounds.Width, bounds.Height,
                User32.SWP_NOACTIVATE | User32.SWP_SHOWWINDOW);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public RectPx GetVirtualScreen()
    {
        int x = User32.GetSystemMetrics(User32.SM_XVIRTUALSCREEN);
        int y = User32.GetSystemMetrics(User32.SM_YVIRTUALSCREEN);
        int w = User32.GetSystemMetrics(User32.SM_CXVIRTUALSCREEN);
        int h = User32.GetSystemMetrics(User32.SM_CYVIRTUALSCREEN);
        return new RectPx(x, y, w, h);
    }
}
