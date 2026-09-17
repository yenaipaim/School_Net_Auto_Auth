using SchoolNetAutoAuth.Uninstaller;

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
}
