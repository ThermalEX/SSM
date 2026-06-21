using System.Windows;
using System.Windows.Media.Animation;
using SSM.Core.Services;
using SSM.Views;

namespace SSM;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsService = new SettingsService();
        settingsService.Load();

        SwitchTheme(settingsService.Settings.ThemeName);

        var mainWindow = new MainWindow(settingsService);
        mainWindow.Show();
    }

    public static void SwitchTheme(string name)
    {
        var content = Current.MainWindow?.Content as UIElement;
        if (content is null)
        {
            ApplyTheme(name);
            return;
        }

        var easeIn  = new CubicEase { EasingMode = EasingMode.EaseIn };
        var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };

        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180))
            { EasingFunction = easeIn };
        fadeOut.Completed += (_, _) =>
        {
            ApplyTheme(name);
            content.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280))
                    { EasingFunction = easeOut });
        };
        content.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    private static void ApplyTheme(string name)
    {
        var dicts = Current.Resources.MergedDictionaries;
        dicts.Clear();
        var uri = name == "Light"
            ? new Uri("Themes/LightTheme.xaml", UriKind.Relative)
            : new Uri("Themes/DarkTheme.xaml", UriKind.Relative);
        dicts.Add(new ResourceDictionary { Source = uri });
    }
}
