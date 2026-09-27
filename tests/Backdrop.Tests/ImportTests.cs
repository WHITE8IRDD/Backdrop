// Backdrop.Tests — import/catalog/integration (TESTING.md §2).
using Backdrop.Core;
using Backdrop.Infrastructure.Catalog;
using Backdrop.Infrastructure.Import;
using Backdrop.Infrastructure.Thumbnails;
using FluentAssertions;

namespace Backdrop.Tests;

public class ImportTests : IDisposable
{
    private readonly string _tmp;
    private readonly SqliteCatalogService _catalog;

    public ImportTests()
    {
        _tmp = Path.Combine(Path.GetTempPath(), "backdrop-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmp);
        BackdropPaths.EnsureCreated();
        _catalog = new SqliteCatalogService(Path.Combine(_tmp, "wallpapers.db"));
    }

    private string WriteMp4(string name = "clip.mp4")
    {
        // Minimal MP4: ftyp box so magic check passes.
        var p = Path.Combine(_tmp, name);
        var bytes = new List<byte>();
        bytes.AddRange(new byte[] { 0, 0, 0, 0x18 });
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes("ftypmp42"));
        bytes.AddRange(new byte[64]);
        File.WriteAllBytes(p, bytes.ToArray());
        return p;
    }

    [Fact]
    public async Task ImportMp4_CopiesToLibrary_And_Lists()
    {
        var svc = new ImportService(_catalog, () => new Core.Settings.BackdropSettings(), new ThumbnailService());
        var src = WriteMp4();
        var r = await svc.ImportAsync(src);
        r.IsSuccess.Should().BeTrue(r.Error);
        File.Exists(r.Value!.LibraryPath).Should().BeTrue();
        (await _catalog.ListAsync()).Should().Contain(x => x.Sha256 == r.Value.Sha256);
    }

    [Fact]
    public async Task DuplicateHash_Rejected()
    {
        var svc = new ImportService(_catalog, () => new Core.Settings.BackdropSettings(), new ThumbnailService());
        var src = WriteMp4();
        (await svc.ImportAsync(src)).IsSuccess.Should().BeTrue();
        var dup = await svc.ImportAsync(src);
        dup.IsSuccess.Should().BeFalse();
        dup.Error.Should().Contain("Duplicate");
    }

    [Fact]
    public async Task MissingCodec_DoesNotCrash()
    {
        var svc = new ImportService(_catalog, () => new Core.Settings.BackdropSettings(), new ThumbnailService());
        var bad = Path.Combine(_tmp, "bad.mp4");
        File.WriteAllBytes(bad, new byte[] { 1, 2, 3, 4 });
        var r = await svc.ImportAsync(bad);
        r.IsSuccess.Should().BeFalse(); // graceful Result, no exception
    }

    public void Dispose()
    {
        try { Directory.Delete(_tmp, true); } catch { }
    }
}
