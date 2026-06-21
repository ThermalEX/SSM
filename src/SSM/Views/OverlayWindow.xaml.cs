using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace SSM.Views;

public partial class OverlayWindow : Window
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private const uint SWP_NOZORDER     = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    public OverlayWindow(int screenIndex, int angle = 0)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => PositionOnScreen(screenIndex);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

        if (angle != 0)
            RotatableContent.LayoutTransform = new RotateTransform(angle);
    }

    private void PositionOnScreen(int index)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var screen  = screens.Length > index ? screens[index] : screens[0];
        var b       = screen.Bounds;

        // SetWindowPos accepts physical pixel coordinates directly,
        // bypassing WPF's per-monitor DPI unit conversion entirely.
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowPos(hwnd, IntPtr.Zero, b.Left, b.Top, b.Width, b.Height,
            SWP_NOZORDER | SWP_FRAMECHANGED);
    }
}
