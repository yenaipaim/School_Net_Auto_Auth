namespace SchoolNetAutoAuth.Installer;

public sealed record InstallRegistration(string UninstallString, string QuietUninstallString)
{
    public static InstallRegistration Create(string applicationPath) => new(
        $"\"{applicationPath}\" --uninstall",
        $"\"{applicationPath}\" --uninstall --quiet");
}
