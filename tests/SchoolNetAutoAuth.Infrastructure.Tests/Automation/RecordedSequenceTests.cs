using SchoolNetAutoAuth.Core.Configuration;
using SchoolNetAutoAuth.Infrastructure.Automation;

namespace SchoolNetAutoAuth.Infrastructure.Tests.Automation;

public sealed class RecordedSequenceTests
{
    [Fact]
    public void CredentialInputDescriptor_IsNotReplayClick()
    {
        var input = new ElementDescriptor("input", "textbox", "账号", "账号", null, null, "#username");
        Assert.False(RecordedClickFilter.ShouldReplay(input));
    }

    [Fact]
    public void ButtonDescriptor_IsReplayClick()
    {
        var button = new ElementDescriptor("button", "button", "登录", null, null, "登录", "#login");
        Assert.True(RecordedClickFilter.ShouldReplay(button));
    }

    [Theory]
    [InlineData("http://2.2.2.2/provider?token=123", "http://2.2.2.2/provider", true)]
    [InlineData("http://2.2.2.2/login", "http://2.2.2.2/provider", false)]
    public void PageMatcher_IgnoresQueryButRequiresRecordedPath(string current, string pattern, bool expected)
    {
        Assert.Equal(expected, RecordedPageMatcher.Matches(current, pattern));
    }

    [Fact]
    public void Orderer_ReturnsClicksInRecordedOrder()
    {
        var second = new RecordedClickStep(2, "page-1", "http://2.2.2.2/next", new(LocatorStrategy.Text, "第二步"), "第二步");
        var first = new RecordedClickStep(1, "page-1", "http://2.2.2.2", new(LocatorStrategy.Text, "第一步"), "第一步");

        Assert.Equal([first, second], RecordedSequenceOrderer.Order([second, first]));
    }
}
