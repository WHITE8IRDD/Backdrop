// Backdrop.WallpaperHost — WebView2 player with SECURITY.md §3 lockdown.
// Origin lock: SetVirtualHostNameToFolderMapping("appassets", DENY_CORS), load via
// https://appassets/index.html (never file://). DevTools/context-menu/zoom off,
// host objects off, web messages off unless manifest allows, NavigationStarting
// cancels outside allowlist, file:// + http://localhost blocked by default.
using Backdrop.Core;
using Backdrop.Core.Security;

namespace Backdrop.WallpaperHost.Players;

public sealed class WebView2PlayerOptions
{
    public bool AllowRemote { get; set; }
    public string? RemoteHostAllowlist { get; set; }
    public bool AllowWebMessages { get; set; }
}

public sealed class WebView2Player : IWallpaperPlayer
{
    private readonly WebView2PlayerOptions _opts;
    private WallpaperRecord? _wall;
    public PlaybackState State { get; private set; } = PlaybackState.Stopped;

    /// <summary>Lockdown profile applied to CoreWebView2.Settings at init. Kept as data so the
    /// WinUI/WebView2 wiring in the host window applies it verbatim.</summary>
    public static class Lockdown
    {
        public const bool AreDevToolsEnabled = false;
        public const bool AreDefaultContextMenusEnabled = false;
        public const bool IsZoomControlEnabled = false;
        public const bool AreHostObjectsAllowed = false;
        public const string VirtualHost = "appassets";
        public const string EntryUrl = "https://appassets/index.html";
    }

    public WebView2Player(WebView2PlayerOptions? opts = null) => _opts = opts ?? new();

    public Task<Result> LoadAsync(WallpaperRecord wall, CancellationToken ct)
    {
        _wall = wall;
        // Real WebView2 init happens on the host window thread (needs CoreWindow/Dispatcher);
        // validate the entry point here so bad packages fail fast with Result, not crash.
        if (wall.Type == WallpaperType.HtmlLocal)
        {
            var folder = Path.GetDirectoryName(wall.LibraryPath) ?? wall.LibraryPath;
            var index = Path.Combine(folder, "index.html");
            if (!File.Exists(index) && !File.Exists(wall.LibraryPath))
                return Task.FromResult(Result.Fail("HTML wallpaper entry (index.html) not found."));
        }
        State = PlaybackState.Buffering;
        return Task.FromResult(Result.Ok());
    }

    public bool CheckNavigation(Uri uri)
        => WallpaperValidator.IsNavigationAllowed(uri, _opts.AllowRemote, _opts.RemoteHostAllowlist);

    public static string EnsureCsp(string html)
        => WallpaperValidator.HtmlNeedsCsp(html)
            ? html.Replace("</head>", WallpaperValidator.RequiredCsp + "</head>", StringComparison.OrdinalIgnoreCase)
            : html;

    public Task<Result> PlayAsync() { State = PlaybackState.Playing; return Task.FromResult(Result.Ok()); }
    public Task<Result> PauseAsync() { State = PlaybackState.Paused; return Task.FromResult(Result.Ok()); }
    public Task<Result> ResumeAsync() { State = PlaybackState.Playing; return Task.FromResult(Result.Ok()); }
    public Task<Result> StopAsync() { State = PlaybackState.Stopped; return Task.FromResult(Result.Ok()); }
    public Task<Result> SetVolumeAsync(double v) => Task.FromResult(Result.Ok());
    public Task<Result> SetMuteAsync(bool m) => Task.FromResult(Result.Ok());
    public Task<Result> SeekAsync(TimeSpan p) => Task.FromResult(Result.Ok());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public static class PlayerFactory
{
    public static IWallpaperPlayer Create(WallpaperType t) => t switch
    {
        WallpaperType.Video or WallpaperType.Gif => new MediaFoundationPlayer(),
        WallpaperType.HtmlLocal or WallpaperType.HtmlRemoteUrl => new WebView2Player(),
        _ => throw new NotSupportedException($"Wallpaper type {t} not supported in V1."),
    };
}
