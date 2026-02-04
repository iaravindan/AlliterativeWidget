using Microsoft.Win32;

namespace AlliterativeWidget.Helpers;

public static class StartupManager
{
    private const string RegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "AlliterativeWidget";

    public static void RegisterStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
            if (key == null) return;

            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            key.SetValue(AppName, $"\"{exePath}\"");
        }
        catch
        {
            // Silently fail - startup registration is non-critical
        }
    }

    public static void UnregisterStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
            key?.DeleteValue(AppName, false);
        }
        catch
        {
            // Silently fail
        }
    }

    public static bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }
}
