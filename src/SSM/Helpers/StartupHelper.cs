using Microsoft.Win32;

namespace SSM.Helpers;

public static class StartupHelper
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "SSM";

    public static void EnableStartWithWindows(string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.SetValue(AppName, $"\"{exePath}\"");
    }

    public static void DisableStartWithWindows()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }

    public static bool IsStartWithWindowsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppName) is not null;
    }
}
