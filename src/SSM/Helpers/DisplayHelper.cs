using System.Windows;
using System.Windows.Forms;

namespace SSM.Helpers;

public static class DisplayHelper
{
    public static IReadOnlyList<Screen> GetAllScreens() =>
        Screen.AllScreens.ToList().AsReadOnly();

    public static Screen? GetScreenByIndex(int index)
    {
        var screens = Screen.AllScreens;
        return index >= 0 && index < screens.Length ? screens[index] : null;
    }

    public static Rect GetScreenBounds(Screen screen) =>
        new(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height);
}
