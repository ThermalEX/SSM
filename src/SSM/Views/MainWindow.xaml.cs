using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using SSM.Core.Interfaces;
using SSM.ViewModels;
using SSM.Views.Pages;

namespace SSM.Views;

public partial class MainWindow : Window
{
    private OverlayWindow? _overlay;
    private bool _isOverlayRunning;
    private readonly SettingsPage _settingsPage;
    private readonly ThemePage    _themePage;
    private readonly EditorPage   _editorPage;
    private readonly SettingsViewModel _settingsVm;
    private string _currentNav = "dashboard";

    private static readonly System.Windows.Media.Geometry _iconPlay = System.Windows.Media.Geometry.Parse("M8,5.14V19.14L19,12.14L8,5.14Z");
    private static readonly System.Windows.Media.Geometry _iconStop = System.Windows.Media.Geometry.Parse("M18,18H6V6H18V18Z");

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint attr, ref int value, uint size);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const uint DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int  DWMWCP_ROUND        = 2;
    private const int  HOTKEY_STOP_OVERLAY = 9001;

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
        base.OnClosed(e);
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

    public MainWindow(ISettingsService settingsService)
    {
        InitializeComponent();
        _settingsVm = new SettingsViewModel(settingsService);
        _settingsPage = new SettingsPage(_settingsVm);
        _themePage    = new ThemePage();
        _editorPage   = new EditorPage();
        SettingsView.Content = _settingsPage;
        ThemeView.Content    = _themePage;
        EditorView.Content   = _editorPage;
        PopulateScreenList();

        _settingsVm.HotkeyChanged += () =>
        {
            if (!_isOverlayRunning) return;
            var hwnd = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(hwnd, HOTKEY_STOP_OVERLAY);
            RegisterHotKey(hwnd, HOTKEY_STOP_OVERLAY,
                _settingsVm.HotkeyModifiers, _settingsVm.HotkeyVirtualKey);
        };
    }

    private void PopulateScreenList()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            ScreenSelector.Items.Add($"显示器 {i + 1}  ({s.Bounds.Width}×{s.Bounds.Height})");
        }
        ScreenSelector.SelectedIndex = screens.Length > 1 ? 1 : 0;
    }

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
        if (sender is not System.Windows.Controls.Button btn) return;
        var tag = btn.Tag as string ?? "dashboard";
        if (tag == _currentNav) return;
        _currentNav = tag;

        DashboardView.Visibility = Vis(_currentNav == "dashboard");
        ThemeView.Visibility     = Vis(_currentNav == "theme");
        EditorView.Visibility    = Vis(_currentNav == "editor");
        SettingsView.Visibility  = Vis(_currentNav == "settings");

        NavDashboardIndicator.Visibility = Vis(_currentNav == "dashboard");
        NavThemeIndicator.Visibility     = Vis(_currentNav == "theme");
        NavEditorIndicator.Visibility    = Vis(_currentNav == "editor");
        NavSettingsIndicator.Visibility  = Vis(_currentNav == "settings");

        UpdateNavColors();
    }

    private void UpdateNavColors()
    {
        var accent    = (System.Windows.Media.Brush)FindResource("Accent");
        var secondary = (System.Windows.Media.Brush)FindResource("TextSecondary");

        NavDashboardIcon.Fill  = _currentNav == "dashboard" ? accent : secondary;
        NavDashboardLabel.Foreground = _currentNav == "dashboard" ? accent : secondary;
        NavThemeIcon.Fill      = _currentNav == "theme"     ? accent : secondary;
        NavThemeLabel.Foreground     = _currentNav == "theme"     ? accent : secondary;
        NavEditorIcon.Fill     = _currentNav == "editor"    ? accent : secondary;
        NavEditorLabel.Foreground    = _currentNav == "editor"    ? accent : secondary;
        NavSettingsIcon.Fill   = _currentNav == "settings"  ? accent : secondary;
        NavSettingsLabel.Foreground  = _currentNav == "settings"  ? accent : secondary;
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
        _overlay = new OverlayWindow(screenIndex, _settingsVm.OverlayAngle);
        _overlay.Closed += OnOverlayClosed;
        _overlay.Show();
        _isOverlayRunning = true;
        OverlayBtnIcon.Data = _iconStop;
        OverlayBtnText.Text = "停止投放";
        StatusDot.Fill  = (System.Windows.Media.Brush)FindResource("OkBrush");
        StatusText.Text = "投放中";
        TitleDot.Fill   = (System.Windows.Media.Brush)FindResource("OkBrush");

        var hwnd = new WindowInteropHelper(this).Handle;
        RegisterHotKey(hwnd, HOTKEY_STOP_OVERLAY,
            _settingsVm.HotkeyModifiers, _settingsVm.HotkeyVirtualKey);
    }

    private void StopOverlay() => _overlay?.Close();

    private void OnOverlayClosed(object? sender, EventArgs e)
    {
        UnregisterHotKey(new WindowInteropHelper(this).Handle, HOTKEY_STOP_OVERLAY);
        _overlay          = null;
        _isOverlayRunning = false;
        OverlayBtnIcon.Data = _iconPlay;
        OverlayBtnText.Text = "开始投放";
        StatusDot.Fill  = (System.Windows.Media.Brush)FindResource("TextSecondary");
        StatusText.Text = "未投放";
        TitleDot.Fill   = (System.Windows.Media.Brush)FindResource("DangerBrush");
    }
}
