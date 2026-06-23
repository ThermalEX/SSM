using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using SSM.Core.Helpers;
using SSM.Core.Interfaces;
using SSM.Core.Models;
using SSM.ViewModels;
using SSM.Views.Pages;
using Brush = System.Windows.Media.Brush;
using Cursors = System.Windows.Input.Cursors;
using DragDropEffects = System.Windows.DragDropEffects;
using Orientation = System.Windows.Controls.Orientation;
using ProgressBar = System.Windows.Controls.ProgressBar;
using Button = System.Windows.Controls.Button;
using Control = System.Windows.Controls.Control;
using DragEventArgs = System.Windows.DragEventArgs;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace SSM.Views;

public partial class MainWindow : Window
{
    private OverlayWindow? _overlay;
    private bool _isOverlayRunning;
    private readonly SettingsPage _settingsPage;
    private readonly ThemePage _themePage;
    private readonly EditorPage _editorPage;
    private readonly SettingsViewModel _settingsVm;
    private readonly ISettingsService _settingsService;
    private readonly IHardwareMonitorService _monitor;
    private string _currentNav = "dashboard";

    // ── Dashboard customization ──
    private readonly List<string> _widgetOrder = new();
    private readonly HashSet<string> _hiddenWidgets = new();
    private bool _editMode;

    // Card cache: built once per edit-mode state, reused during drag
    private readonly Dictionary<string, Border> _cardCache = new();
    private bool _cardCacheIsEditMode = false;
    private Border? _placeholderCard;

    // Drag state
    private System.Windows.Point _dragStartPoint;
    private string? _draggingId;
    private int _dragInsertIdx = -1;

    private readonly Dictionary<string, Action<HardwareData, AppSettings>> _widgetUpdaters = new();
    private Ellipse? _statusDot;
    private TextBlock? _statusText;

    private static readonly string[] AllWidgetIds =
        ["cpu_temp", "cpu_load", "gpu_temp", "gpu_load", "ram", "net_up", "net_down", "status"];

    private static readonly Dictionary<string, string> WidgetLabels = new()
    {
        ["cpu_temp"] = "CPU 温度",
        ["cpu_load"] = "CPU 负载",
        ["gpu_temp"] = "GPU 温度",
        ["gpu_load"] = "GPU 负载",
        ["ram"]      = "RAM",
        ["net_up"]   = "网络 ↑",
        ["net_down"] = "网络 ↓",
        ["status"]   = "状态",
    };

    // ── Win32 / hotkey ──
    private static readonly System.Windows.Media.Geometry _iconPlay =
        System.Windows.Media.Geometry.Parse("M8,5.14V19.14L19,12.14L8,5.14Z");
    private static readonly System.Windows.Media.Geometry _iconStop =
        System.Windows.Media.Geometry.Parse("M18,18H6V6H18V18Z");

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint attr, ref int value, uint size);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const uint DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;
    private const int HOTKEY_STOP_OVERLAY = 9001;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var pref = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, 4);
        HwndSource.FromHwnd(hwnd).AddHook(WndProc);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_isOverlayRunning)
            UnregisterHotKey(new WindowInteropHelper(this).Handle, HOTKEY_STOP_OVERLAY);
        _monitor.DataUpdated -= OnDataUpdated;
        _monitor.Stop();
        _monitor.Dispose();
        base.OnClosed(e);
    }

    private void OnDataUpdated(object? sender, HardwareData data)
    {
        Dispatcher.Invoke(() => UpdateDashboard(data));
    }

    private void UpdateDashboard(HardwareData data)
    {
        var s = _settingsService.Settings;
        foreach (var u in _widgetUpdaters.Values)
            u(data, s);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_STOP_OVERLAY)
        {
            StopOverlay();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public MainWindow(ISettingsService settingsService, IHardwareMonitorService monitor)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _monitor = monitor;
        _settingsVm = new SettingsViewModel(settingsService);
        _settingsPage = new SettingsPage(_settingsVm);
        _themePage = new ThemePage();
        _editorPage = new EditorPage();
        SettingsView.Content = _settingsPage;
        ThemeView.Content = _themePage;
        EditorView.Content = _editorPage;
        PopulateScreenList();

        _settingsVm.HotkeyChanged += () =>
        {
            if (!_isOverlayRunning) return;
            var hwnd = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(hwnd, HOTKEY_STOP_OVERLAY);
            RegisterHotKey(hwnd, HOTKEY_STOP_OVERLAY,
                _settingsVm.HotkeyModifiers, _settingsVm.HotkeyVirtualKey);
        };

        _settingsVm.RefreshIntervalChanged += interval => _monitor.Start(interval);

        _monitor.DataUpdated += OnDataUpdated;
        _monitor.Start(settingsService.Settings.RefreshIntervalMs);

        // Wire grid-level drag events once
        DashboardGrid.AllowDrop = true;
        DashboardGrid.DragOver  += OnGridDragOver;
        DashboardGrid.DragLeave += OnGridDragLeave;
        DashboardGrid.Drop      += OnGridDrop;

        InitDashboard();
    }

    // ────────────────────────────────────────────────
    //  Dashboard init / rebuild
    // ────────────────────────────────────────────────

    private void InitDashboard()
    {
        var saved = _settingsService.Settings.DashboardWidgets;

        if (saved.Count == 0)
        {
            foreach (var id in AllWidgetIds)
            {
                _widgetOrder.Add(id);
                saved.Add(new DashboardWidgetConfig { Id = id, IsVisible = true });
            }
            _settingsService.Save();
        }
        else
        {
            foreach (var cfg in saved)
            {
                _widgetOrder.Add(cfg.Id);
                if (!cfg.IsVisible)
                    _hiddenWidgets.Add(cfg.Id);
            }
            foreach (var id in AllWidgetIds)
            {
                if (!_widgetOrder.Contains(id))
                {
                    _widgetOrder.Add(id);
                    saved.Add(new DashboardWidgetConfig { Id = id, IsVisible = true });
                }
            }
        }

        RebuildDashboard();
    }

    private void RebuildDashboard()
    {
        // Invalidate cache when edit mode changes (cards look different)
        if (_cardCacheIsEditMode != _editMode)
        {
            _cardCache.Clear();
            _widgetUpdaters.Clear();
            _statusDot = null;
            _statusText = null;
            _cardCacheIsEditMode = _editMode;
        }

        // Build any missing cards
        foreach (var id in AllWidgetIds)
        {
            if (!_cardCache.ContainsKey(id))
                _cardCache[id] = BuildCard(id);
        }

        // Reset opacity / transforms in case a drag was interrupted
        foreach (var card in _cardCache.Values)
        {
            card.Opacity = 1.0;
            card.RenderTransform = Transform.Identity;
        }

        RefreshLayout();
        RebuildHiddenPanel();

        // Sync status card with overlay state
        SyncStatusCard();
    }

    // Fast rearrangement — only touches DashboardGrid.Children, never recreates controls
    private void RefreshLayout(string? ghostId = null, int insertIdx = -1)
    {
        DashboardGrid.Children.Clear();

        var visibleIds = _widgetOrder.Where(id => !_hiddenWidgets.Contains(id)).ToList();
        var displayList = ghostId != null
            ? visibleIds.Where(id => id != ghostId).ToList()
            : visibleIds;

        int cols = displayList.Count <= 2 ? 2 : displayList.Count <= 3 ? 3 : 4;
        DashboardGrid.Columns = cols;

        for (int i = 0; i < displayList.Count; i++)
        {
            if (i == insertIdx)
                DashboardGrid.Children.Add(GetPlaceholder());

            if (_cardCache.TryGetValue(displayList[i], out var card))
                DashboardGrid.Children.Add(card);
        }

        // Placeholder at tail
        if (insertIdx >= displayList.Count && insertIdx >= 0)
            DashboardGrid.Children.Add(GetPlaceholder());
    }

    private void RebuildHiddenPanel()
    {
        HiddenWidgetsPanel.Children.Clear();
        if (_editMode && _hiddenWidgets.Count > 0)
        {
            HiddenWidgetsPanel.Visibility = Visibility.Visible;
            foreach (var id in _widgetOrder.Where(id => _hiddenWidgets.Contains(id)))
            {
                var capturedId = id;
                var chip = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(0, 0, 6, 6),
                    Cursor = Cursors.Hand,
                    BorderThickness = new Thickness(1),
                };
                chip.SetResourceReference(Border.BackgroundProperty, "BgCard");
                chip.SetResourceReference(Border.BorderBrushProperty, "Accent");
                var label = new TextBlock { FontSize = 10, Text = "+ " + WidgetLabels[id] };
                label.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
                chip.Child = label;
                chip.MouseLeftButtonUp += (_, _) => RestoreWidget(capturedId);
                HiddenWidgetsPanel.Children.Add(chip);
            }
        }
        else
        {
            HiddenWidgetsPanel.Visibility = Visibility.Collapsed;
        }
    }

    private Border GetPlaceholder()
    {
        if (_placeholderCard != null) return _placeholderCard;

        _placeholderCard = new Border { Margin = new Thickness(3, 0, 3, 8) };

        // Dashed rounded rectangle as visual
        var rect = new Rectangle
        {
            RadiusX = 9,
            RadiusY = 9,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection([6, 4]),
            Fill = System.Windows.Media.Brushes.Transparent,
            Stretch = Stretch.Fill,
            Opacity = 0.7,
            Margin = new Thickness(1),
            IsHitTestVisible = false,
        };
        rect.SetResourceReference(Rectangle.StrokeProperty, "Accent");
        _placeholderCard.Child = rect;
        return _placeholderCard;
    }

    // ────────────────────────────────────────────────
    //  Grid-level drag-and-drop (insert with live preview)
    // ────────────────────────────────────────────────

    private void OnGridDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(string)) is not string ghostId) return;
        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        // Make ghost card semi-transparent (idempotent)
        if (_cardCache.TryGetValue(ghostId, out var ghost))
            ghost.Opacity = 0.3;

        var pos = e.GetPosition(DashboardGrid);
        int newIdx = CalcInsertIdx(pos, ghostId);
        if (newIdx == _dragInsertIdx) return;

        _dragInsertIdx = newIdx;
        AnimateLayout(ghostId, newIdx);
    }

    private void OnGridDragLeave(object sender, DragEventArgs e)
    {
        // DragLeave fires between children too; only act when truly leaving the grid
        var pos = e.GetPosition(DashboardGrid);
        var bounds = new Rect(0, 0, DashboardGrid.ActualWidth, DashboardGrid.ActualHeight);
        if (bounds.Contains(pos)) return;

        _dragInsertIdx = -1;
        // Show all cards (ghost stays semi-transparent until drag ends)
        RefreshLayout();
    }

    private void OnGridDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(string)) is not string sourceId) return;

        var displayList = _widgetOrder
            .Where(id => !_hiddenWidgets.Contains(id) && id != sourceId)
            .ToList();

        int insertIdx = Math.Clamp(_dragInsertIdx, 0, displayList.Count);
        string? insertBeforeId = insertIdx < displayList.Count ? displayList[insertIdx] : null;

        _widgetOrder.Remove(sourceId);
        int targetIdx = insertBeforeId != null
            ? _widgetOrder.IndexOf(insertBeforeId)
            : _widgetOrder.Count;
        if (targetIdx < 0) targetIdx = _widgetOrder.Count;
        _widgetOrder.Insert(targetIdx, sourceId);

        _dragInsertIdx = -1;
        SaveWidgetConfig();
        RebuildDashboard();
        e.Handled = true;
    }

    private int CalcInsertIdx(System.Windows.Point pos, string ghostId)
    {
        var displayList = _widgetOrder
            .Where(id => !_hiddenWidgets.Contains(id) && id != ghostId)
            .ToList();

        int count = displayList.Count;
        if (count == 0) return 0;

        int cols = DashboardGrid.Columns > 0 ? DashboardGrid.Columns : 4;

        // Cell width from grid
        double cellW = DashboardGrid.ActualWidth / cols;
        if (cellW <= 0) return count;

        // Cell height: read from first non-placeholder child
        double cellH = 120;
        foreach (UIElement child in DashboardGrid.Children)
        {
            if (child is FrameworkElement fe && child != _placeholderCard && fe.ActualHeight > 10)
            {
                cellH = fe.ActualHeight;
                break;
            }
        }

        int col = Math.Clamp((int)(pos.X / cellW), 0, cols - 1);
        int row = Math.Max(0, (int)(pos.Y / cellH));
        int cellIdx = row * cols + col;

        // Insert before or after based on X position within cell
        double localX = pos.X - col * cellW;
        bool insertBefore = localX < cellW / 2;
        int insertIdx = insertBefore ? cellIdx : cellIdx + 1;

        return Math.Clamp(insertIdx, 0, count);
    }

    // FLIP animation: records old positions, applies new layout, then animates each card
    // from where it was to where it ended up — without blocking the UI thread.
    private void AnimateLayout(string? ghostId, int insertIdx)
    {
        int oldCols = DashboardGrid.Columns > 0 ? DashboardGrid.Columns : 4;
        double oldCellW = DashboardGrid.ActualWidth / oldCols;

        // Pre-read cell height from first real card (UniformGrid makes all cells equal)
        double cellH = 100;
        foreach (UIElement child in DashboardGrid.Children)
        {
            if (child != _placeholderCard && child is FrameworkElement fe && fe.ActualHeight > 10)
            { cellH = fe.ActualHeight; break; }
        }

        // Snapshot: index-in-children + current animated TX/TY for every visible card
        var oldState = new Dictionary<string, (int idx, double tx, double ty)>();
        int i = 0;
        foreach (UIElement child in DashboardGrid.Children)
        {
            if (child != _placeholderCard && child is FrameworkElement fe && fe.Tag is string id)
            {
                double tx = 0, ty = 0;
                if (fe.RenderTransform is TranslateTransform existTt)
                { tx = existTt.X; ty = existTt.Y; }
                oldState[id] = (i, tx, ty);
            }
            i++;
        }

        // Commit new layout (instant, no animation yet)
        RefreshLayout(ghostId, insertIdx);

        int newCols = DashboardGrid.Columns > 0 ? DashboardGrid.Columns : 4;
        double newCellW = DashboardGrid.ActualWidth / newCols;

        var ease = new System.Windows.Media.Animation.CubicEase
            { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
        var dur = new Duration(TimeSpan.FromMilliseconds(200));

        int newIdx = 0;
        foreach (UIElement child in DashboardGrid.Children)
        {
            if (child != _placeholderCard && child is FrameworkElement feNew && feNew.Tag is string cardId)
            {
                if (oldState.TryGetValue(cardId, out var s))
                {
                    // Where the card WAS rendering (old layout pos + mid-animation offset)
                    double oldRenderedX = (s.idx % oldCols) * oldCellW + s.tx;
                    double oldRenderedY = (s.idx / oldCols) * cellH  + s.ty;

                    // Where the card IS NOW in the new layout
                    double newLayoutX = (newIdx % newCols) * newCellW;
                    double newLayoutY = (newIdx / newCols) * cellH;

                    // Transform that makes the card appear at its old rendered position
                    double startX = oldRenderedX - newLayoutX;
                    double startY = oldRenderedY - newLayoutY;

                    if (Math.Abs(startX) > 1 || Math.Abs(startY) > 1)
                    {
                        // Start offset = old position, animate to 0 (new layout position)
                        var tt = new TranslateTransform(startX, startY);
                        feNew.RenderTransform = tt;
                        tt.BeginAnimation(TranslateTransform.XProperty,
                            new System.Windows.Media.Animation.DoubleAnimation(0, dur) { EasingFunction = ease });
                        tt.BeginAnimation(TranslateTransform.YProperty,
                            new System.Windows.Media.Animation.DoubleAnimation(0, dur) { EasingFunction = ease });
                    }
                    else
                    {
                        feNew.RenderTransform = Transform.Identity;
                    }
                }
            }
            newIdx++;
        }
    }

    // ────────────────────────────────────────────────
    //  Card building
    // ────────────────────────────────────────────────

    private Border BuildCard(string id)
    {
        var card = new Border();
        card.Style = (Style)FindResource("CardBorder");
        card.Margin = new Thickness(3, 0, 3, 8);
        card.Tag = id;
        var capturedId = id;

        var stack = new StackPanel();

        if (_editMode)
        {
            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var handle = new TextBlock
            {
                Text = "⠿",
                FontSize = 14,
                Cursor = Cursors.SizeAll,
                Margin = new Thickness(0, 0, 0, 4),
                VerticalAlignment = VerticalAlignment.Center,
            };
            handle.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            Grid.SetColumn(handle, 0);

            handle.PreviewMouseLeftButtonDown += (_, e) =>
            {
                _dragStartPoint = e.GetPosition(null);
                _draggingId = capturedId;
            };
            handle.PreviewMouseMove += (_, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed || _draggingId != capturedId) return;
                var pos = e.GetPosition(null);
                if (Math.Abs(pos.X - _dragStartPoint.X) <= SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(pos.Y - _dragStartPoint.Y) <= SystemParameters.MinimumVerticalDragDistance)
                    return;

                var result = DragDrop.DoDragDrop(handle, capturedId, DragDropEffects.Move);

                // Drag ended
                _draggingId = null;
                _dragInsertIdx = -1;
                if (result != DragDropEffects.Move)
                {
                    // Cancelled — restore opacity and layout
                    foreach (var c in _cardCache.Values) c.Opacity = 1.0;
                    RefreshLayout();
                }
            };

            var removeBtn = new Button
            {
                Content = "×",
                FontSize = 12,
                Width = 18,
                Height = 18,
                Padding = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                BorderThickness = new Thickness(0),
            };
            removeBtn.SetResourceReference(Control.BackgroundProperty, "BgCard");
            removeBtn.SetResourceReference(Control.ForegroundProperty, "TextSecondary");
            removeBtn.Click += (_, _) => RemoveWidget(capturedId);
            Grid.SetColumn(removeBtn, 1);

            topRow.Children.Add(handle);
            topRow.Children.Add(removeBtn);
            stack.Children.Add(topRow);
        }

        var title = new TextBlock
        {
            Text = WidgetLabels[id],
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6),
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        stack.Children.Add(title);

        BuildWidgetContent(id, stack);
        card.Child = stack;
        return card;
    }

    private void BuildWidgetContent(string id, StackPanel parent)
    {
        switch (id)
        {
            case "cpu_temp":
            {
                var val = MakeValueText("-- °C");
                parent.Children.Add(val);
                _widgetUpdaters[id] = (data, s) =>
                    val.Text = UnitConverter.FormatTemperature(data.Cpu.Temperature, s.TemperatureUnit);
                break;
            }
            case "cpu_load":
            {
                var val = MakeValueText("--%");
                val.Margin = new Thickness(0, 0, 0, 6);
                var bar = MakeProgressBar();
                parent.Children.Add(val);
                parent.Children.Add(bar);
                _widgetUpdaters[id] = (data, _) =>
                {
                    val.Text = $"{data.Cpu.Load:F0} %";
                    bar.Value = data.Cpu.Load;
                };
                break;
            }
            case "gpu_temp":
            {
                var val = MakeValueText("-- °C");
                parent.Children.Add(val);
                _widgetUpdaters[id] = (data, s) =>
                    val.Text = UnitConverter.FormatTemperature(data.Gpu.Temperature, s.TemperatureUnit);
                break;
            }
            case "gpu_load":
            {
                var val = MakeValueText("--%");
                val.Margin = new Thickness(0, 0, 0, 6);
                var bar = MakeProgressBar();
                parent.Children.Add(val);
                parent.Children.Add(bar);
                _widgetUpdaters[id] = (data, _) =>
                {
                    val.Text = $"{data.Gpu.Load:F0} %";
                    bar.Value = data.Gpu.Load;
                };
                break;
            }
            case "ram":
            {
                var val = MakeValueText("-- GB");
                val.Margin = new Thickness(0, 0, 0, 6);
                var bar = MakeProgressBar();
                bar.Margin = new Thickness(0, 0, 0, 4);
                var detail = new TextBlock { Text = "-- / -- GB", FontSize = 10 };
                detail.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
                parent.Children.Add(val);
                parent.Children.Add(bar);
                parent.Children.Add(detail);
                _widgetUpdaters[id] = (data, s) =>
                {
                    val.Text = UnitConverter.FormatMemory(data.Memory.Used, s.MemoryUnit);
                    bar.Value = data.Memory.UsagePercent;
                    detail.Text = $"{UnitConverter.FormatMemory(data.Memory.Used, s.MemoryUnit)} / {UnitConverter.FormatMemory(data.Memory.Total, s.MemoryUnit)}";
                };
                break;
            }
            case "net_up":
            {
                var val = new TextBlock { Text = "--", FontSize = 20, FontWeight = FontWeights.Light };
                val.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
                parent.Children.Add(val);
                _widgetUpdaters[id] = (data, s) =>
                    val.Text = UnitConverter.FormatNetworkSpeed(data.Network.UploadSpeed, s.NetworkSpeedUnit);
                break;
            }
            case "net_down":
            {
                var val = new TextBlock { Text = "--", FontSize = 20, FontWeight = FontWeights.Light };
                val.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
                parent.Children.Add(val);
                _widgetUpdaters[id] = (data, s) =>
                    val.Text = UnitConverter.FormatNetworkSpeed(data.Network.DownloadSpeed, s.NetworkSpeedUnit);
                break;
            }
            case "status":
            {
                var row = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var dot = new Ellipse { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center };
                dot.SetResourceReference(Ellipse.FillProperty, "TextSecondary");
                var text = new TextBlock
                {
                    Text = "未投放",
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0),
                };
                text.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
                row.Children.Add(dot);
                row.Children.Add(text);
                parent.Children.Add(row);
                _statusDot = dot;
                _statusText = text;
                break;
            }
        }
    }

    private static TextBlock MakeValueText(string placeholder)
    {
        var tb = new TextBlock
        {
            Text = placeholder,
            FontSize = 26,
            FontWeight = FontWeights.Light,
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
        return tb;
    }

    private ProgressBar MakeProgressBar()
    {
        var pb = new ProgressBar { Maximum = 100, Value = 0 };
        pb.Style = (Style)FindResource("TechProgressBar");
        return pb;
    }

    // ────────────────────────────────────────────────
    //  Edit mode
    // ────────────────────────────────────────────────

    private void EditLayout_Click(object sender, RoutedEventArgs e)
    {
        _editMode = !_editMode;
        EditLayoutText.Text = _editMode ? "完成" : "编辑";
        RebuildDashboard();
    }

    private void RemoveWidget(string id)
    {
        _hiddenWidgets.Add(id);
        SaveWidgetConfig();
        RebuildDashboard();
    }

    private void RestoreWidget(string id)
    {
        _hiddenWidgets.Remove(id);
        SaveWidgetConfig();
        RebuildDashboard();
    }

    private void SaveWidgetConfig()
    {
        var settings = _settingsService.Settings;
        settings.DashboardWidgets = _widgetOrder
            .Select(id => new DashboardWidgetConfig
            {
                Id = id,
                IsVisible = !_hiddenWidgets.Contains(id),
            })
            .ToList();
        _settingsService.Save();
    }

    private void SyncStatusCard()
    {
        if (_statusDot == null || _statusText == null) return;
        if (_isOverlayRunning)
        {
            _statusDot.Fill = (Brush)FindResource("OkBrush");
            _statusText.Text = "投放中";
        }
        else
        {
            _statusDot.Fill = (Brush)FindResource("TextSecondary");
            _statusText.Text = "未投放";
        }
    }

    // ────────────────────────────────────────────────
    //  Screen / overlay
    // ────────────────────────────────────────────────

    private void PopulateScreenList()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        ScreenSelector.Items.Clear();
        for (int i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            ScreenSelector.Items.Add($"显示器 {i + 1}  ({s.Bounds.Width}×{s.Bounds.Height})");
        }
        ScreenSelector.SelectedIndex = screens.Length > 1 ? 1 : 0;
    }

    private void RefreshScreens_Click(object sender, RoutedEventArgs e) => PopulateScreenList();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var tag = btn.Tag as string ?? "dashboard";
        if (tag == _currentNav) return;
        _currentNav = tag;

        DashboardView.Visibility = Vis(_currentNav == "dashboard");
        ThemeView.Visibility = Vis(_currentNav == "theme");
        EditorView.Visibility = Vis(_currentNav == "editor");
        SettingsView.Visibility = Vis(_currentNav == "settings");

        NavDashboardIndicator.Visibility = Vis(_currentNav == "dashboard");
        NavThemeIndicator.Visibility = Vis(_currentNav == "theme");
        NavEditorIndicator.Visibility = Vis(_currentNav == "editor");
        NavSettingsIndicator.Visibility = Vis(_currentNav == "settings");

        UpdateNavColors();
    }

    private void UpdateNavColors()
    {
        var accent = (Brush)FindResource("Accent");
        var secondary = (Brush)FindResource("TextSecondary");

        NavDashboardIcon.Fill = _currentNav == "dashboard" ? accent : secondary;
        NavDashboardLabel.Foreground = _currentNav == "dashboard" ? accent : secondary;
        NavThemeIcon.Fill = _currentNav == "theme" ? accent : secondary;
        NavThemeLabel.Foreground = _currentNav == "theme" ? accent : secondary;
        NavEditorIcon.Fill = _currentNav == "editor" ? accent : secondary;
        NavEditorLabel.Foreground = _currentNav == "editor" ? accent : secondary;
        NavSettingsIcon.Fill = _currentNav == "settings" ? accent : secondary;
        NavSettingsLabel.Foreground = _currentNav == "settings" ? accent : secondary;
    }

    private static Visibility Vis(bool visible) =>
        visible ? Visibility.Visible : Visibility.Collapsed;

    private void ToggleOverlay_Click(object sender, RoutedEventArgs e)
    {
        if (!_isOverlayRunning) StartOverlay();
        else StopOverlay();
    }

    private void StartOverlay()
    {
        var screenIndex = ScreenSelector.SelectedIndex;
        _overlay = new OverlayWindow(screenIndex, _settingsVm.OverlayAngle, _monitor, _settingsService);
        _overlay.Closed += OnOverlayClosed;
        _overlay.Show();
        _isOverlayRunning = true;
        OverlayBtnIcon.Data = _iconStop;
        OverlayBtnText.Text = "停止投放";
        if (_statusDot != null) _statusDot.Fill = (Brush)FindResource("OkBrush");
        if (_statusText != null) _statusText.Text = "投放中";
        TitleDot.Fill = (Brush)FindResource("OkBrush");

        var hwnd = new WindowInteropHelper(this).Handle;
        RegisterHotKey(hwnd, HOTKEY_STOP_OVERLAY,
            _settingsVm.HotkeyModifiers, _settingsVm.HotkeyVirtualKey);
    }

    private void StopOverlay() => _overlay?.Close();

    private void OnOverlayClosed(object? sender, EventArgs e)
    {
        UnregisterHotKey(new WindowInteropHelper(this).Handle, HOTKEY_STOP_OVERLAY);
        _overlay = null;
        _isOverlayRunning = false;
        OverlayBtnIcon.Data = _iconPlay;
        OverlayBtnText.Text = "开始投放";
        if (_statusDot != null) _statusDot.Fill = (Brush)FindResource("TextSecondary");
        if (_statusText != null) _statusText.Text = "未投放";
        TitleDot.Fill = (Brush)FindResource("TextSecondary");
    }
}
