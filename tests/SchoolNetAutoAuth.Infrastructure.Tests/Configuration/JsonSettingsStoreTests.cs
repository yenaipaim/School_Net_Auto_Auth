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

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
