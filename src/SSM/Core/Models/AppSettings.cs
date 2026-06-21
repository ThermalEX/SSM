namespace SSM.Core.Models;

public class AppSettings
{
    public int TargetScreenIndex { get; set; } = 1;
    public int RefreshIntervalMs { get; set; } = 1000;
    public string ActiveThemePath { get; set; } = "Assets/Themes/default.json";
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool ShowInTaskbar { get; set; } = true;
}
