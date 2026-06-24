using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SSM.Core.Helpers;
using SSM.Core.Interfaces;
using SSM.Core.Models;
using Brush         = System.Windows.Media.Brush;
using Brushes       = System.Windows.Media.Brushes;
using CheckBox      = System.Windows.Controls.CheckBox;
using Color         = System.Windows.Media.Color;
using Cursors       = System.Windows.Input.Cursors;
using MessageBox    = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using ComboBox      = System.Windows.Controls.ComboBox;
using TextBox       = System.Windows.Controls.TextBox;
using WpfColor      = System.Windows.Media.Color;
using WpfColors     = System.Windows.Media.Colors;

namespace SSM.Views;

// Mutable wrapper for Sp2Element to support editing
public class EditorItem
{
    public string RawId       { get; set; } = "";
    public string SensorId    { get; set; } = "";
    public string ElementKind { get; set; } = "";
    public double X           { get; set; }
    public double Y           { get; set; }
    public double Width       { get; set; }
    public double Height      { get; set; }
    public string Label       { get; set; } = "";
    public string Unit        { get; set; } = "";
    public bool   ShowLabel   { get; set; }
    public bool   ShowValue   { get; set; } = true;
    public bool   ShowUnit    { get; set; } = true;
    public string FontName    { get; set; } = "Segoe UI";
    public int    FontSize    { get; set; } = 12;
    public WpfColor TextColor  { get; set; } = WpfColor.FromRgb(255, 255, 255);
    public WpfColor ValueColor { get; set; } = WpfColor.FromRgb(255, 255, 255);
    public WpfColor LabelColor { get; set; } = WpfColor.FromRgb(255, 255, 255);
    public double MinVal      { get; set; }
    public double MaxVal      { get; set; } = 100;
    public string ImageFile   { get; set; } = "";
    public List<string> GaugeFrames { get; set; } = [];
    public WpfColor GraphColor { get; set; } = WpfColor.FromRgb(0, 200, 255);
    public WpfColor BgColor    { get; set; } = WpfColors.Transparent;
    public bool   IsBackground { get; set; }

    public static EditorItem From(Sp2Element e) => new()
    {
        RawId       = e.RawId,
        SensorId    = e.SensorId,
        ElementKind = e.ElementKind,
        X = e.X, Y = e.Y, Width = e.Width, Height = e.Height,
        Label = e.Label, Unit = e.Unit,
        ShowLabel = e.ShowLabel, ShowValue = e.ShowValue, ShowUnit = e.ShowUnit,
        FontName = e.FontName, FontSize = e.FontSize,
        TextColor = e.TextColor, ValueColor = e.ValueColor, LabelColor = e.LabelColor,
        MinVal = e.MinVal, MaxVal = e.MaxVal,
        ImageFile = e.ImageFile, GaugeFrames = new List<string>(e.GaugeFrames),
        GraphColor = e.GraphColor, BgColor = e.BgColor, IsBackground = e.IsBackground,
    };

    public Sp2Element ToSp2Element() => new(
        RawId, SensorId, ElementKind, X, Y, Width, Height,
        Label, Unit, ShowLabel, ShowValue, ShowUnit,
        FontName, FontSize,
        TextColor, ValueColor, LabelColor,
        MinVal, MaxVal,
        ImageFile, new List<string>(GaugeFrames),
        GraphColor, BgColor, IsBackground);

    public string DisplayName => ElementKind switch
    {
        "IMG"    => $"[IMG] {Path.GetFileName(ImageFile)}",
        "LBL"    => $"[LBL] {Label}",
        "SIMPLE" => $"[简] {SensorId}",
        "GAUGE"  => $"[仪] {SensorId}",
        "GRAPH"  => $"[图] {SensorId}",
        _        => $"[传] {SensorId}",
    };
}

public partial class ThemeEditorWindow : Window
{
    private readonly string _sp2Path;
    private Sp2Panel _panel = new();
    private readonly List<EditorItem> _items = [];
    private bool _dirty;

    private EditorItem? _selected;
    private Border? _selectedOverlay;
    private bool _dragging;
    private System.Windows.Point _dragOffset;
    private bool _updatingPanel;

    private OverlayCanvasRenderer? _renderer;
    private readonly MockMonitorService _mockService = new();
    private double _zoom = 1.0;

    // ── Mock monitor service for in-editor preview ────────────────
    private sealed class MockMonitorService : IHardwareMonitorService
    {
        public HardwareData CurrentData { get; } = new();
        public event EventHandler<HardwareData> DataUpdated { add { } remove { } }
        public string CpuName => "CPU";
        public string GpuName => "GPU";
        public void Start(int intervalMs = 1000) { }
        public void Stop() { }
        public void Dispose() { }
    }

    public ThemeEditorWindow(string sp2Path)
    {
        InitializeComponent();
        _sp2Path = sp2Path;
        Title = $"sp2 编辑器 — {Path.GetFileName(sp2Path)}";
        Loaded += (_, _) =>
        {
            LoadSp2();
            Dispatcher.InvokeAsync(ZoomFit, System.Windows.Threading.DispatcherPriority.Loaded);
        };
    }

    // ── Load ─────────────────────────────────────────────────────

    private void LoadSp2()
    {
        try { _panel = Sp2Parser.Parse(_sp2Path); }
        catch { _panel = new Sp2Panel(); }

        _items.Clear();
        ElementList.Items.Clear();

        foreach (var el in _panel.Elements)
        {
            var item = EditorItem.From(el);
            _items.Add(item);
            AddListItem(item);
        }

        RebuildPreview();
    }

    private void AddListItem(EditorItem item)
    {
        var listItem = new ListBoxItem
        {
            Content = item.DisplayName,
            Tag     = item,
            Padding = new Thickness(8, 4, 8, 4),
        };
        listItem.SetResourceReference(ListBoxItem.ForegroundProperty, "TextPrimary");
        ElementList.Items.Add(listItem);
    }

    // ── Preview ───────────────────────────────────────────────────

    private void RebuildPreview()
    {
        _renderer?.Dispose();
        _panel.Elements = _items.Select(i => i.ToSp2Element()).ToList();

        _renderer = new OverlayCanvasRenderer(RenderCanvas, _mockService);
        _renderer.BuildFromPanel(_panel, Path.GetDirectoryName(_sp2Path) ?? "");
        _renderer.OnDataUpdated(BuildMockData());

        // Sync container and selection canvas to actual panel dimensions
        double pw = _panel.Width  > 0 ? _panel.Width  : 1024;
        double ph = _panel.Height > 0 ? _panel.Height : 600;
        CanvasContainer.Width  = pw;
        CanvasContainer.Height = ph;
        SelectionCanvas.Width  = pw;
        SelectionCanvas.Height = ph;

        RebuildSelectionOverlays();
    }

    // ── Zoom ──────────────────────────────────────────────────────

    private void ApplyZoom()
    {
        CanvasScale.ScaleX = _zoom;
        CanvasScale.ScaleY = _zoom;
        ZoomText.Text = $"{_zoom:P0}";
    }

    private void ZoomFit()
    {
        double availW = CanvasScroller.ActualWidth  - 20;
        double availH = CanvasScroller.ActualHeight - 20;
        double pw = _panel.Width  > 0 ? _panel.Width  : 1024;
        double ph = _panel.Height > 0 ? _panel.Height : 600;
        if (availW <= 0 || availH <= 0) { _zoom = 1.0; ApplyZoom(); return; }
        _zoom = Math.Min(availW / pw, availH / ph);
        _zoom = Math.Max(0.05, Math.Min(4.0, _zoom));
        ApplyZoom();
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Min(4.0, _zoom * 1.25);
        ApplyZoom();
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Max(0.05, _zoom / 1.25);
        ApplyZoom();
    }

    private void ZoomFit_Click(object sender, RoutedEventArgs e) => ZoomFit();

    private void CanvasScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            _zoom = e.Delta > 0 ? Math.Min(4.0, _zoom * 1.1) : Math.Max(0.05, _zoom / 1.1);
            ApplyZoom();
            e.Handled = true;
        }
    }

    private void RebuildSelectionOverlays()
    {
        SelectionCanvas.Children.Clear();
        _selectedOverlay = null;

        foreach (var item in _items)
        {
            bool isSelected = item == _selected;
            var overlay = new Border
            {
                Width           = Math.Max(item.Width, 10),
                Height          = Math.Max(item.Height, 10),
                Background      = Brushes.Transparent,
                BorderBrush     = isSelected
                    ? (Brush)FindResource("Accent")
                    : new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                Tag             = item,
                Cursor          = Cursors.SizeAll,
            };
            Canvas.SetLeft(overlay, item.X);
            Canvas.SetTop(overlay, item.Y);
            overlay.MouseLeftButtonDown += SelectionOverlay_MouseDown;
            SelectionCanvas.Children.Add(overlay);

            if (isSelected) _selectedOverlay = overlay;
        }
    }

    private HardwareData BuildMockData()
    {
        var data = new HardwareData();
        data.Cpu.Temperature       = GetMockVal(MockCpuTemp,  55f);
        data.Cpu.Load              = GetMockVal(MockCpuLoad,  35f);
        data.Cpu.Clock             = GetMockVal(MockCpuClock, 4500f);
        data.Gpu.Temperature       = GetMockVal(MockGpuTemp,  65f);
        data.Gpu.Load              = GetMockVal(MockGpuLoad,  40f);
        data.Gpu.Clock             = GetMockVal(MockGpuClock, 1800f);
        data.Memory.Used           = GetMockVal(MockMemUsed,  16384f);
        data.Memory.Total          = 32768f;
        data.Network.DownloadSpeed = GetMockVal(MockNetDL,    1048576f);
        data.Network.UploadSpeed   = GetMockVal(MockNetUL,    524288f);
        data.Storage.Temperature   = GetMockVal(MockStoTemp,  35f);
        data.Audio.Volume          = GetMockVal(MockVolume,   50f);
        data.Motherboard.Temperature = 45f;
        data.Motherboard.Fans.Clear();
        data.Motherboard.Fans.AddRange([1200f, 800f, 0f]);
        return data;
    }

    private static float GetMockVal(TextBox? tb, float fallback)
    {
        if (tb is null || !float.TryParse(tb.Text, out var v)) return fallback;
        return v;
    }

    private void MockValue_Changed(object sender, TextChangedEventArgs e)
    {
        _renderer?.OnDataUpdated(BuildMockData());
    }

    // ── Save ─────────────────────────────────────────────────────

    private void Save()
    {
        _panel.Elements = _items.Select(i => i.ToSp2Element()).ToList();
        try
        {
            Sp2Writer.Write(_sp2Path, _panel);
            _dirty = false;
            if (Title.StartsWith("* ")) Title = Title[2..];
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e) => Save();

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_dirty)
        {
            var r = MessageBox.Show("有未保存的更改，是否保存？", "保存",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes) Save();
            if (r == MessageBoxResult.Cancel) { e.Cancel = true; return; }
        }
        _renderer?.Dispose();
    }

    // ── Add elements ─────────────────────────────────────────────

    private void AddAndSelect(EditorItem item)
    {
        _items.Add(item);
        AddListItem(item);
        _selected = item;
        RebuildPreview();
        UpdatePropertyPanel();
        DeleteSelectedButton.IsEnabled = true;
        MoveUpButton.IsEnabled   = true;
        MoveDownButton.IsEnabled = true;
        MarkDirty();
    }

    private void AddLabel_Click(object sender, RoutedEventArgs e) =>
        AddAndSelect(new EditorItem
        {
            ElementKind = "LBL", RawId = "LBL",
            Label = "文字", FontSize = 14,
            X = 100, Y = 100, Width = 120, Height = 24,
            TextColor = WpfColor.FromRgb(255, 255, 255),
        });

    private void AddSimple_Click(object sender, RoutedEventArgs e) =>
        AddAndSelect(new EditorItem
        {
            ElementKind = "SIMPLE", RawId = "[SIMPLE]TCPU",
            SensorId = "TCPU", Label = "CPU", Unit = "°C",
            ShowLabel = true, ShowValue = true, ShowUnit = true,
            FontSize = 14, X = 100, Y = 100, Width = 120, Height = 24,
            TextColor = WpfColor.FromRgb(255, 255, 255),
            ValueColor = WpfColor.FromRgb(0, 200, 255),
        });

    private void AddGauge_Click(object sender, RoutedEventArgs e)
    {
        var item = new EditorItem
        {
            ElementKind = "GAUGE", RawId = "[GAUGE]SCPUUTI", SensorId = "SCPUUTI",
            X = 100, Y = 100, Width = 64, Height = 64, MaxVal = 100,
        };
        AddAndSelect(item);
    }

    private void AddGraph_Click(object sender, RoutedEventArgs e)
    {
        var item = new EditorItem
        {
            ElementKind = "GRAPH", RawId = "[GRAPH]SCPUUTI", SensorId = "SCPUUTI",
            X = 100, Y = 100, Width = 200, Height = 60, MaxVal = 100,
            GraphColor = WpfColor.FromRgb(0, 200, 255),
        };
        AddAndSelect(item);
    }

    private void AddImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "选择图片",
            Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*",
        };
        if (dlg.ShowDialog(this) != true) return;

        AddAndSelect(new EditorItem
        {
            ElementKind = "IMG", RawId = "IMG",
            ImageFile = dlg.FileName,
            X = 0, Y = 0, Width = (int)_panel.Width, Height = (int)_panel.Height,
        });
    }

    // ── Delete / Move ─────────────────────────────────────────────

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;

        var li = ElementList.Items.OfType<ListBoxItem>()
            .FirstOrDefault(x => x.Tag == _selected);
        if (li is not null) ElementList.Items.Remove(li);
        _items.Remove(_selected);

        _selected = null;
        _selectedOverlay = null;
        Deselect();
        RebuildPreview();
        MarkDirty();
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)   => MoveSelected(-1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => MoveSelected(+1);

    private void MoveSelected(int delta)
    {
        if (_selected is null) return;
        int idx    = _items.IndexOf(_selected);
        int newIdx = idx + delta;
        if (newIdx < 0 || newIdx >= _items.Count) return;

        (_items[idx], _items[newIdx]) = (_items[newIdx], _items[idx]);

        // Swap in ListBox
        var liA = ElementList.Items.OfType<ListBoxItem>().FirstOrDefault(x => x.Tag == _items[idx]);
        var liB = ElementList.Items.OfType<ListBoxItem>().FirstOrDefault(x => x.Tag == _items[newIdx]);
        if (liA is not null && liB is not null)
        {
            int ia = ElementList.Items.IndexOf(liA);
            int ib = ElementList.Items.IndexOf(liB);
            ElementList.Items.RemoveAt(Math.Max(ia, ib));
            ElementList.Items.RemoveAt(Math.Min(ia, ib));
            if (ia < ib) { ElementList.Items.Insert(ia, liB); ElementList.Items.Insert(ib, liA); }
            else         { ElementList.Items.Insert(ib, liA); ElementList.Items.Insert(ia, liB); }
        }

        ElementList.SelectedItem = ElementList.Items.OfType<ListBoxItem>()
            .FirstOrDefault(x => x.Tag == _selected);
        RebuildPreview();
        MarkDirty();
    }

    // ── Drag (on transparent SelectionCanvas overlays) ────────────

    private void SelectionOverlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border b || b.Tag is not EditorItem item) return;
        SelectItem(item);
        _dragging = true;
        _dragOffset = e.GetPosition(b);
        b.CaptureMouse();
        e.Handled = true;
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || _selectedOverlay is null || _selected is null) return;
        var pos = e.GetPosition(SelectionCanvas);
        double x = Math.Max(0, Math.Min(pos.X - _dragOffset.X, SelectionCanvas.Width  - _selected.Width));
        double y = Math.Max(0, Math.Min(pos.Y - _dragOffset.Y, SelectionCanvas.Height - _selected.Height));
        Canvas.SetLeft(_selectedOverlay, x);
        Canvas.SetTop(_selectedOverlay, y);
        _selected.X = x;
        _selected.Y = y;
        _updatingPanel = true;
        PropX.Text = $"{x:F0}";
        PropY.Text = $"{y:F0}";
        _updatingPanel = false;
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        _selectedOverlay?.ReleaseMouseCapture();
        RebuildPreview();
        MarkDirty();
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        SelectionCanvas.Focus();
        if (e.OriginalSource == SelectionCanvas) Deselect();
    }

    // ── Selection ────────────────────────────────────────────────

    private void SelectItem(EditorItem item)
    {
        _selected = item;
        // Update overlay highlights
        _selectedOverlay = null;
        foreach (var child in SelectionCanvas.Children.OfType<Border>())
        {
            bool isSel = child.Tag == item;
            child.BorderBrush     = isSel
                ? (Brush)FindResource("Accent")
                : new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            child.BorderThickness = new Thickness(isSel ? 2 : 1);
            if (isSel) _selectedOverlay = child;
        }

        var li = ElementList.Items.OfType<ListBoxItem>().FirstOrDefault(x => x.Tag == item);
        if (li is not null) ElementList.SelectedItem = li;

        DeleteSelectedButton.IsEnabled = true;
        MoveUpButton.IsEnabled   = true;
        MoveDownButton.IsEnabled = true;
        UpdatePropertyPanel();
    }

    private void Deselect()
    {
        _selected = null;
        _selectedOverlay = null;
        foreach (var child in SelectionCanvas.Children.OfType<Border>())
        {
            child.BorderBrush     = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            child.BorderThickness = new Thickness(1);
        }
        ElementList.SelectedItem = null;
        DeleteSelectedButton.IsEnabled = false;
        MoveUpButton.IsEnabled   = false;
        MoveDownButton.IsEnabled = false;
        NothingSelected.Visibility = Visibility.Visible;
        PropPanel.Visibility       = Visibility.Collapsed;
    }

    private void ElementList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ElementList.SelectedItem is ListBoxItem li &&
            li.Tag is EditorItem item && item != _selected)
            SelectItem(item);
    }

    // ── Property panel ────────────────────────────────────────────

    private void UpdatePropertyPanel()
    {
        if (_selected is null) return;
        _updatingPanel = true;

        NothingSelected.Visibility = Visibility.Collapsed;
        PropPanel.Visibility       = Visibility.Visible;

        var kind = _selected.ElementKind;
        PropKindLabel.Text = kind switch
        {
            "IMG"    => "图片 (IMG)",
            "LBL"    => "静态文字 (LBL)",
            "SIMPLE" => $"传感器文字 (SIMPLE) · {_selected.SensorId}",
            "GAUGE"  => $"仪表盘 (GAUGE) · {_selected.SensorId}",
            "GRAPH"  => $"折线图 (GRAPH) · {_selected.SensorId}",
            _        => $"传感器 (BAR) · {_selected.SensorId}",
        };

        PropX.Text = $"{_selected.X:F0}";
        PropY.Text = $"{_selected.Y:F0}";
        PropW.Text = $"{_selected.Width:F0}";
        PropH.Text = $"{_selected.Height:F0}";

        bool hasSensor = kind is "SIMPLE" or "GAUGE" or "GRAPH" or "BAR";
        bool hasText   = kind is "LBL" or "SIMPLE" or "BAR";
        bool hasRange  = kind is "GAUGE" or "GRAPH";

        PanelSensorId.Visibility = hasSensor ? Visibility.Visible : Visibility.Collapsed;
        PanelLabel.Visibility    = hasText   ? Visibility.Visible : Visibility.Collapsed;
        PanelUnit.Visibility     = hasText   ? Visibility.Visible : Visibility.Collapsed;
        PanelShow.Visibility     = hasText   ? Visibility.Visible : Visibility.Collapsed;
        PanelFont.Visibility     = kind is not "IMG" ? Visibility.Visible : Visibility.Collapsed;
        PanelColor.Visibility    = kind is not "IMG" ? Visibility.Visible : Visibility.Collapsed;
        PanelRange.Visibility    = hasRange  ? Visibility.Visible : Visibility.Collapsed;
        PanelImage.Visibility    = kind == "IMG" ? Visibility.Visible : Visibility.Collapsed;

        if (hasSensor) PropSensorId.Text = _selected.SensorId; // sets editable text in ComboBox
        if (hasText)
        {
            PropLabel.Text          = _selected.Label;
            PropUnit.Text           = _selected.Unit;
            PropShowLabel.IsChecked = _selected.ShowLabel;
            PropShowValue.IsChecked = _selected.ShowValue;
            PropShowUnit.IsChecked  = _selected.ShowUnit;
        }
        if (kind != "IMG")
        {
            PropFontName.Text   = _selected.FontName;
            PropFontSize.Text   = _selected.FontSize.ToString();
            PropTextColor.Text  = ColorToHex(_selected.TextColor);
            PropValueColor.Text = ColorToHex(_selected.ValueColor);
        }
        if (hasRange)
        {
            PropMinVal.Text = _selected.MinVal.ToString();
            PropMaxVal.Text = _selected.MaxVal.ToString();
        }
        if (kind == "IMG") PropImageFile.Text = _selected.ImageFile;

        _updatingPanel = false;
    }

    private void Prop_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingPanel || _selected is null) return;

        // Accept TextBox or the editable inner TextBox inside a ComboBox
        string? tag;
        string? text;
        if (sender is TextBox tb)
        {
            tag  = tb.Tag?.ToString();
            text = tb.Text;
        }
        else if (sender is ComboBox cb)
        {
            tag  = cb.Tag?.ToString();
            text = cb.Text;
        }
        else return;

        bool needsRebuild = true;
        switch (tag)
        {
            case "X":
                if (double.TryParse(text, out var x))
                {
                    _selected.X = x;
                    if (_selectedOverlay is not null) { Canvas.SetLeft(_selectedOverlay, x); needsRebuild = false; }
                }
                break;
            case "Y":
                if (double.TryParse(text, out var y))
                {
                    _selected.Y = y;
                    if (_selectedOverlay is not null) { Canvas.SetTop(_selectedOverlay, y); needsRebuild = false; }
                }
                break;
            case "W":
                if (double.TryParse(text, out var w) && w > 0)
                {
                    _selected.Width = w;
                    if (_selectedOverlay is not null) _selectedOverlay.Width = w;
                }
                break;
            case "H":
                if (double.TryParse(text, out var h) && h > 0)
                {
                    _selected.Height = h;
                    if (_selectedOverlay is not null) _selectedOverlay.Height = h;
                }
                break;
            case "SensorId":
                ApplySensorId(text ?? "");
                break;
            case "Label":
                if (sender is TextBox lbTb) { _selected.Label = lbTb.Text; RefreshListItem(_selected); }
                break;
            case "Unit":      if (sender is TextBox utTb) _selected.Unit     = utTb.Text; break;
            case "FontName":  if (sender is TextBox fnTb) _selected.FontName  = fnTb.Text.Trim(); break;
            case "FontSize":
                if (int.TryParse(text, out var fs) && fs > 0) _selected.FontSize = fs;
                break;
            case "TextColor":
                if (TryParseHexColor(text ?? "", out var tc)) _selected.TextColor = tc;
                break;
            case "ValueColor":
                if (TryParseHexColor(text ?? "", out var vc)) _selected.ValueColor = vc;
                break;
            case "MinVal":
                if (double.TryParse(text, out var mn)) _selected.MinVal = mn;
                break;
            case "MaxVal":
                if (double.TryParse(text, out var mx)) _selected.MaxVal = mx;
                break;
            case "ImageFile":
                if (sender is TextBox ifTb) _selected.ImageFile = ifTb.Text;
                break;
            default:
                needsRebuild = false;
                break;
        }

        if (needsRebuild) RebuildPreview();
        MarkDirty();
    }

    private void ApplySensorId(string raw)
    {
        if (_selected is null) return;
        _selected.SensorId = raw.Trim().ToUpperInvariant();
        _selected.RawId = _selected.ElementKind switch
        {
            "SIMPLE" => $"[SIMPLE]{_selected.SensorId}",
            "GAUGE"  => $"[GAUGE]{_selected.SensorId}",
            "GRAPH"  => $"[GRAPH]{_selected.SensorId}",
            _        => _selected.SensorId,
        };
        RefreshListItem(_selected);
        UpdatePropertyPanel();
    }

    private void PropSensorId_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingPanel || _selected is null) return;
        if (PropSensorId.SelectedItem is ComboBoxItem item && item.Content is string id)
        {
            ApplySensorId(id);
            RebuildPreview();
            MarkDirty();
        }
    }

    private void SelectionCanvas_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
            Keyboard.ClearFocus();
    }

    private void PropCheck_Click(object sender, RoutedEventArgs e)
    {
        if (_updatingPanel || _selected is null || sender is not CheckBox cb) return;
        bool val = cb.IsChecked == true;
        switch (cb.Tag?.ToString())
        {
            case "ShowLabel": _selected.ShowLabel = val; break;
            case "ShowValue": _selected.ShowValue = val; break;
            case "ShowUnit":  _selected.ShowUnit  = val; break;
        }
        RebuildPreview();
        MarkDirty();
    }

    private void BrowseImage_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var dlg = new OpenFileDialog
        {
            Title  = "选择图片",
            Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp|所有文件|*.*",
        };
        if (dlg.ShowDialog(this) != true) return;
        _selected.ImageFile = dlg.FileName;
        PropImageFile.Text  = dlg.FileName;
        RebuildPreview();
        MarkDirty();
    }

    // ── Helpers ───────────────────────────────────────────────────

    private void RefreshListItem(EditorItem item)
    {
        foreach (ListBoxItem li in ElementList.Items)
        {
            if (li.Tag == item) { li.Content = item.DisplayName; break; }
        }
    }

    private void MarkDirty()
    {
        if (!_dirty)
        {
            _dirty = true;
            if (!Title.StartsWith('*')) Title = "* " + Title;
        }
    }

    private static string ColorToHex(WpfColor c) => $"{c.R:X2}{c.G:X2}{c.B:X2}";

    private static bool TryParseHexColor(string hex, out WpfColor color)
    {
        hex = hex.TrimStart('#').Trim();
        color = WpfColor.FromRgb(255, 255, 255);
        if (hex.Length != 6) return false;
        try
        {
            color = WpfColor.FromRgb(
                Convert.ToByte(hex[0..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16));
            return true;
        }
        catch { return false; }
    }
}
