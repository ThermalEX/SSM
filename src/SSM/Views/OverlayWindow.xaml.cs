using System.Windows;

namespace SSM.Views;

public partial class OverlayWindow : Window
{
    public OverlayWindow(int screenIndex)
    {
        InitializeComponent();
        PositionOnScreen(screenIndex);
    }

    private void PositionOnScreen(int index)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var screen = screens.Length > index ? screens[index] : screens[0];
        var bounds = screen.Bounds;

        Left   = bounds.Left;
        Top    = bounds.Top;
        Width  = bounds.Width;
        Height = bounds.Height;
    }
}
