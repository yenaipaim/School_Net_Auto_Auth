using SchoolNetAutoAuth.App.ViewModels;
using SchoolNetAutoAuth.Core.Authentication;

namespace SchoolNetAutoAuth.App.Tests;

public sealed class ViewModelTests
{
    [Theory]
    [InlineData(AuthenticationState.Online, "已联网")]
    [InlineData(AuthenticationState.Authenticating, "正在认证")]
    [InlineData(AuthenticationState.ExternalActionCooldown, "等待人工处理")]
    public void FormatAuthenticationState_ReturnsUserFacingText(AuthenticationState state, string expected)
    {
        Assert.Equal(expected, StatusViewModel.FormatAuthenticationState(state));
    }
}
