using SchoolNetAutoAuth.Core.Configuration;
using SchoolNetAutoAuth.Infrastructure.Configuration;

namespace SchoolNetAutoAuth.Infrastructure.Tests.Configuration;

public sealed class RecordingConfigurationSerializerTests
{
    [Fact]
    public void SerializeThenDeserialize_RoundTripsSequence()
    {
        var sequence = CreateSequence();
        var serializer = new RecordingConfigurationSerializer();

        var json = serializer.Serialize(sequence);
        var actual = serializer.Deserialize(json);

        Assert.Equal(sequence, actual);
    }

    [Fact]
    public void Deserialize_RejectsUnknownVersion()
    {
        var serializer = new RecordingConfigurationSerializer();
        var json = serializer.Serialize(CreateSequence()).Replace("\"version\": 1", "\"version\": 99");

        var error = Assert.Throws<InvalidDataException>(() => serializer.Deserialize(json));

        Assert.Contains("版本", error.Message);
    }

    [Fact]
    public void Deserialize_RejectsMalformedJson()
    {
        var serializer = new RecordingConfigurationSerializer();

        Assert.Throws<InvalidDataException>(() => serializer.Deserialize("{broken"));
    }

    [Fact]
    public void Serialize_ExcludesApplicationSettingsAndCredentialValues()
    {
        var serializer = new RecordingConfigurationSerializer();

        var json = serializer.Serialize(CreateSequence());

        Assert.DoesNotContain("TargetSsid", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PortalUri", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("username", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsNonSequentialSteps()
    {
        var sequence = CreateSequence() with
        {
            Clicks = [CreateSequence().Clicks[0] with { Order = 2 }]
        };
        var file = new RecordingConfigurationFile(
            RecordingConfigurationFile.ExpectedFormat,
            RecordingConfigurationFile.CurrentVersion,
            DateTimeOffset.UtcNow,
            sequence);

        var errors = new RecordingConfigurationSerializer().Validate(file);

        Assert.Contains(errors, error => error.Contains("顺序"));
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
