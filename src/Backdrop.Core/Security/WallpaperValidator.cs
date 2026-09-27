// Backdrop.Core — import validation (SECURITY.md §2/§5, WALLPAPER_ENGINE.md §4).
namespace Backdrop.Core.Security;

public static class WallpaperValidator
{
    public static readonly string[] AllowedExtensions =
        [".mp4", ".webm", ".mov", ".avi", ".mkv", ".m4v", ".gif", ".html", ".htm", ".zip"];

    public static bool IsExtensionAllowed(string path)
        => AllowedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>Magic-bytes check: MP4 ftyp, GIF89a/87a, HTML &lt;html, WebM EBML, ZIP PK.</summary>
    public static bool HasValidMagicBytes(string path, string extension)
    {
        try
        {
            using var fs = File.OpenRead(path);
            Span<byte> header = stackalloc byte[16];
            int read = fs.Read(header);
            if (read < 4)
                return false;
            return extension.ToLowerInvariant() switch
            {
                ".mp4" or ".mov" or ".m4v" => ContainsAscii(header, "ftyp") || ContainsAscii(header, "moov"),
                ".webm" or ".mkv" => header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3,
                ".avi" => ContainsAscii(header, "RIFF"),
                ".gif" => ContainsAscii(header, "GIF8"),
                ".html" or ".htm" => SniffHtml(path),
                ".zip" => header[0] == (byte)'P' && header[1] == (byte)'K',
                _ => false,
            };
        }
        catch
        {
            return false;
        }
    }

    private static bool SniffHtml(string path)
    {
        try
        {
            using var sr = new StreamReader(path);
            char[] buf = new char[512];
            int n = sr.Read(buf, 0, buf.Length);
            var text = new string(buf, 0, n).ToLowerInvariant();
            return text.Contains("<html") || text.Contains("<!doctype html");
        }
        catch
        {
            return false;
        }
    }

    private static bool ContainsAscii(ReadOnlySpan<byte> span, string ascii)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(ascii);
        for (int i = 0; i + bytes.Length <= span.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < bytes.Length; j++)
            {
                if (span[i + j] != bytes[j]) { match = false; break; }
            }
            if (match)
                return true;
        }
        return false;
    }

    /// <summary>Zip-slip guard: entry must resolve inside libraryRoot (SECURITY.md §2).</summary>
    public static bool IsZipEntrySafe(string libraryRoot, string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
            return false;
        if (Path.IsPathRooted(entryName))
            return false;
        var combined = Path.GetFullPath(Path.Combine(libraryRoot, entryName));
        var root = Path.GetFullPath(libraryRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return combined.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    public static string? ValidateImport(string path, long fileBytes, long maxBytes)
    {
        if (!File.Exists(path))
            return "File not found.";
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!IsExtensionAllowed(path))
            return $"Extension '{ext}' is not allowed.";
        if (fileBytes > maxBytes)
            return $"File exceeds the {maxBytes / (1024 * 1024)} MB import limit.";
        if (!HasValidMagicBytes(path, ext))
            return "File content does not match its extension (magic-bytes check failed).";
        return null; // valid
    }

    /// <summary>CSP to inject into HTML wallpapers missing one (SECURITY.md §3).</summary>
    public const string RequiredCsp =
        "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'self' 'unsafe-inline' data: blob:; script-src 'self' 'unsafe-inline'; connect-src 'none';\">";

    public static bool HtmlNeedsCsp(string html)
        => !html.Contains("Content-Security-Policy", StringComparison.OrdinalIgnoreCase);

    /// <summary>NavigationStarting allowlist: only appassets host or explicitly opted-in remote.</summary>
    public static bool IsNavigationAllowed(Uri uri, bool allowRemote, string? remoteHostAllowlist)
    {
        if (uri.Scheme == "https" && uri.Host.Equals("appassets", StringComparison.OrdinalIgnoreCase))
            return true;
        if (uri.Scheme == "file")
            return false; // never file:// (SECURITY.md §3)
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return false; // blocked by default
        if (allowRemote && remoteHostAllowlist is not null &&
            uri.Host.Equals(remoteHostAllowlist, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }
}
