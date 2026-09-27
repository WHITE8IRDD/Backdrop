// Backdrop.Infrastructure — thumbnail service (ARCHITECTURE.md §3).
// V1: copies a lightweight placeholder scheme — real offscreen MF seek-to-15%
// rendering happens in WallpaperHost (has MediaPlayer); this path guarantees
// Library grid never breaks headless and is fully testable.
using Backdrop.Core;

namespace Backdrop.Infrastructure.Thumbnails;

public sealed class ThumbnailService : IThumbnailService
{
    public Task<Result<string>> GenerateAsync(WallpaperRecord wall, CancellationToken ct = default)
    {
        try
        {
            BackdropPaths.EnsureCreated();
            var dest = Path.Combine(BackdropPaths.ThumbnailCacheDir, wall.Sha256 + ".jpg");
            if (File.Exists(dest))
                return Task.FromResult(Result<string>.Ok(dest));
            // Minimal valid JPEG (1x1) placeholder so grid always has an image.
            // Host replaces it with a real frame capture when MF is available.
            File.WriteAllBytes(dest, PlaceholderJpeg);
            return Task.FromResult(Result<string>.Ok(dest));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string>.Fail(ex.Message));
        }
    }

    // 1x1 white JPEG.
    private static readonly byte[] PlaceholderJpeg = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////wgALCAABAAEBAREA/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/9oACAEBAAA/AP/EABQAAEAAAAAAAAAAAAAAAAAAAAD/2gAIAQMBAT8AH//EABQAAEAAAAAAAAAAAAAAAAAAAAD/2gAIAQIBAT8AH//EABQQAQAAAAAAAAAAAAAAAAAAABD/2gAIAQEAAT8AH//Z");
}
