// Backdrop.App — Library page code-behind: drag-drop overlay, file picker, apply/remove.
using Backdrop.App.ViewModels;
using Backdrop.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.InteropServices;
using Windows.Storage.Pickers;

namespace Backdrop.App.Views;

/// <summary>Win32 fallback file dialog (comdlg32). The WinRT picker throws E_FAIL
/// unpackaged on some machines even with correct HWND init; GetOpenFileNameW
/// always works. No extra package/dependency needed (pure P/Invoke).</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct OpenFileNameW
{
    public int lStructSize;
    public nint hwndOwner;
    public nint hInstance;
    [MarshalAs(UnmanagedType.LPWStr)] public string lpstrFilter;
    [MarshalAs(UnmanagedType.LPWStr)] public string? lpstrCustomFilter;
    public int nMaxCustFilter;
    public int nFilterIndex;
    [MarshalAs(UnmanagedType.LPWStr)] public string lpstrFile;
    public int nMaxFile;
    [MarshalAs(UnmanagedType.LPWStr)] public string? lpstrFileTitle;
    public int nMaxFileTitle;
    [MarshalAs(UnmanagedType.LPWStr)] public string? lpstrInitialDir;
    [MarshalAs(UnmanagedType.LPWStr)] public string? lpstrTitle;
    public int Flags;
    public short nFileOffset;
    public short nFileExtension;
    [MarshalAs(UnmanagedType.LPWStr)] public string? lpstrDefExt;
    public nint lCustData;
    public nint lpfnHook;
    [MarshalAs(UnmanagedType.LPWStr)] public string? lpTemplateName;
}

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

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern bool GetOpenFileNameW(ref OpenFileNameW ofn);

    private static nint ResolveHwnd(Page page)
    {
        nint hwnd = 0;
        try
        {
            var window = App.MainWindow;
            if (window is not null)
                hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        }
        catch { /* fall through to fallback */ }
        if (hwnd == 0 && page.XamlRoot is not null)
        {
            try { hwnd = WinRT.Interop.WindowNative.GetWindowHandle(page); }
            catch { /* keep 0, caller reports */ }
        }
        return hwnd;
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        InfoBar.IsOpen = false;
        string? pickedPath = null;
        try
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.VideosLibrary,
                FileTypeFilter = { ".mp4", ".webm", ".mov", ".avi", ".mkv", ".m4v", ".gif", ".html", ".htm", ".zip" },
            };
            nint hwnd = ResolveHwnd(this);
            App.Trace($"Picker try HWND=0x{hwnd:X}");
            if (hwnd != 0)
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            var file = await picker.PickSingleFileAsync();
            if (file is not null)
                pickedPath = file.Path;
        }
        catch (Exception ex)
        {
            App.Trace($"WinRT picker failed 0x{ex.HResult:X8} [{ex.GetType().Name}], falling back to Win32");
        }

        if (pickedPath is null)
        {
            // FALLBACK: Win32 common dialog works unpackaged without any HWND init.
            try
            {
                nint hwnd = ResolveHwnd(this);
                var ofn = new OpenFileNameW
                {
                    hwndOwner = hwnd,
                    lpstrFilter = "Wallpapers\0*.mp4;*.webm;*.avi;*.mov;*.mkv;*.gif;*.html;*.htm\0Videos\0*.mp4;*.webm;*.avi;*.mov;*.mkv\0All files\0*.*\0\0",
                    lpstrFile = new string('\0', 1024),
                    nMaxFile = 1024,
                    lpstrTitle = "Import wallpaper",
                    Flags = 0x00080000 | 0x00001000 | 0x00000008, // OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_NOCHANGEDIR
                };
                ofn.lStructSize = Marshal.SizeOf(ofn);
                if (GetOpenFileNameW(ref ofn))
                {
                    // Buffer is double-null-terminated; take the first path.
                    int end = ofn.lpstrFile.IndexOf('\0');
                    pickedPath = end >= 0 ? ofn.lpstrFile[..end] : ofn.lpstrFile;
                    if (string.IsNullOrWhiteSpace(pickedPath))
                        pickedPath = null;
                }
            }
            catch (Exception ex2)
            {
                App.Trace($"Win32 fallback failed 0x{ex2.HResult:X8} [{ex2.GetType().Name}]: {ex2.Message}");
            }
        }

        if (pickedPath is not null)
        {
            try
            {
                await Vm.ImportAsync(pickedPath);
                Bind();
            }
            catch (Exception ex)
            {
                InfoBar.Message = $"Import failed 0x{ex.HResult:X8}: {ex.Message}";
                InfoBar.Severity = InfoBarSeverity.Error;
                InfoBar.IsOpen = true;
            }
        }
        else
        {
            App.Trace("Pick cancelled (both pickers returned nothing)");
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
            var msg = $"Drop failed 0x{ex.HResult:X8}: {ex.Message.Trim()} | {ex.GetType().Name}";
            App.Trace("OnDrop: " + msg);
            InfoBar.Message = msg + " - check Logs";
            InfoBar.Severity = InfoBarSeverity.Error;
            InfoBar.IsOpen = true;
        }
        e.Handled = true;
    }
}
