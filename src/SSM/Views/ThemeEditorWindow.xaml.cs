using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SSM.Core.Models;

namespace SSM.Views;

public partial class ThemeEditorWindow : Window
{
    private ThemeConfig _theme = new();
    private readonly string _filePath;
    private bool _dirty;

    private ComponentConfig? _selected;
    private Border? _selectedBorder;

    private bool _dragging;
    private System.Windows.Point _dragOffset;
    private Border? _dragTarget;
    private ComponentConfig? _dragComp;

    private int _textCount, _progressCount, _sensorCount, _imageCount;

    public ThemeEditorWindow(string filePath)
    {
        InitializeComponent();
        _filePath = filePath;
        LoadTheme();
    }

    // ── 加载 ──────────────────────────────────────────────────

    private void LoadTheme()
    {
        try
        {
            var json = File.ReadAllText(_filePath);
            _theme = JsonSerializer.Deserialize<ThemeConfig>(json) ?? new ThemeConfig();
        }
        catch { _theme = new ThemeConfig(); }

        Title          = $"主题编辑器 — {_theme.Name}";
        BgColorBox.Text = _theme.Background;
        ApplyCanvasBackground();

        foreach (var comp in _theme.Components)
            AddElementToCanvas(comp);
    }

    private void ApplyCanvasBackground()
    {
        try
        {
            CanvasArea.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_theme.Background));
        }
        catch { CanvasArea.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black); }
    }

    // ── 保存 ──────────────────────────────────────────────────

    private void Save()
    {
        var json = JsonSerializer.Serialize(_theme, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
        _dirty = false;
        Title  = $"主题编辑器 — {_theme.Name}";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e) => Save();

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_dirty) return;
        var result = System.Windows.MessageBox.Show("有未保存的更改，是否保存？",
            "保存主题", System.Windows.MessageBoxButton.YesNoCancel, System.Windows.MessageBoxImage.Question);
        if (result == System.Windows.MessageBoxResult.Yes)    Save();
        if (result == System.Windows.MessageBoxResult.Cancel) e.Cancel = true;
    }

    // ── 添加元素 ──────────────────────────────────────────────

    private void AddText_Click(object sender, RoutedEventArgs e)
    {
        _textCount++;
        var comp = new ComponentConfig
        {
            Type = "Text", Label = $"文字 {_textCount}",
            Text = "示例文字", X = 100, Y = 100,
            Width = 200, Height = 40, FontSize = 24, FontColor = "#FFFFFF",
        };
        _theme.Components.Add(comp);
        AddElementToCanvas(comp);
        SelectElement(comp);
        _dirty = true;
    }

    private void AddProgressBar_Click(object sender, RoutedEventArgs e)
    {
        _progressCount++;
        var comp = new ComponentConfig
        {
            Type = "ProgressBar", Label = $"进度条 {_progressCount}",
            DataBinding = "CPU.Load", X = 100, Y = 160, Width = 400, Height = 24,
        };
        _theme.Components.Add(comp);
        AddElementToCanvas(comp);
        SelectElement(comp);
        _dirty = true;
    }

    private void AddSensorText_Click(object sender, RoutedEventArgs e)
    {
        _sensorCount++;
        var comp = new ComponentConfig
        {
            Type = "SensorText", Label = $"传感器 {_sensorCount}",
            DataBinding = "CPU.Temperature", X = 100, Y = 220,
            Width = 200, Height = 40, FontSize = 28, FontColor = "#FFFFFF",
        };
        _theme.Components.Add(comp);
        AddElementToCanvas(comp);
        SelectElement(comp);
        _dirty = true;
    }

    private void AddImage_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "选择图片",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*",
        };
        if (dlg.ShowDialog() != true) return;

        _imageCount++;
        var comp = new ComponentConfig
        {
            Type = "Image", Label = $"图片 {_imageCount}",
            DataBinding = dlg.FileName,
            X = 0, Y = 0, Width = 1920, Height = 1080, Opacity = 0.5,
        };
        _theme.Components.Add(comp);
        AddElementToCanvas(comp);
        SelectElement(comp);
        _dirty = true;
    }

    // ── Canvas 元素创建 ────────────────────────────────────────

    private void AddElementToCanvas(ComponentConfig comp)
    {
        var inner = BuildInnerControl(comp);
        var border = new Border
        {
            Width           = comp.Width,
            Height          = comp.Height,
            Opacity         = comp.Opacity,
            Child           = inner,
            BorderBrush     = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(80, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Tag             = comp,
            Cursor          = System.Windows.Input.Cursors.SizeAll,
        };

        Canvas.SetLeft(border, comp.X);
        Canvas.SetTop(border,  comp.Y);

        border.MouseLeftButtonDown += Border_MouseLeftButtonDown;
        border.MouseLeftButtonUp   += Border_MouseLeftButtonUp;

        CanvasArea.Children.Add(border);

        var item = new ListBoxItem { Content = comp.Label, Tag = comp };
        ElementList.Items.Add(item);
    }

    private UIElement BuildInnerControl(ComponentConfig comp)
    {
        switch (comp.Type)
        {
            case "Text":
            case "SensorText":
            {
                return new System.Windows.Controls.TextBlock
                {
                    Text                = comp.Type == "Text" ? comp.Text : $"[{comp.DataBinding}]",
                    FontSize            = comp.FontSize,
                    Foreground          = ParseBrush(comp.FontColor),
                    VerticalAlignment   = System.Windows.VerticalAlignment.Center,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    TextWrapping        = System.Windows.TextWrapping.Wrap,
                };
            }
            case "ProgressBar":
            {
                return new System.Windows.Controls.ProgressBar
                {
                    Value           = 65,
                    Maximum         = 100,
                    Height          = comp.Height,
                    Foreground      = ParseBrush("#0A84FF"),
                    Background      = ParseBrush("#3A3A3C"),
                    BorderThickness = new Thickness(0),
                };
            }
            case "Image":
            {
                try
                {
                    return new System.Windows.Controls.Image
                    {
                        Source  = new System.Windows.Media.Imaging.BitmapImage(new Uri(comp.DataBinding)),
                        Stretch = System.Windows.Media.Stretch.UniformToFill,
                    };
                }
                catch
                {
                    return new System.Windows.Controls.TextBlock
                    {
                        Text                = "[图片]",
                        Foreground          = System.Windows.Media.Brushes.Gray,
                        VerticalAlignment   = System.Windows.VerticalAlignment.Center,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    };
                }
            }
            default:
                return new System.Windows.Controls.TextBlock
                {
                    Text       = comp.Type,
                    Foreground = System.Windows.Media.Brushes.White,
                };
        }
    }

    private static System.Windows.Media.SolidColorBrush ParseBrush(string hex)
    {
        try
        {
            return new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
        }
        catch { return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White); }
    }

    // ── 拖拽 ──────────────────────────────────────────────────

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border b) return;
        _dragging   = true;
        _dragTarget = b;
        _dragComp   = b.Tag as ComponentConfig;
        _dragOffset = e.GetPosition(b);
        b.CaptureMouse();
        if (_dragComp != null) SelectElement(_dragComp);
        e.Handled = true;
        CanvasArea.MouseMove         += Canvas_MouseMove;
        CanvasArea.MouseLeftButtonUp += Canvas_GlobalMouseUp;
    }

    private void Border_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        FinishDrag(sender as Border);
    }

    private void Canvas_GlobalMouseUp(object sender, MouseButtonEventArgs e)
    {
        FinishDrag(_dragTarget);
    }

    private void FinishDrag(Border? b)
    {
        if (!_dragging) return;
        _dragging = false;
        b?.ReleaseMouseCapture();
        CanvasArea.MouseMove         -= Canvas_MouseMove;
        CanvasArea.MouseLeftButtonUp -= Canvas_GlobalMouseUp;
    }

    private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_dragging || _dragTarget == null || _dragComp == null) return;
        var pos = e.GetPosition(CanvasArea);
        double x = Math.Max(0, Math.Min(pos.X - _dragOffset.X, 1920 - _dragComp.Width));
        double y = Math.Max(0, Math.Min(pos.Y - _dragOffset.Y, 1080 - _dragComp.Height));
        Canvas.SetLeft(_dragTarget, x);
        Canvas.SetTop(_dragTarget,  y);
        _dragComp.X = x;
        _dragComp.Y = y;
        UpdatePropertyPanel();
        _dirty = true;
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == CanvasArea) DeselectAll();
    }

    // ── 选中 ──────────────────────────────────────────────────

    private void SelectElement(ComponentConfig comp)
    {
        DeselectAll();
        _selected = comp;

        foreach (UIElement child in CanvasArea.Children)
        {
            if (child is Border b && b.Tag == comp)
            {
                _selectedBorder         = b;
                b.BorderBrush           = (System.Windows.Media.Brush)FindResource("Accent");
                b.BorderThickness       = new Thickness(2);
                break;
            }
        }

        foreach (ListBoxItem item in ElementList.Items)
        {
            if (item.Tag == comp) { ElementList.SelectedItem = item; break; }
        }

        DeleteSelectedButton.IsEnabled = true;
        UpdatePropertyPanel();
    }

    private void DeselectAll()
    {
        if (_selectedBorder != null)
        {
            _selectedBorder.BorderBrush     = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(80, 255, 255, 255));
            _selectedBorder.BorderThickness = new Thickness(1);
            _selectedBorder = null;
        }
        _selected                      = null;
        ElementList.SelectedItem       = null;
        DeleteSelectedButton.IsEnabled = false;
        NothingSelected.Visibility     = Visibility.Visible;
        PanelPosition.Visibility       = Visibility.Collapsed;
    }

    private void ElementList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ElementList.SelectedItem is ListBoxItem item &&
            item.Tag is ComponentConfig comp && comp != _selected)
        {
            SelectElement(comp);
        }
    }

    // ── 删除选中 ──────────────────────────────────────────────

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;

        Border? toRemove = null;
        foreach (UIElement child in CanvasArea.Children)
        {
            if (child is Border b && b.Tag == _selected) { toRemove = b; break; }
        }
        if (toRemove != null) CanvasArea.Children.Remove(toRemove);

        ListBoxItem? listItem = null;
        foreach (ListBoxItem li in ElementList.Items)
        {
            if (li.Tag == _selected) { listItem = li; break; }
        }
        if (listItem != null) ElementList.Items.Remove(listItem);

        _theme.Components.Remove(_selected);
        _selected                      = null;
        _selectedBorder                = null;
        DeleteSelectedButton.IsEnabled = false;
        NothingSelected.Visibility     = Visibility.Visible;
        PanelPosition.Visibility       = Visibility.Collapsed;
        _dirty = true;
    }

    // ── 属性面板 ──────────────────────────────────────────────

    private bool _updatingPanel;

    private void UpdatePropertyPanel()
    {
        if (_selected == null) return;
        _updatingPanel = true;

        NothingSelected.Visibility = Visibility.Collapsed;
        PanelPosition.Visibility   = Visibility.Visible;

        PropX.Text = ((int)_selected.X).ToString();
        PropY.Text = ((int)_selected.Y).ToString();
        PropW.Text = ((int)_selected.Width).ToString();
        PropH.Text = ((int)_selected.Height).ToString();

        PropOpacity.Value     = _selected.Opacity;
        PropOpacityLabel.Text = _selected.Opacity.ToString("F2");

        bool isText    = _selected.Type is "Text" or "SensorText";
        bool isSensor  = _selected.Type is "SensorText" or "ProgressBar";
        bool isStaticT = _selected.Type == "Text";

        PanelFont.Visibility    = isText    ? Visibility.Visible : Visibility.Collapsed;
        PanelText.Visibility    = isStaticT ? Visibility.Visible : Visibility.Collapsed;
        PanelBinding.Visibility = isSensor  ? Visibility.Visible : Visibility.Collapsed;

        if (isText)    { PropFontSize.Text  = _selected.FontSize.ToString("F0"); PropFontColor.Text = _selected.FontColor; }
        if (isStaticT) PropText.Text    = _selected.Text;
        if (isSensor)  PropBinding.Text = _selected.DataBinding;

        _updatingPanel = false;
    }

    private void ApplySelectedToCanvas()
    {
        if (_selectedBorder == null || _selected == null) return;
        _selectedBorder.Width   = _selected.Width;
        _selectedBorder.Height  = _selected.Height;
        _selectedBorder.Opacity = _selected.Opacity;
        Canvas.SetLeft(_selectedBorder, _selected.X);
        Canvas.SetTop(_selectedBorder,  _selected.Y);
        _selectedBorder.Child = BuildInnerControl(_selected);
        _dirty = true;
    }

    private void PropX_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingPanel || _selected == null) return;
        if (double.TryParse(PropX.Text, out var v)) { _selected.X = v; ApplySelectedToCanvas(); }
    }

    private void PropY_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingPanel || _selected == null) return;
        if (double.TryParse(PropY.Text, out var v)) { _selected.Y = v; ApplySelectedToCanvas(); }
    }

    private void PropW_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingPanel || _selected == null) return;
        if (double.TryParse(PropW.Text, out var v) && v > 0) { _selected.Width = v; ApplySelectedToCanvas(); }
    }

    private void PropH_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingPanel || _selected == null) return;
        if (double.TryParse(PropH.Text, out var v) && v > 0) { _selected.Height = v; ApplySelectedToCanvas(); }
    }

    private void PropOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingPanel || _selected == null) return;
        _selected.Opacity     = e.NewValue;
        PropOpacityLabel.Text = e.NewValue.ToString("F2");
        ApplySelectedToCanvas();
    }

    private void PropFontSize_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingPanel || _selected == null) return;
        if (double.TryParse(PropFontSize.Text, out var v) && v > 0) { _selected.FontSize = v; ApplySelectedToCanvas(); }
    }

    private void PropFontColor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingPanel || _selected == null) return;
        _selected.FontColor = PropFontColor.Text.Trim();
        ApplySelectedToCanvas();
    }

    private void PropText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingPanel || _selected == null) return;
        _selected.Text = PropText.Text;
        ApplySelectedToCanvas();
    }

    private void PropBinding_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_updatingPanel || _selected == null) return;
        _selected.DataBinding = PropBinding.Text.Trim();
        ApplySelectedToCanvas();
    }

    private void BgColorBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _theme.Background = BgColorBox.Text.Trim();
        ApplyCanvasBackground();
        _dirty = true;
    }
}
