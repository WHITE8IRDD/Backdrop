// Backdrop.Tests — settings atomicity + WebView2 CSP + monitor key sanity.
using Backdrop.Core;
using Backdrop.Infrastructure.Settings;
using Backdrop.Core.Settings;
using Backdrop.WallpaperHost.Players;
using FluentAssertions;

namespace Backdrop.Tests;

public class SettingsTests
{
    [Fact]
    public async Task Settings_RoundTrip_Atomic()
    {
        var dir = Path.Combine(Path.GetTempPath(), "backdrop-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "settings.json");
        var svc = new JsonSettingsService<BackdropSettings>(path, watch: false);
        svc.Current.Volume = 0.33;
        svc.Current.Mute = false;
        (await svc.SaveAsync()).IsSuccess.Should().BeTrue();
        File.Exists(path).Should().BeTrue();
        File.Exists(path + ".tmp").Should().BeFalse(); // tmp -> move completed
        var svc2 = new JsonSettingsService<BackdropSettings>(path, watch: false);
        await svc2.ReloadAsync();
        svc2.Current.Volume.Should().BeApproximately(0.33, 0.0001);
        svc2.Current.Mute.Should().BeFalse();
        Directory.Delete(dir, true);
    }

    [Fact]
    public void WebView2_Csp_Injected_WhenMissing()
    {
        var html = "<html><head><title>x</title></head><body></body></html>";
        WebView2Player.EnsureCsp(html).Should().Contain("Content-Security-Policy");
        var with = "<html><head><meta http-equiv=\"Content-Security-Policy\" content=\"x\"></head></html>";
        WebView2Player.EnsureCsp(with).Should().Be(with);
    }

    [Fact]
    public void WebView2_Lockdown_Defaults()
    {
        WebView2Player.Lockdown.AreDevToolsEnabled.Should().BeFalse();
        WebView2Player.Lockdown.AreDefaultContextMenusEnabled.Should().BeFalse();
        WebView2Player.Lockdown.IsZoomControlEnabled.Should().BeFalse();
        WebView2Player.Lockdown.AreHostObjectsAllowed.Should().BeFalse();
        WebView2Player.Lockdown.EntryUrl.Should().StartWith("https://appassets/");
    }

    [Fact]
    public void PlayerFactory_Routes_Types()
    {
        PlayerFactory.Create(WallpaperType.Video).Should().BeOfType<MediaFoundationPlayer>();
        PlayerFactory.Create(WallpaperType.Gif).Should().BeOfType<MediaFoundationPlayer>();
        PlayerFactory.Create(WallpaperType.HtmlLocal).Should().BeOfType<WebView2Player>();
        PlayerFactory.Create(WallpaperType.HtmlRemoteUrl).Should().BeOfType<WebView2Player>();
    }
}
