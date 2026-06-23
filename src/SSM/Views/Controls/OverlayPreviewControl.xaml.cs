using System.IO;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;
using SSM.Core.Helpers;
using SSM.Core.Interfaces;
using SSM.Core.Models;

namespace SSM.Views.Controls;

public partial class OverlayPreviewControl : UserControl
{
    private OverlayCanvasRenderer? _renderer;
    private IHardwareMonitorService? _monitor;

    public OverlayPreviewControl()
    {
        InitializeComponent();
    }

    public void Initialize(IHardwareMonitorService monitor, string sp2Path)
    {
        if (_monitor is not null)
            _monitor.DataUpdated -= OnDataUpdated;
        _renderer?.Dispose();

        _monitor = monitor;
        _renderer = new OverlayCanvasRenderer(PreviewCanvas, monitor);
        _renderer.BuildFromTemplate(sp2Path);

        _monitor.DataUpdated += OnDataUpdated;

        var current = monitor.CurrentData;
        if (current.Cpu.Temperature > 0 || current.Cpu.Load > 0)
            _renderer.OnDataUpdated(current);
    }

    public void ReloadTemplate(string sp2Path)
    {
        if (_monitor is null) return;
        _renderer?.Dispose();
        _renderer = new OverlayCanvasRenderer(PreviewCanvas, _monitor);
        _renderer.BuildFromTemplate(sp2Path);
    }

    private void OnDataUpdated(object? sender, HardwareData data)
        => Dispatcher.Invoke(() => _renderer?.OnDataUpdated(data));

    public void Cleanup()
    {
        if (_monitor is not null)
            _monitor.DataUpdated -= OnDataUpdated;
        _renderer?.Dispose();
        _renderer = null;
    }
}
