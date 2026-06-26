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
    private readonly Dictionary<string, Canvas>     _graphCanvases   = new();
    private readonly Dictionary<string, WpfColor>   _graphColors     = new();
    private readonly Dictionary<string, Queue<float>> _graphHistories = new();

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

        Sp2Panel panel;
        try { panel = Sp2Parser.Parse(sp2Path); }
        catch { return; }

        BuildFromPanel(panel);
    }

    public void BuildFromPanel(Sp2Panel panel, string templateDir = "")
    {
        if (!string.IsNullOrEmpty(templateDir))
            _templateDir = templateDir;

        _updaters.Clear();
        _graphCanvases.Clear();
        _graphColors.Clear();
        _graphHistories.Clear();
        _canvas.Children.Clear();
        _clockTimer?.Stop();

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
                case "IMG":    BuildImage(el);     break;
                case "LBL":    BuildLabel(el);     break;
                case "SIMPLE": BuildSimple(el);    break;
                case "GAUGE":  BuildGauge(el);     break;
                case "GRAPH":  BuildGraph(el);     break;
                case "BAR":    BuildBarSensor(el); break;
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

        foreach (var (sensorId, gCanvas) in _graphCanvases)
        {
            if (!_graphHistories.TryGetValue(sensorId, out var history)) continue;
            EnqueueHistory(history, SensorValue(sensorId, data));
            var color = _graphColors.GetValueOrDefault(sensorId, WpfColors.Cyan);
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

        var bmp = LoadBitmap(path);
        if (bmp is null) return;
        var img = new Image
        {
            Source  = bmp,
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
                var unit   = ResolveUnit(el.SensorId, el.ShowUnit ? el.Unit : "");
                var suffix = unit.Length > 0 ? unit : "";
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

        var gCanvas = new Canvas { Width = w, Height = h, ClipToBounds = true, Background = Brushes.Transparent };
        Place(gCanvas, el.X, el.Y);

        _graphCanvases[el.SensorId]  = gCanvas;
        _graphColors[el.SensorId]    = el.GraphColor != WpfColors.Transparent
            ? el.GraphColor : WpfColors.Cyan;

        // Pre-fill history with the current sensor value so the graph is
        // immediately meaningful instead of growing in from the right edge.
        var history = new Queue<float>();
        float initVal = SensorValue(el.SensorId, _monitor.CurrentData ?? new());
        if (!float.IsNaN(initVal) && initVal > 0)
            for (int i = 0; i < HistoryLen; i++)
                history.Enqueue(initVal);
        _graphHistories[el.SensorId] = history;
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

        var valTb  = MakeTb("--", el.FontSize, valCol, el.FontName);
        Place(valTb, x, y);

        var barUnit = ResolveUnit(el.SensorId, el.ShowUnit ? el.Unit : "");
        string suffix = barUnit.Length > 0 ? barUnit : "";

        _updaters[el.RawId] = data =>
        {
            float v = SensorValue(el.SensorId, data);
            valTb.Text = float.IsNaN(v) ? "--" : $"{v:F0}{suffix}";
        };
    }

    // ── Graph redraw ──────────────────────────────────────────

    private static void RedrawGraph(Canvas gCanvas, Queue<float> history, WpfColor lineColor)
    {
        gCanvas.Children.Clear();
        if (history.Count == 0) return;

        double w   = gCanvas.Width;
        double h   = gCanvas.Height;
        var pts    = history.ToArray();
        int n      = pts.Length;
        double step = w / HistoryLen;
        int offset  = HistoryLen - n;

        var polyline = new System.Windows.Shapes.Polyline
        {
            Stroke          = new SolidColorBrush(lineColor),
            StrokeThickness = 1.5,
            StrokeLineJoin  = PenLineJoin.Round,
        };

        for (int i = 0; i < n; i++)
        {
            double x = (offset + i) * step;
            double y = h - Math.Clamp(pts[i] / 100.0, 0, 1) * h;
            polyline.Points.Add(new System.Windows.Point(x, y));
        }

        gCanvas.Children.Add(polyline);
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
        // ── CPU ──────────────────────────────────────────────────
        "TCPU" or "TCPUDIO" or "TCPU1"
                            => data.Cpu.Temperature,
        "SCPUUTI"           => data.Cpu.Load,
        "SCPUCLK" or "SCPUCLK1"
                            => data.Cpu.Clock,
        "FCPU" or "FCPU1"  => data.Motherboard.Fans.Count > 0
                                ? data.Motherboard.Fans[0] : data.Cpu.FanSpeed,

        // ── GPU ──────────────────────────────────────────────────
        "TGPU1"             => data.Gpu.Temperature,
        "SGPU1UTI"          => data.Gpu.Load,
        "SGPU1CLK"          => data.Gpu.Clock,
        "SGPU1MCLK"         => data.Gpu.MemoryClock,
        "FGPU1"             => data.Gpu.FanSpeed,
        "FCHA1"             => data.Motherboard.Fans.Count > 1
                                ? data.Motherboard.Fans[1]
                                : data.Motherboard.Fans.Count > 0 ? data.Motherboard.Fans[0] : 0,
        "FCHA2"             => data.Motherboard.Fans.Count > 2 ? data.Motherboard.Fans[2] : 0,
        // GPU memory in MB (AIDA64 reports MB for SmallData)
        "SGPU1MEM" or "SGPU1USEDMEM"
                            => data.Gpu.MemoryUsed,
        "SGPU1TOTALMEM"     => data.Gpu.MemoryTotal,

        // ── System Memory (GB → MB conversion) ───────────────────
        "SUSEDMEM" or "SMEMUSED"
                            => data.Memory.Used * 1024f,
        "SFREEMEM" or "SMEMFREE"
                            => data.Memory.Available * 1024f,
        "STOTALMEM" or "SMEMTOTAL"
                            => data.Memory.Total * 1024f,
        "SRAMUTI" or "SMEMUTI" or "SUTI"
                            => data.Memory.UsagePercent,

        // ── Network ───────────────────────────────────────────────
        "SNIC1DL" or "SNIC2DL" or "SNIC1DLRATE" or "SNIC2DLRATE"
                            => data.Network.DownloadSpeed,
        "SNIC1UL" or "SNIC2UL" or "SNIC1ULRATE" or "SNIC2ULRATE"
                            => data.Network.UploadSpeed,

        // ── Storage ───────────────────────────────────────────────
        "THDD1" or "THDD2" or "TDTS" or "TSTO"
                            => data.Storage.Temperature,

        // ── Motherboard ───────────────────────────────────────────
        "TMOBO" or "TMBSYS1"
                            => data.Motherboard.Temperature,

        // ── Audio ─────────────────────────────────────────────────
        "SVOL" or "SVOL1" or "SVOLUME" or "SMASTVOL"
                            => data.Audio.Volume,

        _                   => float.NaN
    };

    // ── Unit resolution ───────────────────────────────────────
    // Returns the display unit string for a sensor. For well-known IDs, returns
    // the standard unit regardless of what the sp2 template's UNT field says
    // (AIDA64 templates sometimes contain corrupted or locale-specific characters).
    private static string ResolveUnit(string sensorId, string templateUnit)
    {
        var known = sensorId switch
        {
            var s when s.StartsWith("T")                              => "°C",
            var s when s.EndsWith("UTI") || s is "SRAMUTI" or "SUTI" => "%",
            var s when s.EndsWith("CLK")                             => " MHz",
            var s when s.StartsWith("F") && !s.StartsWith("FREE")   => " RPM",
            var s when s.StartsWith("SNIC")                          => " B/s",
            var s when s is "SVOL" or "SVOL1" or "SVOLUME" or "SMASTVOL" => "%",
            var s when s.StartsWith("SMEM") || s.StartsWith("SUSEDMEM")
                      || s.StartsWith("SFREEMEM") || s.StartsWith("STOTALMEM")
                                                                     => " MB",
            _ => ""
        };
        if (known.Length > 0) return known;
        // Fallback to template unit, but filter non-printable/surrogate chars
        var trimmed = templateUnit.Trim();
        return trimmed.All(c => c >= ' ' && !char.IsSurrogate(c)) ? trimmed : "";
    }

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

    private static BitmapImage? LoadBitmap(string path)
    {
        try
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
        catch { return null; }
    }
}
