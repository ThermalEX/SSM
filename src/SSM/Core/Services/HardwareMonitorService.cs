using LibreHardwareMonitor.Hardware;
using SSM.Core.Interfaces;
using SSM.Core.Models;

namespace SSM.Core.Services;

public class HardwareMonitorService : IHardwareMonitorService
{
    private readonly Computer _computer;
    private System.Timers.Timer? _timer;

    public HardwareData CurrentData { get; private set; } = new();
    public event EventHandler<HardwareData>? DataUpdated;

    public HardwareMonitorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsNetworkEnabled = true,
        };
        _computer.Open();
    }

    public void Start(int intervalMs = 1000)
    {
        _timer?.Dispose();
        _timer = new System.Timers.Timer(intervalMs);
        _timer.Elapsed += (_, _) => Refresh();
        _timer.AutoReset = true;
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    private void Refresh()
    {
        _computer.Accept(new UpdateVisitor());

        var data = new HardwareData { Timestamp = DateTime.Now };

        foreach (var hw in _computer.Hardware)
        {
            switch (hw.HardwareType)
            {
                case HardwareType.Cpu:
                    ReadCpu(hw, data.Cpu);
                    break;

                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    ReadGpu(hw, data.Gpu);
                    break;

                case HardwareType.Memory:
                    ReadMemory(hw, data.Memory);
                    break;

                case HardwareType.Network:
                    ReadNetwork(hw, data.Network);
                    break;
            }
        }

        CurrentData = data;
        DataUpdated?.Invoke(this, data);
    }

    private static void ReadCpu(IHardware hw, CpuData cpu)
    {
        ISensor? tempSensor = null;
        ISensor? loadSensor = null;

        foreach (var s in hw.Sensors)
        {
            if (s.SensorType == SensorType.Temperature)
            {
                if (tempSensor is null || s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                    tempSensor = s;
            }
            else if (s.SensorType == SensorType.Load)
            {
                if (loadSensor is null || s.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))
                    loadSensor = s;
            }
            else if (s.SensorType == SensorType.Fan)
            {
                cpu.FanSpeed = s.Value ?? 0;
            }
        }

        cpu.Temperature = tempSensor?.Value ?? 0;
        cpu.Load = loadSensor?.Value ?? 0;

        var coreLoads = hw.Sensors
            .Where(s => s.SensorType == SensorType.Load && s.Name.StartsWith("CPU Core", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Value ?? 0)
            .ToArray();
        if (coreLoads.Length > 0)
            cpu.CoreLoads = coreLoads;
    }

    private static void ReadGpu(IHardware hw, GpuData gpu)
    {
        foreach (var s in hw.Sensors)
        {
            switch (s.SensorType)
            {
                case SensorType.Temperature when gpu.Temperature == 0:
                    gpu.Temperature = s.Value ?? 0;
                    break;

                case SensorType.Load when s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase):
                    gpu.Load = s.Value ?? 0;
                    break;

                case SensorType.SmallData when s.Name.Contains("Memory Used", StringComparison.OrdinalIgnoreCase):
                    gpu.MemoryUsed = s.Value ?? 0;
                    break;

                case SensorType.SmallData when s.Name.Contains("Memory Total", StringComparison.OrdinalIgnoreCase):
                    gpu.MemoryTotal = s.Value ?? 0;
                    break;

                case SensorType.Fan when gpu.FanSpeed == 0:
                    gpu.FanSpeed = s.Value ?? 0;
                    break;
            }
        }
    }

    private static void ReadMemory(IHardware hw, MemoryData memory)
    {
        foreach (var s in hw.Sensors)
        {
            if (s.SensorType != SensorType.Data) continue;

            if (s.Name.Contains("Used", StringComparison.OrdinalIgnoreCase))
                memory.Used = s.Value ?? 0;
            else if (s.Name.Contains("Available", StringComparison.OrdinalIgnoreCase))
                memory.Available = s.Value ?? 0;
        }
        memory.Total = memory.Used + memory.Available;
    }

    private static void ReadNetwork(IHardware hw, NetworkData network)
    {
        foreach (var s in hw.Sensors)
        {
            if (s.SensorType != SensorType.Throughput) continue;

            if (s.Name.Contains("Upload", StringComparison.OrdinalIgnoreCase))
                network.UploadSpeed += s.Value ?? 0;
            else if (s.Name.Contains("Download", StringComparison.OrdinalIgnoreCase))
                network.DownloadSpeed += s.Value ?? 0;
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _computer.Close();
        GC.SuppressFinalize(this);
    }
}

internal class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) => computer.Traverse(this);

    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (var sub in hardware.SubHardware)
            sub.Accept(this);
    }

    public void VisitSensor(ISensor sensor) { }
    public void VisitParameter(IParameter parameter) { }
}
