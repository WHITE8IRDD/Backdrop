// Backdrop.WallpaperHost — out-of-process renderer (ARCHITECTURE.md §6).
// Usage: Backdrop.WallpaperHost.exe --monitor \\.\DISPLAY1 --wallpaper-id {guid} --library-path <file> --type Video --parent-hwnd 0x1234 --pipe backdrop-host-xyz
// Main process never renders video itself; codec crash must not kill UI.
using Backdrop.Core;
using Backdrop.DesktopInterop;
using Backdrop.WallpaperHost.Engine;
using Backdrop.WallpaperHost.Players;

static class Program
{
    static async Task<int> Main(string[] args)
    {
        var opts = Parse(args);
        if (opts is null)
        {
            Console.Error.WriteLine("Usage: --monitor <id> --wallpaper-id <guid> --library-path <path> --type <Video|Gif|HtmlLocal|HtmlRemoteUrl> [--parent-hwnd <hex>]");
            return 2;
        }

        BackdropPaths.EnsureCreated();
        using var host = new HostRunner(opts);
        return await host.RunAsync();
    }

    private sealed record HostOptions(string Monitor, Guid WallpaperId, string LibraryPath, WallpaperType Type, nint ParentHwnd);

    private static HostOptions? Parse(string[] args)
    {
        string? Get(string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
        var monitor = Get("--monitor");
        var wid = Get("--wallpaper-id");
        var lib = Get("--library-path");
        var type = Get("--type");
        if (monitor is null || wid is null || lib is null || type is null)
            return null;
        if (!Guid.TryParse(wid, out var guid))
            return null;
        if (!Enum.TryParse<WallpaperType>(type, out var wt))
            return null;
        nint parent = 0;
        var ph = Get("--parent-hwnd");
        if (ph is not null)
        {
            try { parent = (nint)Convert.ToInt64(ph.Replace("0x", ""), 16); } catch { parent = 0; }
        }
        return new HostOptions(monitor, guid, lib, wt, parent);
    }
}
