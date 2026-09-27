// Backdrop.Tests — security gate (SECURITY.md, TESTING.md §2 Import/Security cases).
using Backdrop.Core.Security;
using FluentAssertions;

namespace Backdrop.Tests;

public class SecurityTests
{
    [Theory]
    [InlineData("a.mp4", true)]
    [InlineData("a.webm", true)]
    [InlineData("a.mov", true)]
    [InlineData("a.avi", true)]
    [InlineData("a.mkv", true)]
    [InlineData("a.m4v", true)]
    [InlineData("a.gif", true)]
    [InlineData("a.html", true)]
    [InlineData("a.htm", true)]
    [InlineData("a.zip", true)]
    [InlineData("a.exe", false)]
    [InlineData("a.scr", false)]
    [InlineData("a.bat", false)]
    public void ExtensionAllowlist(string name, bool allowed)
    {
        WallpaperValidator.IsExtensionAllowed(name).Should().Be(allowed);
    }

    [Fact]
    public void ZipSlip_Blocked()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        WallpaperValidator.IsZipEntrySafe(root, "../../evil.exe").Should().BeFalse();
        WallpaperValidator.IsZipEntrySafe(root, "C:\\Windows\\evil.exe").Should().BeFalse();
        WallpaperValidator.IsZipEntrySafe(root, "/absolute/evil.exe").Should().BeFalse();
        WallpaperValidator.IsZipEntrySafe(root, "wall/index.html").Should().BeTrue();
    }

    [Fact]
    public void Oversize_Rejected()
    {
        // Allowlisted extension so validation reaches the size check.
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp4");
        File.WriteAllBytes(tmp, System.Text.Encoding.ASCII.GetBytes("ftypmp42"));
        try
        {
            WallpaperValidator.ValidateImport(tmp, 600L * 1024 * 1024, 500L * 1024 * 1024)
                .Should().Contain("exceeds");
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public void CorruptFile_DoesNotThrow_ReturnsError()
    {
        // 10-file corrupt suite pattern (TESTING.md §3): random bytes must fail magic check, never crash.
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp4");
        try
        {
            File.WriteAllBytes(tmp, new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 });
            WallpaperValidator.HasValidMagicBytes(tmp, ".mp4").Should().BeFalse();
            WallpaperValidator.ValidateImport(tmp, 5, 500L * 1024 * 1024).Should().NotBeNullOrEmpty();
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public void GifMagic_Accepted()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gif");
        try
        {
            File.WriteAllBytes(tmp, System.Text.Encoding.ASCII.GetBytes("GIF89a...."));
            WallpaperValidator.HasValidMagicBytes(tmp, ".gif").Should().BeTrue();
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public void EvilFetch_Blocked_ByCsp_And_Navigation()
    {
        var html = "<html><head></head><body><script>fetch('https://evil.com')</script></body></html>";
        WallpaperValidator.HtmlNeedsCsp(html).Should().BeTrue();
        WallpaperValidator.IsNavigationAllowed(new Uri("https://evil.com/x"), false, null).Should().BeFalse();
        WallpaperValidator.IsNavigationAllowed(new Uri("file:///C:/secret"), true, "evil.com").Should().BeFalse();
        WallpaperValidator.IsNavigationAllowed(new Uri("http://localhost:8080/"), true, "localhost").Should().BeFalse();
        WallpaperValidator.IsNavigationAllowed(new Uri("https://appassets/index.html"), false, null).Should().BeTrue();
    }
}
