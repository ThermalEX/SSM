using SSM.Core.Models;

namespace SSM.Core.Helpers;

public static class UnitConverter
{
    public static string FormatTemperature(float celsius, TemperatureUnit unit) =>
        unit == TemperatureUnit.Fahrenheit
            ? $"{celsius * 9f / 5f + 32f:F0} °F"
            : $"{celsius:F0} °C";

    public static string FormatMemory(float gb, MemoryUnit unit) =>
        unit == MemoryUnit.MB
            ? $"{gb * 1024f:F0} MB"
            : $"{gb:F1} GB";

    public static string FormatNetworkSpeed(float bps, NetworkSpeedUnit unit)
    {
        return unit switch
        {
            NetworkSpeedUnit.Bps => $"{bps:F0} B/s",
            NetworkSpeedUnit.KBps => $"{bps / 1024f:F1} KB/s",
            NetworkSpeedUnit.MBps => $"{bps / 1048576f:F2} MB/s",
            _ => bps switch   // Auto
            {
                < 1024f => $"{bps:F0} B/s",
                < 1048576f => $"{bps / 1024f:F1} KB/s",
                _ => $"{bps / 1048576f:F2} MB/s",
            },
        };
    }
}
