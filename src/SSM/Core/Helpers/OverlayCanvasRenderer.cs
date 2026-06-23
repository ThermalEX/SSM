using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using SSM.Core.Interfaces;
using SSM.Core.Models;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using Image = System.Windows.Controls.Image;
using Path = System.IO.Path;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;

namespace SSM.Core.Helpers;

/// <summary>
/// Parses an sp2 template and renders it into any WPF Canvas.
/// Shared by OverlayWindow (full-screen) and OverlayPreviewControl (in-app preview).
/// </summary>
public sealed class OverlayCanvasRenderer : IDisposable
{
    private readonly Canvas _canvas;
    private readonly IHardwareMonitorService _monitor;

    private readonly Dictionary<string, Action<HardwareData>> _updaters = new();
    private readonly Queue<float> _cpuHistory = new();
    private readonly Queue<float> _gpuHistory = new();
    private readonly Dictionary<string, Canvas> _graphCanvases = new();
    private readonly Dictionary<string, WpfColor> _graphColors = new();

    private const int HistoryLen = 60;

    private DispatcherTimer? _clockTimer;
    private TextBlock? _timeText;
    private TextBlock? _uptimeText;

    private string _templateDir = "";

    public OverlayCanvasRenderer(Canvas canvas, IHardwareMonitorService monitor)
    {
        _canvas = canvas;
        _monitor = monitor;
    }

    public void BuildFromTemplate(string sp2Path)
    {
        if (!File.Exists(sp2Path)) return;

        _templateDir = Path.GetDirectoryName(sp2Path) ?? "";
        _updaters.Clear();
        _graphCanvases.Clear();
        _graphColors.Clear();
        _canvas.Children.Clear();
        _clockTimer?.Stop();

        Sp2Panel panel;
        try { panel = Sp2Parser.Parse(sp2Path); }
        catch { return; }

        _canvas.Width  = panel.Width;
        _canvas.Height = panel.Height;

        var bgRect = new Rectangle
        {
            Width  = panel.Width,
            Height = panel.Height,
            Fill   = new SolidColorBrush(panel.Background)
        };
        Canvas.SetLeft(bgRect, 0);
        Canvas.SetTop(bgRect, 0);
        _canvas.Children.Add(bgRect);

        foreach (var el in panel.Elements)
        {
            switch (el.ElementKind)
            {
                case "IMG":   BuildImage(el);     break;
                case "LBL":   BuildLabel(el);     break;
                case "SIMPLE":BuildSimple(el);    break;
                case "GAUGE": BuildGauge(el);     break;
                case "GRAPH": BuildGraph(el);     break;
                case "BAR":   BuildBarSensor(el); break;
            }
        }

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => TickClock();
        _clockTimer.Start();
        TickClock();
    }

    public void OnDataUpdated(HardwareData data)
    {
        foreach (var u in _updaters.Values)
            u(data);

        EnqueueHistory(_cpuHistory, data.Cpu.Load);
        EnqueueHistory(_gpuHistory, data.Gpu.Load);

        foreach (var (sensorId, gCanvas) in _graphCanvases)
        {
            var history = sensorId == "SCPUUTI" ? _cpuHistory : _gpuHistory;
            var color   = _graphColors.GetValueOrDefault(sensorId, WpfColors.Cyan);
            RedrawGraph(gCanvas, history, color);
        }
    }

    public void Dispose()
    {
        _clockTimer?.Stop();
        _clockTimer = null;
    }

    // ── Image ─────────────────────────────────────────────────

    private void BuildImage(Sp2Element el)
    {
        var path = Path.Combine(_templateDir, el.ImageFile);
        if (!File.Exists(path)) return;

        var img = new Image
        {
            Source  = LoadBitmap(path),
            Stretch = el.IsBackground ? Stretch.Fill : Stretch.Uniform
        };

        if (el.IsBackground)
        {
            img.Width  = _canvas.Width;
            img.Height = _canvas.Height;
        }
        else
        {
            if (el.Width  > 0) img.Width  = el.Width;
            if (el.Height > 0) img.Height = el.Height;
        }

        Place(img, el.X, el.Y);
    }

    // ── Static label ──────────────────────────────────────────

    private void BuildLabel(Sp2Element el)
    {
        string text = el.Label;
        if (text == "$CPUMODEL")  text = _monitor.CpuName.Length > 0 ? _monitor.CpuName : "CPU";
        if (text == "$GPU1MODEL") text = _monitor.GpuName.Length > 0 ? _monitor.GpuName : "GPU";

        var col = el.LabelColor != WpfColors.Transparent ? el.LabelColor : el.TextColor;
        Place(MakeTb(text, el.FontSize, col, el.FontName), el.X, el.Y);
    }

    // ── [SIMPLE] sensor text ──────────────────────────────────

    private void BuildSimple(Sp2Element el)
    {
        var col = el.TextColor != WpfColors.Transparent ? el.TextColor : WpfColors.White;
        var tb  = MakeTb("--", el.FontSize, col, el.FontName);
        Place(tb, el.X, el.Y);

        if (el.ShowLabel && el.Label.Length > 0)
        {
            var lblCol = el.LabelColor != WpfColors.Transparent ? el.LabelColor : col;
            Place(MakeTb(el.Label, Math.Max(8, el.FontSize - 4), lblCol, el.FontName),
                el.X, el.Y - el.FontSize - 2);
        }

        switch (el.SensorId)
        {
            case "STIME":     _timeText   = tb; break;
            case "SUPTIMENS": _uptimeText = tb; break;
            default:
                var suffix = el.ShowUnit && el.Unit.Trim().Length > 0 ? " " + el.Unit.Trim() : "";
                _updaters[el.RawId] = data =>
                {
                    float v = SensorValue(el.SensorId, data);
                    tb.Text = float.IsNaN(v) ? "--" : $"{v:F0}{suffix}";
                };
                break;
        }
    }

    // ── [GAUGE] animated frame image ─────────────────────────

    private void BuildGauge(Sp2Element el)
    {
        if (el.GaugeFrames.Count == 0) return;

        var frames = el.GaugeFrames
            .Select(f => Path.Combine(_templateDir, f))
            .Select(p => File.Exists(p) ? LoadBitmap(p) : null)
            .ToArray();

        if (frames.All(f => f is null)) return;

        var img = new Image
        {
            Source  = frames[0],
            Stretch = Stretch.Uniform
        };
        if (el.Width  > 0) img.Width  = el.Width;
        if (el.Height > 0) img.Height = el.Height;
        Place(img, el.X, el.Y);

        _updaters[el.RawId] = data =>
        {
            float v = SensorValue(el.SensorId, data);
            if (float.IsNaN(v)) return;
            int idx = (int)Math.Round((v - el.MinVal) / (el.MaxVal - el.MinVal) * (frames.Length - 1));
            idx = Math.Clamp(idx, 0, frames.Length - 1);
            if (frames[idx] is not null) img.Source = frames[idx];
        };
    }

    // ── [GRAPH] history polyline ──────────────────────────────

    private void BuildGraph(Sp2Element el)
    {
        double w = el.Width  > 0 ? el.Width  : 250;
        double h = el.Height > 0 ? el.Height : 100;

        var bgFill = el.BgColor != WpfColors.Transparent
            ? (Brush)new SolidColorBrush(el.BgColor)
            : Brushes.Transparent;

        Place(new Rectangle { Width = w, Height = h, Fill = bgFill }, el.X, el.Y);

        var gCanvas = new Canvas { Width = w, Height = h, ClipToBounds = true };
        Place(gCanvas, el.X, el.Y);

        _graphCanvases[el.SensorId] = gCanvas;
        _graphColors[el.SensorId]   = el.GraphColor != WpfColors.Transparent
            ? el.GraphColor : WpfColors.Cyan;
    }

    // ── BAR / plain sensor text ───────────────────────────────

    private void BuildBarSensor(Sp2Element el)
    {
        var valCol = el.ValueColor != WpfColors.Transparent ? el.ValueColor : WpfColors.White;
        var lblCol = el.LabelColor != WpfColors.Transparent ? el.LabelColor : el.TextColor;

        double x = el.X;
        double y = el.Y;

        if (el.ShowLabel && el.Label.Length > 0)
        {
            Place(MakeTb(el.Label + "  ", el.FontSize, lblCol, el.FontName), x, y);
            x += el.Label.Length * el.FontSize * 0.55 + 4;
        }

        var valTb = MakeTb("--", el.FontSize, valCol, el.FontName);
        Place(valTb, x, y);

        if (el.ShowUnit && el.Unit.Trim().Length > 0)
            Place(MakeTb(" " + el.Unit.Trim(), Math.Max(8, el.FontSize - 2),
                el.TextColor, el.FontName), x + (el.Width > 0 ? el.Width : 60), y);

        _updaters[el.RawId] = data =>
        {
            float v = SensorValue(el.SensorId, data);
            valTb.Text = float.IsNaN(v) ? "--" : $"{v:F0}";
        };
    }

    // ── Graph redraw ──────────────────────────────────────────

    private static void RedrawGraph(Canvas gCanvas, Queue<float> history, WpfColor lineColor)
    {
        gCanvas.Children.Clear();
        if (history.Count < 2) return;

        double w = gCanvas.Width;
        double h = gCanvas.Height;
        var pts  = history.ToArray();
        int n    = pts.Length;
        double step = w / (HistoryLen - 1);

        var points = new PointCollection(n);
        for (int i = 0; i < n; i++)
            points.Add(new Point((HistoryLen - n + i) * step, h - (pts[i] / 100.0) * h));

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

    // ── Clock tick ────────────────────────────────────────────

    private void TickClock()
    {
        if (_timeText is not null)
            _timeText.Text = DateTime.Now.ToString("HH:mm:ss");

        if (_uptimeText is not null)
        {
            var up = TimeSpan.FromMilliseconds(Environment.TickCount64);
            _uptimeText.Text = $"{(int)up.TotalDays:D2}.{up:hh\\:mm\\:ss}";
        }
    }

    // ── Sensor routing ────────────────────────────────────────

    private static float SensorValue(string sensorId, HardwareData data) => sensorId switch
    {
        "TCPUDIO"  => data.Cpu.Temperature,
        "TGPU1"    => data.Gpu.Temperature,
        "SCPUUTI"  => data.Cpu.Load,
        "SGPU1UTI" => data.Gpu.Load,
        "FCPU"     => data.Cpu.FanSpeed,
        "FCHA1"    => data.Gpu.FanSpeed,
        "SCPUCLK"  => data.Cpu.Clock,
        "SGPU1CLK" => data.Gpu.Clock,
        _          => float.NaN
    };

    // ── Helpers ───────────────────────────────────────────────

    private void Place(UIElement el, double x, double y)
    {
        Canvas.SetLeft(el, x);
        Canvas.SetTop(el, y);
        _canvas.Children.Add(el);
    }

    private static TextBlock MakeTb(string text, int fontSize, WpfColor color, string font)
        => new()
        {
            Text       = text,
            FontSize   = fontSize,
            FontFamily = new FontFamily(font),
            Foreground = new SolidColorBrush(color)
        };

    private static BitmapImage LoadBitmap(string path)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource     = new Uri(path, UriKind.Absolute);
        bmp.CacheOption   = BitmapCacheOption.OnLoad;
        bmp.CreateOptions = BitmapCreateOptions.None;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}
