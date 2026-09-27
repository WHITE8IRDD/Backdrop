// Backdrop.App — main window (UI spec §2: 960x640 min, 1120x720 default, Mica, titlebar extends).
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Backdrop.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Window size: default 1120x720, min 960x640 (UI spec §2).
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var id = Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow.GetFromWindowId(id).Resize(new SizeInt32(1120, 720));
        EnforceMinSize(hwnd, 960, 640); // DPI-aware WM_GETMINMAXINFO hook (no WASDK-version-sensitive API)
        // Mica + extended titlebar.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(null);
        TrySetMica();
        Nav.SelectedItem = Nav.MenuItems[0];
        ContentFrame.Navigate(typeof(Views.LibraryPage));
    }

    // Minimum window size via subclassing (works on every Windows App SDK 1.x).
    private static readonly SubclassProc _subclassProc = SubclassWndProc;
    private static (int W, int H)? _minSize;

    private static void EnforceMinSize(nint hwnd, int minW, int minH)
    {
        _minSize = (minW, minH);
        SetWindowSubclass(hwnd, _subclassProc, 0, 0);
    }

    private static nint SubclassWndProc(nint hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData)
    {
        const uint WM_GETMINMAXINFO = 0x0024;
        if (uMsg == WM_GETMINMAXINFO && _minSize is { } min)
        {
            uint dpi = GetDpiForWindow(hWnd);
            float scale = dpi / 96f;
            var info = System.Runtime.InteropServices.Marshal.PtrToStructure<MINMAXINFO>(lParam);
            info.ptMinTrackSize.X = (int)(min.W * scale);
            info.ptMinTrackSize.Y = (int)(min.H * scale);
            System.Runtime.InteropServices.Marshal.StructureToPtr(info, lParam, false);
            return 0;
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private delegate nint SubclassProc(nint hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nuint dwRefData);

    [System.Runtime.InteropServices.DllImport("comctl32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool SetWindowSubclass(nint hWnd, SubclassProc pfnSubclass, nuint uIdSubclass, nuint dwRefData);
    [System.Runtime.InteropServices.DllImport("comctl32.dll")]
    private static extern nint DefSubclassProc(nint hWnd, uint uMsg, nint wParam, nint lParam);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    private void TrySetMica()
    {
        try
        {
            var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            var backdrop = new MicaBackdrop();
            this.SystemBackdrop = backdrop;
        }
        catch { /* fallback: default background */ }
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
        System.Type? page = tag switch
        {
            "library" => typeof(Views.LibraryPage),
            "displays" => typeof(Views.DisplaysPage),
            "performance" => typeof(Views.PerformancePage),
            "settings" => typeof(Views.SettingsPage),
            "about" => typeof(Views.AboutPage),
            _ => null,
        };
        if (page is not null)
            ContentFrame.Navigate(page);
    }
}
