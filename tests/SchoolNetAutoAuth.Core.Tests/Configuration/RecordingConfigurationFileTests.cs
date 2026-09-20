using SchoolNetAutoAuth.Core.Configuration;

namespace SchoolNetAutoAuth.Core.Tests.Configuration;

public sealed class RecordingConfigurationFileTests
{
    [Fact]
    public void Constructor_PreservesExportMetadataAndSequence()
    {
        var sequence = CreateSequence();
        var exportedAt = DateTimeOffset.UtcNow;

        var file = new RecordingConfigurationFile(
            RecordingConfigurationFile.ExpectedFormat,
            RecordingConfigurationFile.CurrentVersion,
            exportedAt,
            sequence);

        Assert.Equal(RecordingConfigurationFile.ExpectedFormat, file.Format);
        Assert.Equal(RecordingConfigurationFile.CurrentVersion, file.Version);
        Assert.Equal(exportedAt, file.ExportedAtUtc);
        Assert.Equal(sequence, file.RecordedSequence);
    }

    [Fact]
    public void RecordedSequence_DoesNotContainCredentials()
    {
        var file = new RecordingConfigurationFile(
            RecordingConfigurationFile.ExpectedFormat,
            RecordingConfigurationFile.CurrentVersion,
            DateTimeOffset.UtcNow,
            CreateSequence());

        var names = file.GetType().GetProperties().Select(property => property.Name).ToArray();

        Assert.DoesNotContain("Username", names);
        Assert.DoesNotContain("Password", names);
        Assert.DoesNotContain("TargetSsid", names);
        Assert.DoesNotContain("PortalUri", names);
    }

    private static RecordedClickSequence CreateSequence() => new(
        1,
        DateTimeOffset.UtcNow,
        new(
            new(LocatorStrategy.Label, "账号", "账号"),
            new(LocatorStrategy.Label, "密码", "密码"),
            "page-1",
            "https://portal.example/login"),
        [new(1, "page-1", "https://portal.example/login", new(LocatorStrategy.Role, "button", "登录"), "登录")]);
}
