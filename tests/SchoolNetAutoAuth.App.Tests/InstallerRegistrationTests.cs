using SchoolNetAutoAuth.Installer;

namespace SchoolNetAutoAuth.App.Tests;

public sealed class InstallerRegistrationTests
{
    [Fact]
    public void Create_UsesMainApplicationForInteractiveAndQuietUninstall()
    {
        var values = InstallRegistration.Create(@"C:\Apps\SchoolNetAutoAuth.App.exe");

        Assert.Equal("\"C:\\Apps\\SchoolNetAutoAuth.App.exe\" --uninstall", values.UninstallString);
        Assert.Equal("\"C:\\Apps\\SchoolNetAutoAuth.App.exe\" --uninstall --quiet", values.QuietUninstallString);
        Assert.DoesNotContain("Uninstall.exe", values.UninstallString, StringComparison.OrdinalIgnoreCase);
    }
}
