namespace SSM.Core.Models;

public class HardwareData
{
    public CpuData Cpu { get; set; } = new();
    public GpuData Gpu { get; set; } = new();
    public MemoryData Memory { get; set; } = new();
    public NetworkData Network { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class CpuData
{
    public float Temperature { get; set; }
    public float Load { get; set; }
    public float[] CoreLoads { get; set; } = [];
    public float FanSpeed { get; set; }
    public float Clock { get; set; }  // MHz
}

public class GpuData
{
    public float Temperature { get; set; }
    public float Load { get; set; }
    public float MemoryUsed { get; set; }
    public float MemoryTotal { get; set; }
    public float FanSpeed { get; set; }
    public float Clock { get; set; }  // MHz
}

public class MemoryData
{
    public float Used { get; set; }
    public float Available { get; set; }
    public float Total { get; set; }
    public float UsagePercent => Total > 0 ? Used / Total * 100 : 0;
}

public class NetworkData
{
    public float UploadSpeed { get; set; }
    public float DownloadSpeed { get; set; }
}
