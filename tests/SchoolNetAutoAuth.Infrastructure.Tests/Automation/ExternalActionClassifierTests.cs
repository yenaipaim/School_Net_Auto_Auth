using SchoolNetAutoAuth.Core.Authentication;
using SchoolNetAutoAuth.Core.Contracts;
using SchoolNetAutoAuth.Infrastructure.Automation;

namespace SchoolNetAutoAuth.Infrastructure.Tests.Automation;

public sealed class ExternalActionClassifierTests
{
    [Theory]
    [InlineData("请使用手机拨打页面中的电话完成认证")]
    [InlineData("需要进行语音验证，完成后请重试")]
    [InlineData("Phone verification is required")]
    public void Classify_PhoneVerificationText_ReturnsPhoneVerification(string text)
    {
        var result = ExternalActionClassifier.Classify([text], null);

        Assert.Equal(ExternalActionKind.PhoneVerification, result?.Kind);
    }

    [Theory]
    [InlineData("在线设备数量已达到上限，请先移除终端")]
    [InlineData("终端数已满，无法添加新设备")]
    [InlineData("Device limit exceeded")]
    public void Classify_DeviceLimitText_ReturnsDeviceLimit(string text)
    {
        var result = ExternalActionClassifier.Classify([text], null);

        Assert.Equal(ExternalActionKind.DeviceLimit, result?.Kind);
    }

    [Theory]
    [InlineData("登录失败，请检查账号或密码")]
    [InlineData("帮助  返回首页  忘记密码")]
    [InlineData("欢迎使用校园网络")]
    public void Classify_UnrelatedText_ReturnsNull(string text)
    {
        Assert.Null(ExternalActionClassifier.Classify([text], null));
    }

    [Fact]
    public void Classify_SafeExcerpt_RedactsCredentialsAndLongTokens()
    {
        var token = new string('a', 48);
        var credential = new PortalCredential("2026123456", "Secret-Password");
        var text = $"账号 2026123456 需要手机拨打电话验证，password=Secret-Password token={token}";

        var result = ExternalActionClassifier.Classify([text], credential);

        Assert.NotNull(result);
        Assert.DoesNotContain(credential.Username, result.SafeExcerpt, StringComparison.Ordinal);
        Assert.DoesNotContain(credential.Password, result.SafeExcerpt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(token, result.SafeExcerpt, StringComparison.Ordinal);
        Assert.Contains("[已隐藏]", result.SafeExcerpt, StringComparison.Ordinal);
    }

    [Fact]
    public void Classify_SafeExcerpt_IsSingleLineAndAtMostThreeHundredCharacters()
    {
        var text = "需要手机\r\n拨打电话完成认证 " + new string('测', 500);

        var result = ExternalActionClassifier.Classify([text], null);

        Assert.NotNull(result);
        Assert.DoesNotContain('\r', result.SafeExcerpt);
        Assert.DoesNotContain('\n', result.SafeExcerpt);
        Assert.True(result.SafeExcerpt.Length <= 300);
    }
}
