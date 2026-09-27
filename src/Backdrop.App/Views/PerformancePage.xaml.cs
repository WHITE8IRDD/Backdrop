using Backdrop.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Backdrop.App.Views;

public sealed partial class PerformancePage : Page
{
    private PerformanceViewModel Vm => App.Host.Services.GetRequiredService<PerformanceViewModel>();

    public PerformancePage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            TFullscreen.IsOn = Vm.Rules.PauseOnFullscreen;
            TMaximized.IsOn = Vm.Rules.PauseOnMaximized;
            TBattery.IsOn = Vm.Rules.PauseOnBatterySaver;
            TRdp.IsOn = Vm.Rules.PauseOnRemoteDesktop;
            TLock.IsOn = Vm.Rules.PauseOnLockScreen;
            TDisplay.IsOn = Vm.Rules.PauseOnDisplayOff;
            FpsBox.SelectedIndex = Vm.FpsLimit == 30 ? 0 : Vm.FpsLimit == 60 ? 1 : 2;
        };
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        Vm.Rules.PauseOnFullscreen = TFullscreen.IsOn;
        Vm.Rules.PauseOnMaximized = TMaximized.IsOn;
        Vm.Rules.PauseOnBatterySaver = TBattery.IsOn;
        Vm.Rules.PauseOnRemoteDesktop = TRdp.IsOn;
        Vm.Rules.PauseOnLockScreen = TLock.IsOn;
        Vm.Rules.PauseOnDisplayOff = TDisplay.IsOn;
        Vm.FpsLimit = FpsBox.SelectedIndex == 0 ? 30 : FpsBox.SelectedIndex == 1 ? 60 : 0;
        await Vm.SaveAsync();
        Info.IsOpen = true;
    }
}
