// Backdrop.App — Displays VM (MULTI_MONITOR.md §4, UI spec §2).
using Backdrop.Core;
using Backdrop.Core.Settings;
using Backdrop.Infrastructure.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Backdrop.App.ViewModels;

public partial class DisplaysViewModel : ObservableObject
{
    private readonly IMonitorService _monitors;
    private readonly IWallpaperCatalog _catalog;
    private readonly IWallpaperEngine _engine;
    private readonly JsonSettingsService<BackdropSettings> _settings;

    [ObservableProperty] private bool spanEnabled;
    [ObservableProperty] private string? infoMessage;

    public List<MonitorInfo> Monitors { get; private set; } = [];
    public List<WallpaperRecord> Wallpapers { get; private set; } = [];

    public DisplaysViewModel(IMonitorService monitors, IWallpaperCatalog catalog,
        IWallpaperEngine engine, JsonSettingsService<BackdropSettings> settings)
    {
        _monitors = monitors;
        _catalog = catalog;
        _engine = engine;
        _settings = settings;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        Monitors = _monitors.GetMonitors().ToList();
        Wallpapers = (await _catalog.ListAsync()).ToList();
        OnPropertyChanged(nameof(Monitors));
        OnPropertyChanged(nameof(Wallpapers));
    }

    [RelayCommand]
    public async Task AssignAsync((string monitor, Guid wallpaper) assignment)
    {
        var wall = Wallpapers.FirstOrDefault(w => w.Id == assignment.wallpaper);
        if (wall is null)
            return;
        Result r = SpanEnabled
            ? await _engine.ApplySpanAsync(wall, Monitors.Select(m => m.DeviceId).ToList())
            : await _engine.ApplyAsync(wall, assignment.monitor, SpawnMode.PerMonitor);
        if (r.IsSuccess)
        {
            if (SpanEnabled)
            {
                _settings.Current.SpawnMode = SpawnMode.Span;
                foreach (var m in Monitors)
                    _settings.Current.MonitorAssignments[m.DeviceId] = wall.Id;
            }
            else
            {
                _settings.Current.SpawnMode = SpawnMode.PerMonitor;
                _settings.Current.MonitorAssignments[assignment.monitor] = wall.Id;
            }
            await _settings.SaveAsync();
        }
        InfoMessage = r.IsSuccess ? "Display updated." : $"Failed: {r.Error}";
    }
}
