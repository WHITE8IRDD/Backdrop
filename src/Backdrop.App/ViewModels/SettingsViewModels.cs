// Backdrop.App — Performance + Settings VMs (PERFORMANCE.md §2/§4, UI spec §2).
using Backdrop.Core.Settings;
using Backdrop.Infrastructure.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Backdrop.App.ViewModels;

public partial class PerformanceViewModel : ObservableObject
{
    private readonly JsonSettingsService<BackdropSettings> _settings;
    public PerformanceViewModel(JsonSettingsService<BackdropSettings> settings) => _settings = settings;

    public PauseRules Rules => _settings.Current.PauseRules;
    public int FpsLimit { get => _settings.Current.FpsLimit; set => _settings.Current.FpsLimit = value; }
    public bool HardwareAcceleration { get => _settings.Current.HardwareAcceleration; set => _settings.Current.HardwareAcceleration = value; }

    [RelayCommand]
    public async Task SaveAsync() => await _settings.SaveAsync();
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly JsonSettingsService<BackdropSettings> _settings;
    private readonly Core.IStartupService _startup;

    public SettingsViewModel(JsonSettingsService<BackdropSettings> settings, Core.IStartupService startup)
    {
        _settings = settings;
        _startup = startup;
    }

    public BackdropSettings S => _settings.Current;
    public bool RunAtStartup => _startup.IsEnabled();

    [RelayCommand]
    public async Task SaveAsync() => await _settings.SaveAsync();

    [RelayCommand]
    public void SetStartup(bool enabled)
    {
        _startup.SetEnabled(enabled);
        S.RunAtStartup = enabled;
        _ = _settings.SaveAsync();
    }
}
