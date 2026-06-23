using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using SSM.Core.Helpers;
using SSM.Core.Interfaces;
using SSM.Core.Models;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using Rectangle = System.Windows.Shapes.Rectangle;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;

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

    private readonly Dictionary<string, Action<HardwareData>> _updaters = new();
    private readonly Queue<float> _cpuHistory = new();
    private readonly Queue<float> _gpuHistory = new();
    private const int HistoryLen = 60;

    // graph canvases keyed by sensor id
    private readonly Dictionary<string, Canvas> _graphCanvases = new();
    private readonly Dictionary<string, WpfColor> _graphColors = new();

    private DispatcherTimer? _clockTimer;
    private TextBlock? _timeText;
    private TextBlock? _uptimeText;

    private static readonly string TemplateDir = System.IO.Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Themes", "monitor", "template");

    public OverlayWindow(int screenIndex, int angle, IHardwareMonitorService monitor, ISettingsService settingsService)
    {
        InitializeComponent();
        _monitor = monitor;
        _settingsService = settingsService;

        SourceInitialized += (_, _) => PositionOnScreen(screenIndex);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

        if (angle != 0)
            RotatableContent.LayoutTransform = new RotateTransform(angle);

        BuildOverlayFromTemplate(System.IO.Path.Combine(TemplateDir, "2026-06-23.sp2"));

        _monitor.DataUpdated += OnDataUpdated;

        var current = _monitor.CurrentData;
        if (current.Cpu.Temperature > 0 || current.Cpu.Load > 0)
            UpdateOverlay(current);
    }

    protected override void OnClosed(EventArgs e)
    {
        _clockTimer?.Stop();
        _monitor.DataUpdated -= OnDataUpdated;
        base.OnClosed(e);
    }

    private void OnDataUpdated(object? sender, HardwareData data)
        => Dispatcher.Invoke(() => UpdateOverlay(data));

    private void UpdateOverlay(HardwareData data)
    {
        foreach (var u in _updaters.Values)
            u(data);

        EnqueueHistory(_cpuHistory, data.Cpu.Load);
        EnqueueHistory(_gpuHistory, data.Gpu.Load);

        foreach (var (sensorId, gCanvas) in _graphCanvases)
        {
            var history = sensorId == "SCPUUTI" ? _cpuHistory : _gpuHistory;
            var color   = _graphColors.GetValueOrDefault(sensorId, Colors.White);
            RedrawGraph(gCanvas, history, color);
        }
    }

    // ──────────────────────────────────────────────────────────
    //  Template builder
    // ──────────────────────────────────────────────────────────

    private void BuildOverlayFromTemplate(string sp2Path)
    {
        if (!File.Exists(sp2Path)) return;

        Sp2Panel panel;
        try { panel = Sp2Parser.Parse(sp2Path); }
        catch { return; }

        OverlayCanvas.Width  = panel.Width;
        OverlayCanvas.Height = panel.Height;

        // Fallback background color rectangle (sits behind everything)
        var bgRect = new Rectangle
        {
            Width  = panel.Width,
            Height = panel.Height,
            Fill   = new SolidColorBrush(panel.Background)
        };
        Canvas.SetLeft(bgRect, 0);
        Canvas.SetTop(bgRect, 0);
        OverlayCanvas.Children.Add(bgRect);

        foreach (var el in panel.Elements)
        {
            switch (el.ElementKind)
            {
                case "IMG":
                    BuildImage(el);
                    break;
                case "LBL":
                    BuildLabel(el);
                    break;
                case "SIMPLE":
                    BuildSimple(el);
                    break;
                case "GAUGE":
                    BuildGauge(el);
                    break;
                case "GRAPH":
                    BuildGraph(el);
                    break;
                case "BAR":
                    BuildBarSensor(el);
                    break;
            }
        }

        // Clock timer — ticks every second for time / uptime labels
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => TickClock();
        _clockTimer.Start();
        TickClock();
    }

    // ── Image ─────────────────────────────────────────────────

    private void BuildImage(Sp2Element el)
    {
        var path = System.IO.Path.Combine(TemplateDir, el.ImageFile);
        if (!File.Exists(path)) return;

        var img = new System.Windows.Controls.Image
        {
            Source  = LoadBitmap(path),
            Width   = el.Width  > 0 ? el.Width  : double.NaN,
            Height  = el.Height > 0 ? el.Height : double.NaN,
            Stretch = el.IsBackground ? Stretch.Fill : Stretch.Uniform
        };

        if (el.IsBackground)
        {
            img.Width  = OverlayCanvas.Width;
            img.Height = OverlayCanvas.Height;
        }

        PlaceOnCanvas(img, el.X, el.Y);
    }

    // ── Static label ──────────────────────────────────────────

    private void BuildLabel(Sp2Element el)
    {
        string text = el.Label;
        // Expand AIDA64 system variable placeholders
        if (text == "$CPUMODEL")  text = _monitor.CpuName.Length > 0 ? _monitor.CpuName : "CPU";
        if (text == "$GPU1MODEL") text = _monitor.GpuName.Length > 0 ? _monitor.GpuName : "GPU";

        var tb = MakeTextBlock(text, el.FontSize, el.LabelColor != Colors.Transparent ? el.LabelColor : el.TextColor, el.FontName);
        PlaceOnCanvas(tb, el.X, el.Y);
    }

    // ── [SIMPLE] sensor text ──────────────────────────────────

    private void BuildSimple(Sp2Element el)
    {
        var tb = MakeTextBlock("--", el.FontSize, el.TextColor, el.FontName);
        PlaceOnCanvas(tb, el.X, el.Y);

        switch (el.SensorId)
        {
            case "STIME":
                _timeText = tb;
                break;

            case "SUPTIMENS":
                _uptimeText = tb;
                break;

            default:
                var unitSuffix = el.ShowUnit && el.Unit.Length > 0 ? " " + el.Unit.Trim() : "";
                _updaters[el.RawId] = data =>
                {
                    float v = GetSensorValue(el.SensorId, data);
                    tb.Text = float.IsNaN(v) ? "--" : $"{v:F0}{unitSuffix}";
                };
                break;
        }

        // Composite display: label + value if ShowLabel requested
        if (el.ShowLabel && el.Label.Length > 0)
        {
            var lbl = MakeTextBlock(el.Label, Math.Max(8, el.FontSize - 4),
                el.LabelColor != Colors.Transparent ? el.LabelColor : el.TextColor, el.FontName);
            // Place label slightly above
            PlaceOnCanvas(lbl, el.X, el.Y - el.FontSize - 2);
        }
    }

    // ── [GAUGE] animated frame image ─────────────────────────

    private void BuildGauge(Sp2Element el)
    {
        if (el.GaugeFrames.Count == 0) return;

        var frames = el.GaugeFrames
            .Select(f => System.IO.Path.Combine(TemplateDir, f))
            .Select(p => File.Exists(p) ? LoadBitmap(p) : null)
            .ToArray();

        if (frames.All(f => f is null)) return;

        var img = new System.Windows.Controls.Image
        {
            Width   = el.Width  > 0 ? el.Width  : double.NaN,
            Height  = el.Height > 0 ? el.Height : double.NaN,
            Source  = frames[0],
            Stretch = Stretch.Uniform
        };

        PlaceOnCanvas(img, el.X, el.Y);

        _updaters[el.RawId] = data =>
        {
            float v = GetSensorValue(el.SensorId, data);
            if (float.IsNaN(v)) return;
            int idx = (int)Math.Round((v - el.MinVal) / (el.MaxVal - el.MinVal) * (frames.Length - 1));
            idx = Math.Clamp(idx, 0, frames.Length - 1);
            if (frames[idx] is not null)
                img.Source = frames[idx];
        };
    }

    // ── [GRAPH] history polyline ──────────────────────────────

    private void BuildGraph(Sp2Element el)
    {
        double w = el.Width  > 0 ? el.Width  : 250;
        double h = el.Height > 0 ? el.Height : 100;

        // Background
        var bgRect = new Rectangle
        {
            Width  = w,
            Height = h,
            Fill   = el.BgColor != Colors.Transparent
                ? new SolidColorBrush(el.BgColor)
                : Brushes.Transparent
        };
        PlaceOnCanvas(bgRect, el.X, el.Y);

        // Drawing canvas
        var graphCanvas = new Canvas { Width = w, Height = h, ClipToBounds = true };
        PlaceOnCanvas(graphCanvas, el.X, el.Y);

        _graphCanvases[el.SensorId] = graphCanvas;
        _graphColors[el.SensorId] = el.GraphColor != Colors.Transparent ? el.GraphColor : Colors.Cyan;
    }

    // ── BAR / plain sensor text ───────────────────────────────

    private void BuildBarSensor(Sp2Element el)
    {
        double x = el.X;
        double y = el.Y;

        if (el.ShowLabel && el.Label.Length > 0)
        {
            var lbl = MakeTextBlock(el.Label + "  ",
                el.FontSize,
                el.LabelColor != Colors.Transparent ? el.LabelColor : el.TextColor,
                el.FontName);
            PlaceOnCanvas(lbl, x, y);
            // value goes after label horizontally (approximate offset)
            x += el.Label.Length * el.FontSize * 0.55 + 4;
        }

        var valTb = MakeTextBlock("--", el.FontSize, el.ValueColor != Colors.Transparent ? el.ValueColor : Colors.White, el.FontName);
        PlaceOnCanvas(valTb, x, y);

        if (el.ShowUnit && el.Unit.Trim().Length > 0)
        {
            var unitTb = MakeTextBlock(" " + el.Unit.Trim(), Math.Max(8, el.FontSize - 2), el.TextColor, el.FontName);
            PlaceOnCanvas(unitTb, x + (el.Width > 0 ? el.Width : 60), y);
        }

        var unitSuffix = el.ShowUnit && el.Unit.Trim().Length > 0 ? " " + el.Unit.Trim() : "";
        _updaters[el.RawId] = data =>
        {
            float v = GetSensorValue(el.SensorId, data);
            valTb.Text = float.IsNaN(v) ? "--" : $"{v:F0}";
        };
    }

    // ──────────────────────────────────────────────────────────
    //  Graph redraw
    // ──────────────────────────────────────────────────────────

    private static void RedrawGraph(Canvas gCanvas, Queue<float> history, WpfColor lineColor)
    {
        gCanvas.Children.Clear();
        if (history.Count < 2) return;

        double w = gCanvas.Width;
        double h = gCanvas.Height;
        var pts = history.ToArray();
        int n = pts.Length;
        double step = w / (HistoryLen - 1);

        var points = new PointCollection(n);
        for (int i = 0; i < n; i++)
        {
            double px = (HistoryLen - n + i) * step;
            double py = h - (pts[i] / 100.0) * h;
            points.Add(new System.Windows.Point(px, py));
        }

        gCanvas.Children.Add(new Polyline
        {
            Points          = points,
            Stroke          = new SolidColorBrush(lineColor),
            StrokeThickness = 2,
            StrokeLineJoin  = PenLineJoin.Round
        });
    }

    private static void EnqueueHistory(Queue<float> q, float value)
    {
        q.Enqueue(value);
        while (q.Count > HistoryLen) q.Dequeue();
    }

    // ──────────────────────────────────────────────────────────
    //  Clock tick
    // ──────────────────────────────────────────────────────────

    private void TickClock()
    {
        if (_timeText is not null)
            _timeText.Text = DateTime.Now.ToString("HH:mm:ss");

        if (_uptimeText is not null)
        {
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            _uptimeText.Text = $"{(int)uptime.TotalDays:D2}.{uptime:hh\\:mm\\:ss}";
        }
    }

    // ──────────────────────────────────────────────────────────
    //  Sensor value routing
    // ──────────────────────────────────────────────────────────

    private static float GetSensorValue(string sensorId, HardwareData data) => sensorId switch
    {
        "TCPUDIO"   => data.Cpu.Temperature,
        "TGPU1"     => data.Gpu.Temperature,
        "SCPUUTI"   => data.Cpu.Load,
        "SGPU1UTI"  => data.Gpu.Load,
        "FCPU"      => data.Cpu.FanSpeed,
        "FCHA1"     => data.Gpu.FanSpeed,
        "SCPUCLK"   => data.Cpu.Clock,
        "SGPU1CLK"  => data.Gpu.Clock,
        _           => float.NaN
    };

    // ──────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────

    private static System.Windows.Controls.TextBlock MakeTextBlock(
        string text, int fontSize, WpfColor color, string fontFamily)
        => new()
        {
            Text       = text,
            FontSize   = fontSize,
            FontFamily = new FontFamily(fontFamily),
            Foreground = new SolidColorBrush(color)
        };

    private void PlaceOnCanvas(UIElement el, double x, double y, Canvas? target = null)
    {
        var c = target ?? OverlayCanvas;
        Canvas.SetLeft(el, x);
        Canvas.SetTop(el, y);
        c.Children.Add(el);
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource        = new Uri(path, UriKind.Absolute);
        bmp.CacheOption      = BitmapCacheOption.OnLoad;
        bmp.CreateOptions    = BitmapCreateOptions.None;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
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
