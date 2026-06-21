using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSM.Core.Interfaces;
using SSM.Helpers;

namespace SSM.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty] private int _targetScreenIndex;
    [ObservableProperty] private int _refreshIntervalMs;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private string _activeThemePath = string.Empty;

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadFromSettings();
    }

    [RelayCommand]
    private void Save()
    {
        _settingsService.Settings.TargetScreenIndex = TargetScreenIndex;
        _settingsService.Settings.RefreshIntervalMs = RefreshIntervalMs;
        _settingsService.Settings.StartWithWindows = StartWithWindows;
        _settingsService.Settings.ActiveThemePath = ActiveThemePath;
        _settingsService.Save();

        if (StartWithWindows)
            StartupHelper.EnableStartWithWindows(Environment.ProcessPath ?? string.Empty);
        else
            StartupHelper.DisableStartWithWindows();
    }

    private void LoadFromSettings()
    {
        var s = _settingsService.Settings;
        TargetScreenIndex = s.TargetScreenIndex;
        RefreshIntervalMs = s.RefreshIntervalMs;
        StartWithWindows = s.StartWithWindows;
        ActiveThemePath = s.ActiveThemePath;
    }
}
