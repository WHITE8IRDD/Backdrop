using Microsoft.UI.Xaml.Controls;

namespace Backdrop.App.Views;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        Version.Text = $"Version {v}";
    }
}
