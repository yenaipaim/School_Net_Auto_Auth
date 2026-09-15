using SchoolNetAutoAuth.Core.Configuration;

namespace SchoolNetAutoAuth.Core.Tests.Configuration;

public sealed class AppSettingsTests
{
    [Fact]
    public void CreateDefault_UsesApprovedPortalValues()
    {
        var settings = AppSettings.CreateDefault();
        Assert.Equal("NSU-SDN", settings.TargetSsid);
        Assert.Equal(new Uri("http://2.2.2.2"), settings.PortalUri);
        Assert.Equal(new Uri("https://www.yuanshen.com"), settings.ProbeUri);
        Assert.True(settings.StartWithWindows);
    }

    [Fact]
    public void Validate_RejectsNonPositiveRetryValues()
    {
        var settings = AppSettings.CreateDefault() with { RetryInterval = TimeSpan.Zero, MaximumAttempts = 0 };
        Assert.False(settings.Validate().IsValid);
    }
}
