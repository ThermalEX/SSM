using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using SSM.Core.Helpers;
using SSM.Core.Interfaces;
using SSM.Core.Models;

namespace SSM.Views;

public partial class OverlayWindow : Window
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private readonly IHardwareMonitorService _monitor;
    private readonly ISettingsService _settingsService;
    private OverlayCanvasRenderer? _renderer;

    public OverlayWindow(int screenIndex, int angle, IHardwareMonitorService monitor, ISettingsService settingsService)
    {
        InitializeComponent();
        _monitor = monitor;
        _settingsService = settingsService;

        SourceInitialized += (_, _) => PositionOnScreen(screenIndex);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

        if (angle != 0)
            RotatableContent.LayoutTransform = new RotateTransform(angle);

        RotatableContent.Stretch = settingsService.Settings.FitMode switch
        {
            OverlayFitMode.Center  => Stretch.None,
            OverlayFitMode.Stretch => Stretch.Fill,
            _                      => Stretch.Uniform,
        };

        var sp2Path = ResolveTemplatePath();
        _renderer = new OverlayCanvasRenderer(OverlayCanvas, _monitor);
        _renderer.BuildFromTemplate(sp2Path);

        _monitor.DataUpdated += OnDataUpdated;

        _renderer.OnDataUpdated(_monitor.CurrentData);
    }

    protected override void OnClosed(EventArgs e)
    {
        _monitor.DataUpdated -= OnDataUpdated;
        _renderer?.Dispose();
        base.OnClosed(e);
    }

    private void OnDataUpdated(object? sender, HardwareData data)
        => Dispatcher.Invoke(() => _renderer?.OnDataUpdated(data));

    private string ResolveTemplatePath()
    {
        var rel = _settingsService.Settings.ActiveSp2Template;
        if (!string.IsNullOrEmpty(rel))
        {
            var full = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, rel));
            if (File.Exists(full)) return full;
        }
        // fallback to default bundled template
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "Themes", "Monitor", "template", "2026-06-23.sp2");
    }

    private void PositionOnScreen(int index)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var screen  = screens.Length > index ? screens[index] : screens[0];
        var b       = screen.Bounds;

        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowPos(hwnd, IntPtr.Zero, b.Left, b.Top, b.Width, b.Height,
            SWP_NOZORDER | SWP_FRAMECHANGED);
    }
}
