// Backdrop.Infrastructure — catalog CRUD + duplicate-hash detection.
using Backdrop.Core;
using Microsoft.EntityFrameworkCore;

namespace Backdrop.Infrastructure.Catalog;

public sealed class SqliteCatalogService : IWallpaperCatalog
{
    private readonly string _dbPath;
    public SqliteCatalogService(string dbPath) => _dbPath = dbPath;

    private WallpaperDbContext Open()
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (dir is not null)
            Directory.CreateDirectory(dir);
        var ctx = new WallpaperDbContext(_dbPath);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    public async Task<Result<WallpaperRecord>> AddAsync(WallpaperRecord record, CancellationToken ct = default)
    {
        try
        {
            using var ctx = Open();
            ctx.Wallpapers.Add(WallpaperEntity.FromRecord(record));
            await ctx.SaveChangesAsync(ct);
            return Result<WallpaperRecord>.Ok(record);
        }
        catch (DbUpdateException)
        {
            return Result<WallpaperRecord>.Fail("Duplicate wallpaper (same file hash already imported).");
        }
        catch (Exception ex)
        {
            return Result<WallpaperRecord>.Fail(ex.Message);
        }
    }

    public async Task<Result<WallpaperRecord?>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            using var ctx = Open();
            var e = await ctx.Wallpapers.FindAsync([id], ct);
            return Result<WallpaperRecord?>.Ok(e?.ToRecord());
        }
        catch (Exception ex)
        {
            return Result<WallpaperRecord?>.Fail(ex.Message);
        }
    }

    public async Task<Result<WallpaperRecord?>> GetByHashAsync(string sha256, CancellationToken ct = default)
    {
        try
        {
            using var ctx = Open();
            var e = await ctx.Wallpapers.FirstOrDefaultAsync(x => x.Sha256 == sha256, ct);
            return Result<WallpaperRecord?>.Ok(e?.ToRecord());
        }
        catch (Exception ex)
        {
            return Result<WallpaperRecord?>.Fail(ex.Message);
        }
    }

    public async Task<IReadOnlyList<WallpaperRecord>> ListAsync(CancellationToken ct = default)
    {
        using var ctx = Open();
        return await ctx.Wallpapers.OrderByDescending(x => x.LastUsedAt)
            .Select(x => x.ToRecord()).ToListAsync(ct);
    }

    public async Task<Result> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            using var ctx = Open();
            var e = await ctx.Wallpapers.FindAsync([id], ct);
            if (e is null)
                return Result.Fail("Not found.");
            ctx.Wallpapers.Remove(e);
            await ctx.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    public async Task<Result> TouchUsedAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            using var ctx = Open();
            var e = await ctx.Wallpapers.FindAsync([id], ct);
            if (e is null)
                return Result.Fail("Not found.");
            e.LastUsedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }
}
