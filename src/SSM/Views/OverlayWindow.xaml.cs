using System.Runtime.InteropServices;
using System.Windows;
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

    public OverlayWindow(int screenIndex, int angle, IHardwareMonitorService monitor, ISettingsService settingsService)
    {
        InitializeComponent();
        _monitor = monitor;
        _settingsService = settingsService;

        SourceInitialized += (_, _) => PositionOnScreen(screenIndex);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

        if (angle != 0)
            RotatableContent.LayoutTransform = new RotateTransform(angle);

        _monitor.DataUpdated += OnDataUpdated;

        var current = _monitor.CurrentData;
        if (current.Cpu.Temperature > 0 || current.Cpu.Load > 0)
            UpdateOverlay(current);
    }

    protected override void OnClosed(EventArgs e)
    {
        _monitor.DataUpdated -= OnDataUpdated;
        base.OnClosed(e);
    }

    private void OnDataUpdated(object? sender, HardwareData data)
    {
        Dispatcher.Invoke(() => UpdateOverlay(data));
    }

    private void UpdateOverlay(HardwareData data)
    {
        var s = _settingsService.Settings;

        OverlayCpuTempText.Text = UnitConverter.FormatTemperature(data.Cpu.Temperature, s.TemperatureUnit);
        OverlayCpuBar.Value = data.Cpu.Load;
        OverlayCpuLoadText.Text = $"负载 {data.Cpu.Load:F0}%";

        OverlayGpuTempText.Text = UnitConverter.FormatTemperature(data.Gpu.Temperature, s.TemperatureUnit);
        OverlayGpuBar.Value = data.Gpu.Load;
        OverlayGpuLoadText.Text = $"负载 {data.Gpu.Load:F0}%";

        OverlayRamText.Text = UnitConverter.FormatMemory(data.Memory.Used, s.MemoryUnit);
        OverlayRamBar.Value = data.Memory.UsagePercent;
        OverlayRamDetailText.Text = $"{UnitConverter.FormatMemory(data.Memory.Used, s.MemoryUnit)} / {UnitConverter.FormatMemory(data.Memory.Total, s.MemoryUnit)}";

        OverlayNetUpText.Text = UnitConverter.FormatNetworkSpeed(data.Network.UploadSpeed, s.NetworkSpeedUnit);
        OverlayNetDownText.Text = UnitConverter.FormatNetworkSpeed(data.Network.DownloadSpeed, s.NetworkSpeedUnit);
    }

    private void PositionOnScreen(int index)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var screen = screens.Length > index ? screens[index] : screens[0];
        var b = screen.Bounds;

        // SetWindowPos accepts physical pixel coordinates directly,
        // bypassing WPF's per-monitor DPI unit conversion entirely.
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowPos(hwnd, IntPtr.Zero, b.Left, b.Top, b.Width, b.Height,
            SWP_NOZORDER | SWP_FRAMECHANGED);
    }
}
