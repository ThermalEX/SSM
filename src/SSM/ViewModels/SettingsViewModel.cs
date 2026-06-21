using CommunityToolkit.Mvvm.ComponentModel;
using SSM.Core.Interfaces;
using SSM.Helpers;

namespace SSM.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private bool _loading;

    [ObservableProperty] private int _targetScreenIndex;
    [ObservableProperty] private int _refreshIntervalMs;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private string _activeThemePath = string.Empty;
    [ObservableProperty] private string _themeName = "Dark";
    [ObservableProperty] private int _overlayAngle = 0;
    [ObservableProperty] private string _hotkeyDisplay = "Ctrl + Shift + S";
    [ObservableProperty] private uint _hotkeyModifiers = 6;
    [ObservableProperty] private uint _hotkeyVirtualKey = 0x53;

    public event Action? HotkeyChanged;

    public bool IsDarkTheme
    {
        get => ThemeName == "Dark";
        set { if (value) ThemeName = "Dark"; OnPropertyChanged(); OnPropertyChanged(nameof(IsLightTheme)); }
    }

    public bool IsLightTheme
    {
        get => ThemeName == "Light";
        set { if (value) ThemeName = "Light"; OnPropertyChanged(); OnPropertyChanged(nameof(IsDarkTheme)); }
    }

    public bool IsAngle0   { get => OverlayAngle == 0;   set { if (value) OverlayAngle = 0;   OnPropertyChanged(); } }
    public bool IsAngle90  { get => OverlayAngle == 90;  set { if (value) OverlayAngle = 90;  OnPropertyChanged(); } }
    public bool IsAngle180 { get => OverlayAngle == 180; set { if (value) OverlayAngle = 180; OnPropertyChanged(); } }
    public bool IsAngle270 { get => OverlayAngle == 270; set { if (value) OverlayAngle = 270; OnPropertyChanged(); } }

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadFromSettings();
    }

    partial void OnThemeNameChanged(string value)
    {
        App.SwitchTheme(value);
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        if (!_loading) AutoSave();
    }

    partial void OnOverlayAngleChanged(int value)
    {
        OnPropertyChanged(nameof(IsAngle0));
        OnPropertyChanged(nameof(IsAngle90));
        OnPropertyChanged(nameof(IsAngle180));
        OnPropertyChanged(nameof(IsAngle270));
        if (!_loading) AutoSave();
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading) return;
        if (value)
            StartupHelper.EnableStartWithWindows(Environment.ProcessPath ?? string.Empty);
        else
            StartupHelper.DisableStartWithWindows();
        AutoSave();
    }

    partial void OnRefreshIntervalMsChanged(int value)
    {
        if (!_loading) AutoSave();
    }

    public void UpdateHotkey(uint modifiers, uint vk, string display)
    {
        _loading = true;
        HotkeyModifiers   = modifiers;
        HotkeyVirtualKey  = vk;
        HotkeyDisplay     = display;
        _loading = false;
        AutoSave();
        HotkeyChanged?.Invoke();
    }

    private void AutoSave()
    {
        var s = _settingsService.Settings;
        s.TargetScreenIndex  = TargetScreenIndex;
        s.RefreshIntervalMs  = RefreshIntervalMs;
        s.StartWithWindows   = StartWithWindows;
        s.ActiveThemePath    = ActiveThemePath;
        s.ThemeName          = ThemeName;
        s.OverlayAngle       = OverlayAngle;
        s.HotkeyDisplay      = HotkeyDisplay;
        s.HotkeyModifiers    = HotkeyModifiers;
        s.HotkeyVirtualKey   = HotkeyVirtualKey;
        _settingsService.Save();
    }

    private void LoadFromSettings()
    {
        _loading = true;
        var s = _settingsService.Settings;
        TargetScreenIndex = s.TargetScreenIndex;
        RefreshIntervalMs = s.RefreshIntervalMs;
        StartWithWindows  = s.StartWithWindows;
        ActiveThemePath   = s.ActiveThemePath;
        ThemeName         = s.ThemeName;
        OverlayAngle      = s.OverlayAngle;
        HotkeyDisplay     = s.HotkeyDisplay;
        HotkeyModifiers   = s.HotkeyModifiers;
        HotkeyVirtualKey  = s.HotkeyVirtualKey;
        _loading = false;
    }
}
