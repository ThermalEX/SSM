using SSM.Core.Interfaces;
using SSM.Core.Models;

namespace SSM.Core.Services;

// TODO: 实现 LibreHardwareMonitor 集成，需管理员权限
public class HardwareMonitorService : IHardwareMonitorService
{
    private System.Timers.Timer? _timer;

    public HardwareData CurrentData { get; private set; } = new();
    public event EventHandler<HardwareData>? DataUpdated;

    public void Start(int intervalMs = 1000)
    {
        _timer = new System.Timers.Timer(intervalMs);
        _timer.Elapsed += (_, _) => Refresh();
        _timer.AutoReset = true;
        _timer.Start();
    }

    public void Stop() => _timer?.Stop();

    private void Refresh()
    {
        // TODO: 从 LibreHardwareMonitor 读取传感器数据
        DataUpdated?.Invoke(this, CurrentData);
    }

    public void Dispose()
    {
        _timer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
