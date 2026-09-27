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
        if (Grid is null || InfoBar is null || EmptyState is null)
            return;
        var items = Vm.FilteredItems().ToList();
        Grid.ItemsSource = items;
        Grid.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = items.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        if (!string.IsNullOrEmpty(Vm.InfoMessage))
        {
            InfoBar.Message = Vm.InfoMessage;
            InfoBar.IsOpen = true;
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
        InfoBar.IsOpen = false;
        try
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.VideosLibrary,
                FileTypeFilter = { ".mp4", ".webm", ".mov", ".avi", ".mkv", ".m4v", ".gif", ".html", ".htm", ".zip" },
            };
            // Robust HWND: MainWindow handle first, XamlRoot fallback for unpackaged WinUI 3.
            nint hwnd = 0;
            var window = App.MainWindow;
            if (window is not null)
                hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            if (hwnd == 0 && XamlRoot is not null)
            {
                try { hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this); }
                catch { /* keep 0, reported below */ }
            }
            App.Trace($"Picker HWND=0x{hwnd:X}, launching FileOpenPicker");
            if (hwnd == 0)
                throw new InvalidOperationException("HWND is 0 - MainWindow not initialized.");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            var file = await picker.PickSingleFileAsync();
            App.Trace($"Picker returned {(file is null ? "null (cancelled)" : file.Path)}");
            if (file is not null)
            {
                await Vm.ImportAsync(file.Path);
                Bind();
            }
        }
        catch (Exception ex)
        {
            // ex.Message is empty for E_FAIL; HResult carries the diagnosis.
            var msg = $"Picker failed 0x{ex.HResult:X8}: {ex.Message.Trim()} [{ex.GetType().Name}]";
            App.Trace("Import_Click: " + msg);
            Vm.InfoMessage = msg + " You can drag & drop files instead.";
            InfoBar.Message = Vm.InfoMessage;
            InfoBar.Severity = InfoBarSeverity.Error;
            InfoBar.IsOpen = true;
            System.Diagnostics.Debug.WriteLine(ex.ToString());
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Import wallpaper";
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.IsGlyphVisible = true;
        DragOverlay.Visibility = Visibility.Visible;
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
        => DragOverlay.Visibility = Visibility.Collapsed;

    private async void OnDrop(object sender, DragEventArgs e)
    {
        DragOverlay.Visibility = Visibility.Collapsed;
        InfoBar.IsOpen = false;
        try
        {
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                foreach (var item in items.OfType<Windows.Storage.StorageFile>())
                {
                    App.Trace($"Drop import {item.Path}");
                    await Vm.ImportAsync(item.Path);
                }
                Bind();
            }
        }
        catch (Exception ex)
        {
            var msg = $"Import failed 0x{ex.HResult:X8}: {ex.Message.Trim()}";
            App.Trace("OnDrop: " + msg);
            InfoBar.Message = msg;
            InfoBar.Severity = InfoBarSeverity.Error;
            InfoBar.IsOpen = true;
        }
        e.Handled = true;
    }
}
