// Backdrop.Infrastructure — media probe (WALLPAPER_ENGINE.md §4 step 4).
// V1 uses filename/extension heuristics + safe defaults; full MF metadata
// (Windows.Media.Editing.MediaClip) is resolved in WallpaperHost where MF is available.
using Backdrop.Core;

namespace Backdrop.Infrastructure.Media;

public sealed record ProbeResult(int Width, int Height, double? DurationSec, double? Fps);

public static class MediaProbe
{
    public static ProbeResult Probe(string libraryPath, WallpaperType type)
    {
        if (type is WallpaperType.HtmlLocal or WallpaperType.HtmlRemoteUrl)
            return new ProbeResult(1920, 1080, null, null);
        if (type == WallpaperType.Gif)
            return new ProbeResult(800, 600, null, null);
        // Video defaults; host updates the record once MF opens the file.
        return new ProbeResult(1920, 1080, null, 30);
    }
}
