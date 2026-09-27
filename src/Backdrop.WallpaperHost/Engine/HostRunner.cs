// Backdrop.WallpaperHost — host window + player lifecycle (WALLPAPER_ENGINE.md §2/§3/§5).
using Backdrop.Core;
using Backdrop.DesktopInterop;
using Backdrop.WallpaperHost.Players;
using System.Runtime.InteropServices;

namespace Backdrop.WallpaperHost.Engine;

internal sealed class HostRunner : IDisposable
{
    private readonly string _monitor;
    private readonly Guid _wallpaperId;
    private readonly string _libraryPath;
    private readonly WallpaperType _type;
    private readonly nint _parentHwnd;
    private readonly DesktopIntegrationService _desktop = new();
    private IWallpaperPlayer? _player;
    private nint _hwnd;
    private bool _disposed;

    public HostRunner(dynamic opts)
    {
        _monitor = opts.Monitor;
        _wallpaperId = opts.WallpaperId;
        _libraryPath = opts.LibraryPath;
        _type = opts.Type;
        _parentHwnd = opts.ParentHwnd;
    }

    public async Task<int> RunAsync()
    {
        // Per-monitor V2 DPI awareness (WALLPAPER_ENGINE.md §1).
        SetProcessDpiAwarenessContext(new nint(-4)); // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
        _hwnd = CreateHostWindow();
        if (_hwnd == 0)
            return 3;

        // Attach under WorkerW at monitor bounds (or virtual screen for span).
        var monitors = new MonitorService().GetMonitors();
        RectPx bounds = _monitor == "::span"
            ? _desktop.GetVirtualScreen()
            : monitors.FirstOrDefault(m => m.DeviceId == _monitor)?.Bounds ?? _desktop.GetVirtualScreen();
        var attach = _desktop.AttachHost(_hwnd, bounds);
        if (!attach.IsSuccess)
            return 4;

        var record = new WallpaperRecord(_wallpaperId, _wallpaperId.ToString("N"), _type,
            _libraryPath, _libraryPath, string.Empty, 1920, 1080, null, 30, 0, string.Empty,
            DateTime.UtcNow, DateTime.UtcNow, []);
        _player = PlayerFactory.Create(_type);
        var load = await _player.LoadAsync(record, CancellationToken.None);
        if (!load.IsSuccess)
            return 5;
        var play = await _player.PlayAsync();
        if (!play.IsSuccess)
            return 6;

        // Minimal message loop; pipe commands (play/pause/stop/heartbeat) arrive via stdin JSON lines.
        _ = Task.Run(ListenStdinLoop);
        return MessageLoop();
    }

    private void ListenStdinLoop()
    {
        string? line;
        while ((line = Console.ReadLine()) is not null)
        {
            try
            {
                if (line.Contains("\"pause\""))
                    _player?.PauseAsync().GetAwaiter().GetResult();
                else if (line.Contains("\"resume\"") || line.Contains("\"play\""))
                    _player?.ResumeAsync().GetAwaiter().GetResult();
                else if (line.Contains("\"stop\""))
                    break;
                else if (line.Contains("\"heartbeat\""))
                    Console.WriteLine("{\"ok\":true,\"state\":\"" + _player?.State + "\"}");
            }
            catch { /* never crash host on bad command */ }
        }
    }

    private static int MessageLoop()
    {
        while (true)
        {
            int r = GetMessage(out var msg, 0, 0, 0);
            if (r == 0)
                return (int)msg.wParam;
            if (r == -1)
                return 1;
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    private static readonly WndProc _wndProcKeepAlive = DefWindowProcForwarder;

    private nint CreateHostWindow()
    {
        const string cls = "BackdropWallpaperHost";
        var wc = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcKeepAlive),
            hInstance = GetModuleHandle(null),
            lpszClassName = cls
        };
        RegisterClassEx(ref wc);
        return CreateWindowEx(0, cls, "", 0x80000000 | 0x10000000 | 0x40000000, // WS_POPUP|WS_VISIBLE|WS_CHILD
            0, 0, 64, 64, _parentHwnd, 0, wc.hInstance, 0);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try { _player?.StopAsync().GetAwaiter().GetResult(); } catch { }
        _player?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (_hwnd != 0)
            DestroyWindow(_hwnd);
    }

    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(nint v);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassEx(ref WNDCLASSEX w);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint CreateWindowEx(int ex, string cls, string name, uint style, int x, int y, int w, int h, nint parent, nint menu, nint inst, nint p);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint h);
    [DllImport("user32.dll")] private static extern int GetMessage(out MSG m, nint w, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG m);
    [DllImport("user32.dll")] private static extern nint DispatchMessage(ref MSG m);
    [DllImport("user32.dll")] private static extern nint DefWindowProc(nint h, uint m, nint w, nint l);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? n);

    private delegate nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam);
    private static nint DefWindowProcForwarder(nint h, uint m, nint w, nint l) => DefWindowProc(h, m, w, l);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX { public int cbSize; public uint style; public nint lpfnWndProc; public int cbCls; public int cbWnd; public nint hInstance; public nint hIcon; public nint hCursor; public nint hbr; public string? lpszMenu; public string lpszClassName; public nint hIconSm; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public nint hwnd; public uint message; public nint wParam; public nint lParam; public uint time; public int x; public int y; }
}
