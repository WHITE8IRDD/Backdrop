// Backdrop.Core — domain models (ARCHITECTURE.md §5, WALLPAPER_ENGINE.md §3).
namespace Backdrop.Core;

public enum WallpaperType { Video, Gif, HtmlLocal, HtmlRemoteUrl }
public enum PlaybackState { Stopped, Buffering, Playing, Paused, Error }
public enum SpawnMode { PerMonitor, Span, Duplicate }
public enum PauseReason
{
    User,
    FullscreenApp,
    MaximizedApp,
    BatterySaver,
    RemoteDesktop,
    LockScreen,
    DisplayOff,
    InactiveWindow
}

public enum FillMode { Fill, Fit, Stretch }

/// <summary>Library record. Stored in SQLite (wallpapers.db), files under Library\{guid}\.</summary>
public sealed record WallpaperRecord(
    Guid Id,
    string Title,
    WallpaperType Type,
    string OriginalPath,
    string LibraryPath,
    string ThumbnailPath,
    int Width,
    int Height,
    double? DurationSec,
    double? Fps,
    long FileSizeBytes,
    string Sha256,
    DateTime AddedAt,
    DateTime LastUsedAt,
    string[] Tags);

/// <summary>Physical pixel rectangle in virtual-screen coordinates (negative X allowed).</summary>
public readonly record struct RectPx(int X, int Y, int Width, int Height);

public sealed record MonitorInfo(
    string DeviceId,          // \\.\DISPLAYx — stable key (MULTI_MONITOR.md)
    string FriendlyName,
    RectPx Bounds,            // virtual-screen coords
    RectPx WorkArea,
    bool IsPrimary,
    uint Dpi,
    double Scale,
    int RefreshHz,
    string AdapterName);

public sealed record WallpaperInstance(
    Guid WallpaperId,
    string MonitorDeviceId,
    int HostProcessId,
    PlaybackState State,
    PauseReason? PausedBy);
