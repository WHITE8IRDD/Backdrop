// Backdrop.App — Library page code-behind: drag-drop overlay, file picker, apply/remove.
using Backdrop.App.ViewModels;
using Backdrop.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace Backdrop.App.Views;

public sealed partial class LibraryPage : Page
{
    private LibraryViewModel Vm => App.Host.Services.GetRequiredService<LibraryViewModel>();

    public LibraryPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => { await Vm.RefreshAsync(); Bind(); };
    }

    private void Bind()
    {
        // SelectionChanged fires during InitializeComponent before fields exist.
        if (Grid is null || Info is null)
            return;
        Grid.ItemsSource = Vm.FilteredItems().ToList();
        if (!string.IsNullOrEmpty(Vm.InfoMessage))
        {
            Info.Message = Vm.InfoMessage;
            Info.IsOpen = true;
        }
    }

    private void Filter_Changed(object s, SelectionChangedEventArgs e)
    {
        Vm.Filter = (LibraryFilter)FilterChips.SelectedIndex;
        Bind();
    }

    private void Search_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            Vm.Search = sender.Text;
            Bind();
        }
    }

    private void Sort_Changed(object s, SelectionChangedEventArgs e)
    {
        Vm.Sort = (LibrarySort)SortBox.SelectedIndex;
        Bind();
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.VideosLibrary,
            FileTypeFilter = { ".mp4", ".webm", ".mov", ".avi", ".mkv", ".m4v", ".gif", ".html", ".htm", ".zip" },
        };
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            await Vm.ImportAsync(file.Path);
            Bind();
        }
    }

    private void Grid_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Drop to import";
    }

    private async void Grid_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            foreach (var item in items)
            {
                await Vm.ImportAsync(item.Path);
            }
            Bind();
        }
    }
}
