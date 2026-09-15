using SchoolNetAutoAuth.Core.Configuration;
using SchoolNetAutoAuth.Infrastructure.Automation;

namespace SchoolNetAutoAuth.Infrastructure.Tests.Automation;

public sealed class LocatorCandidateFactoryTests
{
    [Fact]
    public void CreateCandidates_PrefersRoleAndAccessibleName()
    {
        var candidates = LocatorCandidateFactory.CreateCandidates(new("button", "button", "登录", null, null, "登录", "#login"));
        Assert.Equal(LocatorStrategy.Role, candidates[0].Strategy);
        Assert.Equal("登录", candidates[0].Name);
        Assert.Equal(LocatorStrategy.Text, candidates[1].Strategy);
        Assert.Equal(LocatorStrategy.Css, candidates[2].Strategy);
    }
}
