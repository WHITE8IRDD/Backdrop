// Backdrop.Infrastructure — import pipeline (WALLPAPER_ENGINE.md §4, SECURITY.md §2/§5).
using Backdrop.Core;
using Backdrop.Core.Security;
using Backdrop.Core.Settings;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Backdrop.Infrastructure.Import;

public sealed class ImportService : IImportService
{
    private readonly IWallpaperCatalog _catalog;
    private readonly Func<BackdropSettings> _settings;
    private readonly IThumbnailService _thumbs;

    public ImportService(IWallpaperCatalog catalog, Func<BackdropSettings> settings, IThumbnailService thumbs)
    {
        _catalog = catalog;
        _settings = settings;
        _thumbs = thumbs;
    }

    public async Task<Result<WallpaperRecord>> ImportAsync(string sourcePathOrUrl, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(sourcePathOrUrl))
                return Result<WallpaperRecord>.Fail("File not found.");
            var info = new FileInfo(sourcePathOrUrl);
            var maxBytes = _settings().MaxImportBytes;
            var err = WallpaperValidator.ValidateImport(sourcePathOrUrl, info.Length, maxBytes);
            if (err is not null)
                return Result<WallpaperRecord>.Fail(err);

            string sha = await Sha256OfAsync(sourcePathOrUrl, ct);
            var dup = await _catalog.GetByHashAsync(sha, ct);
            if (dup.IsSuccess && dup.Value is not null)
                return Result<WallpaperRecord>.Fail("Duplicate wallpaper (same file hash already imported).");

            var ext = Path.GetExtension(sourcePathOrUrl).ToLowerInvariant();
            var id = Guid.NewGuid();
            var libDir = BackdropPaths.LibraryDirFor(id);
            Directory.CreateDirectory(libDir);

            string libraryPath;
            WallpaperType type;
            if (ext == ".zip")
            {
                var zipRes = ExtractZipSafe(sourcePathOrUrl, libDir);
                if (!zipRes.IsSuccess)
                    return Result<WallpaperRecord>.Fail(zipRes.Error);
                libraryPath = zipRes.Value!;
                type = WallpaperType.HtmlLocal;
            }
            else
            {
                libraryPath = Path.Combine(libDir, Path.GetFileName(sourcePathOrUrl));
                File.Copy(sourcePathOrUrl, libraryPath);
                type = ext is ".html" or ".htm" ? WallpaperType.HtmlLocal
                    : ext == ".gif" ? WallpaperType.Gif : WallpaperType.Video;
            }

            var probe = Media.MediaProbe.Probe(libraryPath, type);
            var now = DateTime.UtcNow;
            var record = new WallpaperRecord(
                id, Path.GetFileNameWithoutExtension(sourcePathOrUrl), type,
                sourcePathOrUrl, libraryPath, string.Empty,
                probe.Width, probe.Height, probe.DurationSec, probe.Fps,
                info.Length, sha, now, now, []);

            // Thumbnail (best-effort: never fail import on thumbnail error).
            try
            {
                var t = await _thumbs.GenerateAsync(record with { LibraryPath = libraryPath }, ct);
                if (t.IsSuccess)
                    record = record with { ThumbnailPath = t.Value! };
            }
            catch { /* best effort */ }

            var added = await _catalog.AddAsync(record, ct);
            return added.IsSuccess
                ? Result<WallpaperRecord>.Ok(record)
                : Result<WallpaperRecord>.Fail(added.Error);
        }
        catch (Exception ex)
        {
            return Result<WallpaperRecord>.Fail(ex.Message);
        }
    }

    internal static Result<string> ExtractZipSafe(string zipPath, string destDir)
    {
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            foreach (var entry in zip.Entries)
            {
                if (!WallpaperValidator.IsZipEntrySafe(destDir, entry.FullName))
                    return Result<string>.Fail($"Unsafe zip entry blocked: {entry.FullName}");
            }
            foreach (var entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith('/'))
                    continue; // directory
                var dest = Path.GetFullPath(Path.Combine(destDir, entry.FullName));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                entry.ExtractToFile(dest, overwrite: true);
            }
            // V1: single .html at root (or index.html) = HtmlLocal (WALLPAPER_ENGINE.md §4).
            var html = Directory.GetFiles(destDir, "*.html", SearchOption.TopDirectoryOnly).FirstOrDefault()
                ?? Directory.GetFiles(destDir, "*.htm", SearchOption.TopDirectoryOnly).FirstOrDefault()
                ?? Path.Combine(destDir, "index.html");
            return Result<string>.Ok(html);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(ex.Message);
        }
    }

    internal static async Task<string> Sha256OfAsync(string path, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        await using var fs = File.OpenRead(path);
        var hash = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
