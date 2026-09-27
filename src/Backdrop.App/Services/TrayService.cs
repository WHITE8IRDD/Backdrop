// Backdrop.App — tray (H.NotifyIcon.WinUI). Right-click pause toggles instantly (UI spec §3).
using Backdrop.Core;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Backdrop.App.Services;

public sealed class TrayService : IDisposable
{
    private TaskbarIcon? _icon;
    private readonly IWallpaperEngine _engine;
    private readonly IPerformanceManager _perf;
    private bool _paused;

    public TrayService(IWallpaperEngine engine, IPerformanceManager perf)
    {
        _engine = engine;
        _perf = perf;
    }

    public void Show()
    {
        _icon ??= new TaskbarIcon
        {
            ToolTipText = "Backdrop",
            ContextFlyout = BuildMenu(),
        };
        _icon.ForceCreate();
    }

    private MenuFlyout BuildMenu()
    {
        var menu = new MenuFlyout();
        var pause = new MenuFlyoutItem { Text = "Pause" };
        pause.Click += async (_, _) =>
        {
            _paused = !_paused;
            pause.Text = _paused ? "Resume" : "Pause";
            if (_paused)
                await _engine.PauseAllAsync(PauseReason.User);
            else
                await _engine.ResumeAllAsync();
            if (_icon is not null)
                _icon.ToolTipText = _paused ? "Backdrop (paused)" : "Backdrop";
        };
        var show = new MenuFlyoutItem { Text = "Open Backdrop" };
        show.Click += (_, _) => App.MainWindow?.Activate();
        var quit = new MenuFlyoutItem { Text = "Quit" };
        quit.Click += (_, _) => Application.Current.Exit();
        menu.Items.Add(pause);
        menu.Items.Add(show);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(quit);
        return menu;
    }

    public void SetPaused(bool paused) => _paused = paused;
    public void Hide() => _icon?.Dispose();
    public void Dispose() => _icon?.Dispose();
}
