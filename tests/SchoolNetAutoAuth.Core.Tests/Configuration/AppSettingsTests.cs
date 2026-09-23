using SchoolNetAutoAuth.Core.Configuration;

namespace SchoolNetAutoAuth.Core.Tests.Configuration;

public sealed class AppSettingsTests
{
    [Fact]
    public void CreateDefault_EnablesAutomaticAuthentication()
    {
        var settings = AppSettings.CreateDefault();
        Assert.True(settings.AutomaticAuthenticationEnabled);
        Assert.Null(settings.BackgroundImagePath);
        Assert.Equal(0.45, settings.BackgroundImageOpacity);
        Assert.Equal(5, settings.SchemaVersion);
    }

    [Fact]
    public void CreateDefault_ChecksNetworkEveryFifteenSecondsWhenOffline()
    {
        Assert.Equal(TimeSpan.FromSeconds(15), AppSettings.CreateDefault().NetworkCheckInterval);
    }
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

    [Fact]
    public void Validate_RejectsRelativeProbeWithoutThrowing()
    {
        var settings = AppSettings.CreateDefault() with { ProbeUri = new Uri("relative", UriKind.Relative) };
        var result = settings.Validate();
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsBackgroundOpacityOutsideRange()
    {
        var settings = AppSettings.CreateDefault() with { BackgroundImageOpacity = 1.2 };
        Assert.False(settings.Validate().IsValid);
    }
}
