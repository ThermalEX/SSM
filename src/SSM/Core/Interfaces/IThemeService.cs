using SSM.Core.Models;

namespace SSM.Core.Interfaces;

public interface IThemeService
{
    ThemeConfig CurrentTheme { get; }
    IReadOnlyList<string> AvailableThemes { get; }
    void LoadTheme(string path);
    void SaveTheme(ThemeConfig theme, string path);
}
