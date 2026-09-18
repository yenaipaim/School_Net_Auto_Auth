using SchoolNetAutoAuth.App.Uninstallation;

namespace SchoolNetAutoAuth.App.Tests;

public sealed class UninstallPlanTests
{
    [Fact]
    public void DefaultPlan_PreservesUserData()
    {
        var plan = UninstallPlan.CreateDefault("C:\\App", "C:\\UserData");

        Assert.False(plan.DeleteUserData);
        Assert.Equal("C:\\App", plan.InstallDirectory);
        Assert.Equal("C:\\UserData", plan.UserDataDirectory);
    }

    [Fact]
    public void WithUserDataDeletion_EnablesCredentialAndProfileCleanup()
    {
        var plan = UninstallPlan.CreateDefault("C:\\App", "C:\\UserData") with { DeleteUserData = true };

        Assert.True(plan.DeleteCredential);
        Assert.True(plan.DeleteEdgeProfile);
        Assert.True(plan.DeleteSettings);
    }

    [Theory]
    [InlineData(new[] { "--uninstall" }, false)]
    [InlineData(new[] { "--uninstall", "--quiet" }, true)]
    public void TryParse_UninstallArguments_ReturnsRequestedMode(string[] arguments, bool expectedQuiet)
    {
        var requested = UninstallCommand.TryParse(arguments, out var quiet);

        Assert.True(requested);
        Assert.Equal(expectedQuiet, quiet);
    }

    [Fact]
    public void TryParse_BackgroundMode_IsNotUninstall()
    {
        Assert.False(UninstallCommand.TryParse(["--background"], out _));
    }

    [Fact]
    public void ValidateInstallDirectory_AcceptsOnlyCurrentUserProgramsDirectory()
    {
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "SchoolNetAutoAuth");

        Assert.Equal(Path.GetFullPath(expected), UninstallService.ValidateInstallDirectory(expected));
        Assert.Throws<InvalidOperationException>(() => UninstallService.ValidateInstallDirectory(Path.GetTempPath()));
    }
}
