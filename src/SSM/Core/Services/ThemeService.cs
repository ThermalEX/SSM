using System.IO;
using System.Text.Json;
using SSM.Core.Interfaces;
using SSM.Core.Models;

namespace SSM.Core.Services;

public class ThemeService : IThemeService
{
    private const string ThemesDir = "Assets/Themes";

    public ThemeConfig CurrentTheme { get; private set; } = new();
    public IReadOnlyList<string> AvailableThemes
    {
        get
        {
            Directory.CreateDirectory(ThemesDir);
            return Directory.GetFiles(ThemesDir, "*.json").ToList().AsReadOnly();
        }
    }

    public void LoadTheme(string path)
    {
        if (!File.Exists(path)) return;
        var json = File.ReadAllText(path);
        CurrentTheme = JsonSerializer.Deserialize<ThemeConfig>(json) ?? new ThemeConfig();
    }

    public void SaveTheme(ThemeConfig theme, string path)
    {
        var json = JsonSerializer.Serialize(theme, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
