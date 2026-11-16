using ComicReader.ViewModels;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ComicReader.Views;

/// <summary>
/// Main application window with seamless dark custom WindowChrome, suppressed icon and title,
/// and native window controls.
/// </summary>
public partial class MainWindow : Window
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int WM_SETICON = 0x0080;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        StateChanged += MainWindow_StateChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            // 1. Enable immersive dark mode caption in DWM (Windows 10/11)
            int trueValue = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref trueValue, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref trueValue, sizeof(int));

            // Set DWM caption color to #141414 (BGR: 0x00141414)
            int darkColor = 0x00141414;
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref darkColor, sizeof(int));

            // 2. Explicitly remove any icon from the window frame while retaining it for taskbar/explorer
            SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_SMALL), IntPtr.Zero);
            SendMessage(hwnd, WM_SETICON, new IntPtr(ICON_BIG), IntPtr.Zero);
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            MaximizeGlyph.Text = "❐";
            // Offset maximized border compensation for WindowChrome
            Padding = new Thickness(7);
        }
        else
        {
            MaximizeGlyph.Text = "⬜";
            Padding = new Thickness(0);
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
