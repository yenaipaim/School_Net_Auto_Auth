using System.Text;
using SchoolNetAutoAuth.Core.Authentication;
using SchoolNetAutoAuth.Infrastructure.Logging;

namespace SchoolNetAutoAuth.Infrastructure.Tests.Logging;

public sealed class RollingFileLoggerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "SchoolNetAutoAuthTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Write_WhenLogExceedsOneMiB_RemovesOldestCompleteLines()
    {
        var path = Path.Combine(_directory, "SchoolNetAutoAuth.log");
        var logger = new RollingFileLogger(path, new FixedTimeProvider());

        for (var attempt = 0; attempt < 25_000; attempt++)
            logger.Write(new(AppLogEvent.AuthenticationAttempt, Attempt: attempt));

        var bytes = File.ReadAllBytes(path);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.True(bytes.Length <= 1024 * 1024);
        Assert.DoesNotContain("attempt=0 ", text, StringComparison.Ordinal);
        Assert.Contains("attempt=24999", text, StringComparison.Ordinal);
        Assert.All(File.ReadAllLines(path), line => Assert.StartsWith("2026-09-17T10:00:00.0000000+00:00", line, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Write_FromConcurrentCallers_ProducesValidUtf8Lines()
    {
        var path = Path.Combine(_directory, "SchoolNetAutoAuth.log");
        var logger = new RollingFileLogger(path, new FixedTimeProvider());

        await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (var index = 0; index < 500; index++)
                logger.Write(new(AppLogEvent.StateChanged, AuthenticationState.Authenticating, Attempt: worker * 500 + index));
        })));

        var text = File.ReadAllText(path, new UTF8Encoding(false, true));
        Assert.Equal(4_000, text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void Write_OnlySerializesWhitelistedStructuredFields()
    {
        var path = Path.Combine(_directory, "SchoolNetAutoAuth.log");
        var logger = new RollingFileLogger(path, new FixedTimeProvider());
        logger.Write(new(
            AppLogEvent.AuthenticationResult,
            AuthenticationState.ExternalActionCooldown,
            AuthenticationOutcome.ExternalActionRequired,
            ExternalActionKind.PhoneVerification,
            2));

        var text = File.ReadAllText(path);
        Assert.Contains("event=AuthenticationResult", text, StringComparison.Ordinal);
        Assert.Contains("state=ExternalActionCooldown", text, StringComparison.Ordinal);
        Assert.Contains("outcome=ExternalActionRequired", text, StringComparison.Ordinal);
        Assert.Contains("externalAction=PhoneVerification", text, StringComparison.Ordinal);
        Assert.DoesNotContain("message=", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("url=", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("username=", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password=", text, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);
    }
}
