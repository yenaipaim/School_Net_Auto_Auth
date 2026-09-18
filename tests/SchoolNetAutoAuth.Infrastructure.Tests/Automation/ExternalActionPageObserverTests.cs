using SchoolNetAutoAuth.Core.Authentication;
using SchoolNetAutoAuth.Infrastructure.Automation;

namespace SchoolNetAutoAuth.Infrastructure.Tests.Automation;

public sealed class ExternalActionPageObserverTests : IAsyncLifetime
{
    private readonly string _profile = Path.Combine(Path.GetTempPath(), "SchoolNetAutoAuthTests", Guid.NewGuid().ToString("N"));
    private EdgeSession? _session;

    public async Task InitializeAsync()
    {
        _session = await new EdgeSessionFactory(_profile).LaunchAsync(true, CancellationToken.None);
    }

    [Fact]
    public async Task InspectAsync_VisibleDialog_ReturnsDeviceLimit()
    {
        var page = _session!.Context.Pages.FirstOrDefault() ?? await _session.Context.NewPageAsync();
        var observer = new ExternalActionPageObserver(_session.Context, null);
        await observer.AttachAsync(page);
        await page.SetContentAsync("<main>欢迎使用</main><div role='dialog'>在线设备数量已达到上限，请先移除终端</div>");

        var result = await observer.InspectAsync(CancellationToken.None);

        Assert.Equal(ExternalActionKind.DeviceLimit, result?.Kind);
    }

    [Fact]
    public async Task InspectAsync_BrowserDialog_ReturnsPhoneVerification()
    {
        var page = _session!.Context.Pages.FirstOrDefault() ?? await _session.Context.NewPageAsync();
        var observer = new ExternalActionPageObserver(_session.Context, null);
        await observer.AttachAsync(page);
        await page.EvaluateAsync("setTimeout(() => alert('请使用手机拨打电话完成认证'), 0)");
        await Task.Delay(100);

        var result = await observer.InspectAsync(CancellationToken.None);

        Assert.Equal(ExternalActionKind.PhoneVerification, result?.Kind);
    }

    public async Task DisposeAsync()
    {
        if (_session is not null) await _session.DisposeAsync();
        if (Directory.Exists(_profile)) Directory.Delete(_profile, recursive: true);
    }
}
