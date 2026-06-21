using System.Windows;
using System.Windows.Input;

namespace SSM.Views;

public partial class MainWindow : Window
{
    private OverlayWindow? _overlay;
    private bool _isOverlayRunning;

    public MainWindow()
    {
        InitializeComponent();
        PopulateScreenList();
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
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        Close();

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: 页面导航
    }

    private void ToggleOverlay_Click(object sender, RoutedEventArgs e)
    {
        if (!_isOverlayRunning)
        {
            var screenIndex = ScreenSelector.SelectedIndex;
            _overlay = new OverlayWindow(screenIndex);
            _overlay.Show();
            _isOverlayRunning = true;
            ToggleOverlayButton.Content = "■  停止投放";
            StatusDot.Background = FindResource("OkBrush") as System.Windows.Media.Brush;
            StatusText.Text = "投放中";
        }
        else
        {
            _overlay?.Close();
            _overlay = null;
            _isOverlayRunning = false;
            ToggleOverlayButton.Content = "▶  开始投放";
            StatusDot.Background = FindResource("TextSecondary") as System.Windows.Media.Brush;
            StatusText.Text = "未投放";
        }
    }
}
