using Backdrop.App.ViewModels;
using Backdrop.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;

namespace Backdrop.App.Views;

public sealed partial class SettingsPage : Page
{
    private SettingsViewModel Vm => App.Host.Services.GetRequiredService<SettingsViewModel>();

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            TStartup.IsOn = Vm.RunAtStartup;
            TMute.IsOn = Vm.S.Mute;
            Volume.Value = Vm.S.Volume;
            TLoop.IsOn = Vm.S.Loop;
            FillBox.SelectedIndex = Vm.S.Fill == FillMode.Fill ? 0 : Vm.S.Fill == FillMode.Fit ? 1 : 2;
        };
    }

    private void Startup_Toggled(object sender, RoutedEventArgs e) => Vm.SetStartup(TStartup.IsOn);

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        Vm.S.Mute = TMute.IsOn;
        Vm.S.Volume = Volume.Value;
        Vm.S.Loop = TLoop.IsOn;
        Vm.S.Fill = FillBox.SelectedIndex == 0 ? FillMode.Fill : FillBox.SelectedIndex == 1 ? FillMode.Fit : FillMode.Stretch;
        await Vm.SaveAsync();
        Info.IsOpen = true;
    }

    private void Logs_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(BackdropPaths.LogsDir) { UseShellExecute = true }); } catch { }
    }

    private void Cache_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            foreach (var f in System.IO.Directory.GetFiles(BackdropPaths.ThumbnailCacheDir))
                System.IO.File.Delete(f);
            Info.Message = "Thumbnail cache cleared.";
            Info.IsOpen = true;
        }
        catch { }
    }
}
