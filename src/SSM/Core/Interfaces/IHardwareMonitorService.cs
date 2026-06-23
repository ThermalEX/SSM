using SSM.Core.Models;

namespace SSM.Core.Interfaces;

public interface IHardwareMonitorService : IDisposable
{
    HardwareData CurrentData { get; }
    event EventHandler<HardwareData> DataUpdated;
    string CpuName { get; }
    string GpuName { get; }
    void Start(int intervalMs = 1000);
    void Stop();
}
