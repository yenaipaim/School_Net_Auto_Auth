using System.Diagnostics;

namespace SchoolNetAutoAuth.Installer;

public static class InstallVersionPolicy
{
    public static string ResolveApplicationVersion(string executablePath)
    {
        var versionInfo = FileVersionInfo.GetVersionInfo(executablePath);
        var value = versionInfo.ProductVersion ?? versionInfo.FileVersion;
        return string.IsNullOrWhiteSpace(value) ? "0.0.0" : value.Trim();
    }

    public static bool IsDowngrade(string? installedVersion, string? incomingVersion)
    {
        var installed = ParseVersion(installedVersion);
        var incoming = ParseVersion(incomingVersion);
        return installed is not null && incoming is not null && incoming < installed;
    }

    private static Version? ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Split('+', 2)[0].Split('-', 2)[0].Trim();
        var parts = normalized.Split('.');
        if (parts.Length is < 1 or > 4) return null;

        var numbers = new int[4];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!int.TryParse(parts[index], out numbers[index]) || numbers[index] < 0)
                return null;
        }
        return new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
    }
}
