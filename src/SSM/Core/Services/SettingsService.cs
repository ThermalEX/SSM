using System.IO;
using System.Text.Json;
using SSM.Core.Interfaces;
using SSM.Core.Models;

namespace SSM.Core.Services;

public class SettingsService : ISettingsService
{
    private const string SettingsPath = "Assets/settings.json";

    public AppSettings Settings { get; private set; } = new();

    public void Load()
    {
        if (!File.Exists(SettingsPath)) return;
        var json = File.ReadAllText(SettingsPath);
        Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
