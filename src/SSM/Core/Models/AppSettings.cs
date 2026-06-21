namespace SSM.Core.Models;

public class AppSettings
{
    public int TargetScreenIndex { get; set; } = 1;
    public int RefreshIntervalMs { get; set; } = 1000;
    public string ActiveThemePath { get; set; } = "Assets/Themes/default.json";
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool ShowInTaskbar { get; set; } = true;
    public string ThemeName { get; set; } = "Dark";
    public int OverlayAngle { get; set; } = 0;
    public string HotkeyDisplay { get; set; } = "Ctrl + Shift + S";
    public uint HotkeyModifiers { get; set; } = 6;   // MOD_CONTROL | MOD_SHIFT
    public uint HotkeyVirtualKey { get; set; } = 0x53; // 'S'
}
