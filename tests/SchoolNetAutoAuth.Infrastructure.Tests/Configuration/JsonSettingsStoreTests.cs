using SchoolNetAutoAuth.Core.Configuration;
using SchoolNetAutoAuth.Infrastructure.Configuration;

namespace SchoolNetAutoAuth.Infrastructure.Tests.Configuration;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "SchoolNetAutoAuthTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveThenLoad_RoundTripsValidatedSettings()
    {
        var store = new JsonSettingsStore(_directory);
        var expected = AppSettings.CreateDefault() with { TargetSsid = "Campus-Test" };
        await store.SaveAsync(expected, CancellationToken.None);
        var actual = await store.LoadAsync(CancellationToken.None);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Load_WhenJsonIsCorrupt_BacksUpFileAndReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "settings.json"), "{broken");
        var actual = await new JsonSettingsStore(_directory).LoadAsync(CancellationToken.None);
        Assert.Equal(AppSettings.CreateDefault(), actual);
        Assert.Single(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    [Fact]
    public async Task Load_VersionOneProviderConfig_MigratesToCurrentSchema()
    {
        Directory.CreateDirectory(_directory);
        var json = """
        {
          "schemaVersion": 1,
          "targetSsid": "NSU-SDN",
          "portalUri": "http://2.2.2.2",
          "probeUri": "https://www.yuanshen.com",
          "selectedProvider": "中国移动",
          "networkCheckInterval": "00:00:15",
          "probeTimeout": "00:00:08",
          "authenticationTimeout": "00:00:45",
          "retryInterval": "00:00:10",
          "maximumAttempts": 3,
          "startWithWindows": true,
          "recordedFlow": null
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(_directory, "settings.json"), json);

        var settings = await new JsonSettingsStore(_directory).LoadAsync(CancellationToken.None);

        Assert.Equal(7, settings.SchemaVersion);
        Assert.True(settings.AutomaticAuthenticationEnabled);
        Assert.Null(settings.BackgroundImagePath);
        Assert.Equal(0.45, settings.BackgroundImageOpacity);
        Assert.Null(settings.RecordedSequence);
    }

    [Fact]
    public async Task Load_VersionTwoConfig_EnablesAutomaticAuthentication()
    {
        Directory.CreateDirectory(_directory);
        var json = """
        {
          "schemaVersion": 2,
          "targetSsid": "NSU-SDN",
          "portalUri": "http://2.2.2.2",
          "probeUri": "https://www.yuanshen.com",
          "networkCheckInterval": "00:00:15",
          "probeTimeout": "00:00:08",
          "authenticationTimeout": "00:00:45",
          "retryInterval": "00:00:10",
          "maximumAttempts": 3,
          "startWithWindows": true,
          "recordedSequence": null
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(_directory, "settings.json"), json);

        var settings = await new JsonSettingsStore(_directory).LoadAsync(CancellationToken.None);

        Assert.Equal(7, settings.SchemaVersion);
        Assert.True(settings.AutomaticAuthenticationEnabled);
        Assert.Equal(0.45, settings.BackgroundImageOpacity);
    }

    [Fact]
    public async Task Load_VersionThreeConfig_AddsEmptyBackgroundImage()
    {
        Directory.CreateDirectory(_directory);
        var json = """
        {
          "schemaVersion": 3,
          "targetSsid": "NSU-SDN",
          "portalUri": "http://2.2.2.2",
          "probeUri": "https://www.yuanshen.com",
          "networkCheckInterval": "00:00:15",
          "probeTimeout": "00:00:08",
          "authenticationTimeout": "00:00:45",
          "retryInterval": "00:00:10",
          "maximumAttempts": 3,
          "startWithWindows": true,
          "automaticAuthenticationEnabled": true,
          "recordedSequence": null
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(_directory, "settings.json"), json);

        var settings = await new JsonSettingsStore(_directory).LoadAsync(CancellationToken.None);

        Assert.Equal(7, settings.SchemaVersion);
        Assert.Null(settings.BackgroundImagePath);
        Assert.Equal(0.45, settings.BackgroundImageOpacity);
    }

    [Fact]
    public async Task Load_VersionFourConfig_AddsBackgroundOpacity()
    {
        Directory.CreateDirectory(_directory);
        var json = """
        {
          "schemaVersion": 4,
          "targetSsid": "NSU-SDN",
          "portalUri": "http://2.2.2.2",
          "probeUri": "https://www.yuanshen.com",
          "networkCheckInterval": "00:00:15",
          "probeTimeout": "00:00:08",
          "authenticationTimeout": "00:00:45",
          "retryInterval": "00:00:10",
          "maximumAttempts": 3,
          "startWithWindows": true,
          "automaticAuthenticationEnabled": true,
          "backgroundImagePath": "C:\\test\\background.png",
          "recordedSequence": null
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(_directory, "settings.json"), json);

        var settings = await new JsonSettingsStore(_directory).LoadAsync(CancellationToken.None);

        Assert.Equal(7, settings.SchemaVersion);
        Assert.Equal("C:\\test\\background.png", settings.BackgroundImagePath);
        Assert.Equal(0.45, settings.BackgroundImageOpacity);
    }

    [Fact]
    public async Task Load_VersionSixConfig_MigratesToSevenAndStaysConfigured()
    {
        Directory.CreateDirectory(_directory);
        var json = """
        {
          "schemaVersion": 6,
          "targetSsid": "NSU-SDN",
          "portalUri": "http://2.2.2.2",
          "probeUri": "https://www.yuanshen.com",
          "networkCheckInterval": "00:00:15",
          "probeTimeout": "00:00:08",
          "authenticationTimeout": "00:00:45",
          "retryInterval": "00:00:10",
          "maximumAttempts": 3,
          "startWithWindows": true,
          "automaticAuthenticationEnabled": true,
          "backgroundImagePath": null,
          "backgroundImageOpacity": 0.45,
          "recordedSequence": null,
          "theme": "dark",
          "accentColor": "#0078D4",
          "interfaceFontFamily": null
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(_directory, "settings.json"), json);

        var settings = await new JsonSettingsStore(_directory).LoadAsync(CancellationToken.None);

        Assert.Equal(7, settings.SchemaVersion);
        Assert.True(settings.IsConfigured);
        Assert.Equal("NSU-SDN", settings.TargetSsid);
        Assert.False(settings.FirstRunPromptShown);
    }

    [Fact]
    public async Task Load_WhenValidateFails_KeepsFileAndReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        var settingsPath = Path.Combine(_directory, "settings.json");
        var json = """
        {
          "schemaVersion": 7,
          "targetSsid": "",
          "portalUri": null,
          "probeUri": "https://www.yuanshen.com",
          "networkCheckInterval": "00:00:15",
          "probeTimeout": "00:00:08",
          "authenticationTimeout": "00:00:45",
          "retryInterval": "00:00:00",
          "maximumAttempts": 0,
          "startWithWindows": true,
          "automaticAuthenticationEnabled": true,
          "backgroundImagePath": null,
          "backgroundImageOpacity": 0.45,
          "recordedSequence": null,
          "theme": "dark",
          "accentColor": "#0078D4",
          "interfaceFontFamily": null,
          "firstRunPromptShown": false
        }
        """;
        await File.WriteAllTextAsync(settingsPath, json);

        var actual = await new JsonSettingsStore(_directory).LoadAsync(CancellationToken.None);

        Assert.Equal(AppSettings.CreateDefault(), actual);
        Assert.True(File.Exists(settingsPath));
        Assert.Empty(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    [Fact]
    public async Task Save_WhenCanceled_KeepsExistingSettingsAndRemovesTemporaryFile()
    {
        var store = new JsonSettingsStore(_directory);
        var original = AppSettings.CreateDefault();
        await store.SaveAsync(original, CancellationToken.None);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var changed = original with { BackgroundImageOpacity = 0.8 };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.SaveAsync(changed, cancellation.Token));

        Assert.Equal(original, await store.LoadAsync(CancellationToken.None));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task Save_ConcurrentWrites_LeaveValidSettingsAndNoTemporaryFiles()
    {
        var store = new JsonSettingsStore(_directory);
        var settings = Enumerable.Range(1, 24)
            .Select(index => AppSettings.CreateDefault() with
            {
                BackgroundImageOpacity = index / 100d
            })
            .ToArray();

        await Task.WhenAll(settings.Select(value => store.SaveAsync(value, CancellationToken.None)));

        var actual = await store.LoadAsync(CancellationToken.None);
        Assert.InRange(actual.BackgroundImageOpacity, 0.01, 0.24);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
