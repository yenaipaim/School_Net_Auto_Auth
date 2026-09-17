using SchoolNetAutoAuth.Core.Configuration;

namespace SchoolNetAutoAuth.Core.Tests.Configuration;

public sealed class RecordedClickSequenceTests
{
    [Fact]
    public void Sequence_PreservesClickOrderAndPageContext()
    {
        var first = new RecordedClickStep(1, "page-1", "http://2.2.2.2/", new(LocatorStrategy.Role, "button", "登录"), "登录");
        var second = new RecordedClickStep(2, "page-2", "http://2.2.2.2/provider", new(LocatorStrategy.Text, "确认"), "确认");
        var credentials = new RecordedCredentialLocators(new(LocatorStrategy.Label, "账号"), new(LocatorStrategy.Label, "密码"));
        var sequence = new RecordedClickSequence(1, DateTimeOffset.UtcNow, credentials, [first, second]);

        Assert.Equal([first, second], sequence.Clicks);
        Assert.Equal("page-2", sequence.Clicks[1].PageKey);
    }

    [Fact]
    public void AppSettings_DefaultHasNoProviderSelection()
    {
        var settings = AppSettings.CreateDefault();
        Assert.Null(settings.RecordedSequence);
        Assert.Null(typeof(AppSettings).GetProperty("SelectedProvider"));
    }
}
