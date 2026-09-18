namespace SchoolNetAutoAuth.App.Uninstallation;

public static class UninstallCommand
{
    public static bool TryParse(IEnumerable<string> arguments, out bool quiet)
    {
        var values = arguments.ToArray();
        var requested = values.Contains("--uninstall", StringComparer.OrdinalIgnoreCase);
        quiet = requested && values.Contains("--quiet", StringComparer.OrdinalIgnoreCase);
        return requested;
    }
}
