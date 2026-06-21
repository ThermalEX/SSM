using CommunityToolkit.Mvvm.ComponentModel;
using SSM.Core.Models;

namespace SSM.ViewModels;

public partial class OverlayViewModel : ObservableObject
{
    [ObservableProperty] private float _cpuTemperature;
    [ObservableProperty] private float _cpuLoad;
    [ObservableProperty] private float _gpuTemperature;
    [ObservableProperty] private float _gpuLoad;
    [ObservableProperty] private float _memoryUsagePercent;
    [ObservableProperty] private float _networkUpload;
    [ObservableProperty] private float _networkDownload;

    public void Update(HardwareData data)
    {
        CpuTemperature = data.Cpu.Temperature;
        CpuLoad = data.Cpu.Load;
        GpuTemperature = data.Gpu.Temperature;
        GpuLoad = data.Gpu.Load;
        MemoryUsagePercent = data.Memory.UsagePercent;
        NetworkUpload = data.Network.UploadSpeed;
        NetworkDownload = data.Network.DownloadSpeed;
    }
}
