// Backdrop.Core — runtime paths. %AppData%\Backdrop only (ARCHITECTURE.md §4).
namespace Backdrop.Core;

public static class BackdropPaths
{
    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Backdrop");

    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string DatabaseFile => Path.Combine(Root, "wallpapers.db");
    public static string LibraryDir => Path.Combine(Root, "Library");
    public static string ThumbnailCacheDir => Path.Combine(Root, "Cache", "Thumbnails");
    public static string LogsDir => Path.Combine(Root, "Logs");
    public static string WebView2Root => Path.Combine(Root, "WebView2");

    public static string WebView2DirFor(Guid wallId) => Path.Combine(WebView2Root, wallId.ToString("N"));
    public static string LibraryDirFor(Guid id) => Path.Combine(LibraryDir, id.ToString("N"));

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LibraryDir);
        Directory.CreateDirectory(ThumbnailCacheDir);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(WebView2Root);
    }
}
