// Backdrop.Infrastructure — EF Core SQLite catalog (ARCHITECTURE.md §4, TECH_STACK.md).
using Backdrop.Core;
using Microsoft.EntityFrameworkCore;

namespace Backdrop.Infrastructure.Catalog;

public sealed class WallpaperEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Type { get; set; }
    public string OriginalPath { get; set; } = string.Empty;
    public string LibraryPath { get; set; } = string.Empty;
    public string ThumbnailPath { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public double? DurationSec { get; set; }
    public double? Fps { get; set; }
    public long FileSizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
    public string Tags { get; set; } = string.Empty; // ';'-joined

    public WallpaperRecord ToRecord() => new(
        Id, Title, (WallpaperType)Type, OriginalPath, LibraryPath, ThumbnailPath,
        Width, Height, DurationSec, Fps, FileSizeBytes, Sha256, AddedAt, LastUsedAt,
        Tags.Split(';', StringSplitOptions.RemoveEmptyEntries));

    public static WallpaperEntity FromRecord(WallpaperRecord r) => new()
    {
        Id = r.Id,
        Title = r.Title,
        Type = (int)r.Type,
        OriginalPath = r.OriginalPath,
        LibraryPath = r.LibraryPath,
        ThumbnailPath = r.ThumbnailPath,
        Width = r.Width,
        Height = r.Height,
        DurationSec = r.DurationSec,
        Fps = r.Fps,
        FileSizeBytes = r.FileSizeBytes,
        Sha256 = r.Sha256,
        AddedAt = r.AddedAt,
        LastUsedAt = r.LastUsedAt,
        Tags = string.Join(';', r.Tags)
    };
}

public sealed class WallpaperDbContext : DbContext
{
    private readonly string _dbPath;
    public DbSet<WallpaperEntity> Wallpapers => Set<WallpaperEntity>();

    public WallpaperDbContext(string dbPath) => _dbPath = dbPath;

    protected override void OnConfiguring(DbContextOptionsBuilder b)
        => b.UseSqlite($"Data Source={_dbPath}");

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<WallpaperEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Sha256).IsUnique();
            e.HasIndex(x => x.AddedAt);
        });
    }
}
