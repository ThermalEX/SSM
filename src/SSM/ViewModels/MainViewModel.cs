using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SSM.Core.Interfaces;
using SSM.Core.Models;

namespace SSM.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IHardwareMonitorService _monitor;
    private readonly ISettingsService _settings;

    [ObservableProperty]
    private HardwareData _hardwareData = new();

    [ObservableProperty]
    private bool _isOverlayRunning;

    public MainViewModel(IHardwareMonitorService monitor, ISettingsService settings)
    {
        _monitor = monitor;
        _settings = settings;
        _monitor.DataUpdated += OnDataUpdated;
    }

    [RelayCommand]
    private void StartOverlay()
    {
        _monitor.Start(_settings.Settings.RefreshIntervalMs);
        IsOverlayRunning = true;
    }

    [RelayCommand]
    private void StopOverlay()
    {
        _monitor.Stop();
        IsOverlayRunning = false;
    }

    private void OnDataUpdated(object? sender, HardwareData data)
    {
        App.Current.Dispatcher.Invoke(() => HardwareData = data);
    }
}
