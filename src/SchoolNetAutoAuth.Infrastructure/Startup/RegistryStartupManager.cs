using Microsoft.Win32;

namespace SchoolNetAutoAuth.Infrastructure.Startup;

public sealed class RegistryStartupManager
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CampusNetworkAutoAuth";

    public void SetEnabled(bool enabled, string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (enabled) key.SetValue(ValueName, $"\"{executablePath}\" --background");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
