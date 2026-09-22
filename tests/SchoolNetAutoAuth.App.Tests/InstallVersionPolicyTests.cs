using SchoolNetAutoAuth.Installer;

namespace SchoolNetAutoAuth.App.Tests;

public sealed class InstallVersionPolicyTests
{
    [Theory]
    [InlineData("0.3.0", "0.2.0", true)]
    [InlineData("0.2.0", "0.2.0", false)]
    [InlineData("0.1.0", "0.2.0", false)]
    [InlineData("0.2.0+abc123", "0.2.0", false)]
    [InlineData("0.2.0.0", "0.2.0", false)]
    [InlineData("0.2.0", "0.2.0.0", false)]
    [InlineData(null, "0.2.0", false)]
    public void IsDowngrade_ComparesParsableVersions(string? installed, string? incoming, bool expected)
    {
        Assert.Equal(expected, InstallVersionPolicy.IsDowngrade(installed, incoming));
    }
}
