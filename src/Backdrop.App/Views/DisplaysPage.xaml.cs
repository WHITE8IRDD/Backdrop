using Backdrop.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Backdrop.App.Views;

public sealed partial class DisplaysPage : Page
{
    private DisplaysViewModel Vm => App.Host.Services.GetRequiredService<DisplaysViewModel>();

    public DisplaysPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await Vm.RefreshAsync();
            MonitorList.ItemsSource = Vm.Monitors;
        };
    }

    private void Span_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Vm.SpanEnabled = SpanToggle.IsOn;
    }
}
