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
        Assert.Equal(7, settings.SchemaVersion);
        Assert.False(settings.FirstRunPromptShown);
    }

    [Fact]
    public void CreateDefault_ChecksNetworkEveryFifteenSecondsWhenOffline()
    {
        Assert.Equal(TimeSpan.FromSeconds(15), AppSettings.CreateDefault().NetworkCheckInterval);
    }
    [Fact]
    public void CreateDefault_ShipsNoSchoolInformation()
    {
        var settings = AppSettings.CreateDefault();
        Assert.Equal(string.Empty, settings.TargetSsid);
        Assert.Null(settings.PortalUri);
        Assert.Equal(new Uri("https://www.yuanshen.com"), settings.ProbeUri);
        Assert.True(settings.StartWithWindows);
        Assert.False(settings.IsConfigured);
        Assert.True(settings.Validate().IsValid);
    }

    [Fact]
    public void IsConfigured_RequiresSsidAndAbsolutePortal()
    {
        var configured = AppSettings.CreateDefault() with
        {
            TargetSsid = "NSU-SDN",
            PortalUri = new Uri("http://2.2.2.2")
        };
        Assert.True(configured.IsConfigured);
        Assert.False((configured with { TargetSsid = "  " }).IsConfigured);
        Assert.False((configured with { PortalUri = null }).IsConfigured);
    }

    [Fact]
    public void Validate_RejectsSsidLongerThan32Bytes()
    {
        var settings = AppSettings.CreateDefault() with { TargetSsid = new string('中', 11) };
        Assert.False(settings.Validate().IsValid);
        Assert.True((AppSettings.CreateDefault() with { TargetSsid = new string('中', 10) }).Validate().IsValid);
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

    [Fact]
    public void CreateDefault_UsesFlatAppearanceTokens()
    {
        var settings = AppSettings.CreateDefault();
        Assert.Equal(AppSettings.DarkTheme, settings.Theme);
        Assert.Equal("#0078D4", settings.AccentColor);
        Assert.Null(settings.InterfaceFontFamily);
    }

    [Fact]
    public void Validate_RejectsUnknownThemeAndMalformedAccent()
    {
        Assert.False((AppSettings.CreateDefault() with { Theme = "solarized" }).Validate().IsValid);
        Assert.False((AppSettings.CreateDefault() with { AccentColor = "0078D4" }).Validate().IsValid);
        Assert.True((AppSettings.CreateDefault() with { AccentColor = "#07d" }).Validate().IsValid);
    }
}
