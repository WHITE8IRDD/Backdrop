// Backdrop.Infrastructure — thumbnail service (ARCHITECTURE.md §3).
// Real frame capture via the shell thumbnail provider (StorageFile.GetThumbnailAsync):
// no MediaFoundation needed in this process, works unpackaged, fully async.
// Falls back to a 1x1 placeholder so the grid never breaks; import never fails.
using Backdrop.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace Backdrop.Infrastructure.Thumbnails;

public sealed class ThumbnailService : IThumbnailService
{
    private const uint RequestedSize = 400;
    private const long RealThumbMinBytes = 4096; // below this we treat a cached file as placeholder

    public async Task<Result<string>> GenerateAsync(WallpaperRecord wall, CancellationToken ct = default)
    {
        try
        {
            BackdropPaths.EnsureCreated();
            var dest = Path.Combine(BackdropPaths.ThumbnailCacheDir, wall.Sha256 + ".jpg");
            var existing = new FileInfo(dest);
            if (existing.Exists && existing.Length >= RealThumbMinBytes)
                return Result<string>.Ok(dest);

            if (wall.Type is WallpaperType.Video or WallpaperType.Gif)
            {
                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(wall.LibraryPath);
                    var mode = wall.Type == WallpaperType.Gif
                        ? ThumbnailMode.PicturesView
                        : ThumbnailMode.VideosView;
                    using var thumb = await file.GetThumbnailAsync(mode, RequestedSize);
                    if (thumb is not null && thumb.Size > 0)
                    {
                        using var reader = new Windows.Storage.Streams.DataReader(thumb);
                        await reader.LoadAsync((uint)thumb.Size);
                        byte[] buf = new byte[thumb.Size];
                        reader.ReadBytes(buf);
                        await File.WriteAllBytesAsync(dest, buf, ct);
                        if (new FileInfo(dest).Length > 0)
                            return Result<string>.Ok(dest);
                    }
                }
                catch
                {
                    // Fall through to placeholder.
                }
            }

            // Minimal valid JPEG (1x1) placeholder so grid always has an image.
            await File.WriteAllBytesAsync(dest, PlaceholderJpeg, ct);
            return Result<string>.Ok(dest);
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(ex.Message);
        }
    }

    // 1x1 white JPEG.
    private static readonly byte[] PlaceholderJpeg = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/9oACAEBAAA/AP/EABQAAEAAAAAAAAAAAAAAAAAAAAD/2gAIAQMBAT8AH//EABQAAEAAAAAAAAAAAAAAAAAAAAD/2gAIAQIBAT8AH//EABQQAQAAAAAAAAAAAAAAAAAAABD/2gAIAQEAAT8AH//Z");
}
