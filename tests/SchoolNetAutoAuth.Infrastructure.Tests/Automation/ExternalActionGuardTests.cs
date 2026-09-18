using SchoolNetAutoAuth.Core.Authentication;
using SchoolNetAutoAuth.Core.Contracts;
using SchoolNetAutoAuth.Infrastructure.Automation;

namespace SchoolNetAutoAuth.Infrastructure.Tests.Automation;

public sealed class ExternalActionGuardTests
{
    [Fact]
    public async Task CheckAsync_DetectionButAlreadyOnline_ReturnsSuccess()
    {
        var observer = new FakeObserver(new(ExternalActionKind.PhoneVerification, "需要电话验证"));
        var guard = new ExternalActionGuard(new FakeProbe(true));

        var result = await guard.CheckAsync(observer, new Uri("https://example.test"), TimeSpan.FromSeconds(1), default);

        Assert.Equal(AuthenticationOutcome.Succeeded, result?.Outcome);
    }

    [Fact]
    public async Task CheckAsync_NoDetectionButAlreadyOnline_ReturnsSuccess()
    {
        var observer = new FakeObserver(null);
        var guard = new ExternalActionGuard(new FakeProbe(true));

        var result = await guard.CheckAsync(observer, new Uri("https://example.test"), TimeSpan.FromSeconds(1), default);

        Assert.Equal(AuthenticationOutcome.Succeeded, result?.Outcome);
    }

    [Fact]
    public async Task CheckAsync_IntermediateStepWithoutDetection_DoesNotProbe()
    {
        var observer = new FakeObserver(null);
        var probe = new FakeProbe(false);
        var guard = new ExternalActionGuard(probe);

        var result = await guard.CheckAsync(
            observer,
            new Uri("https://example.test"),
            TimeSpan.FromSeconds(8),
            default,
            probeWithoutDetection: false);

        Assert.Null(result);
        Assert.Equal(0, probe.Calls);
    }

    [Fact]
    public async Task CheckAsync_DetectionAndOffline_ReturnsExternalAction()
    {
        var observer = new FakeObserver(new(ExternalActionKind.DeviceLimit, "在线设备达到上限"));
        var guard = new ExternalActionGuard(new FakeProbe(false));

        var result = await guard.CheckAsync(observer, new Uri("https://example.test"), TimeSpan.FromSeconds(1), default);

        Assert.Equal(AuthenticationOutcome.ExternalActionRequired, result?.Outcome);
        Assert.Equal(ExternalActionKind.DeviceLimit, result?.ExternalAction);
        Assert.Equal("在线设备达到上限", result?.UserMessage);
    }

    private sealed class FakeObserver(ExternalActionDetection? result) : IExternalActionObserver
    {
        public Task<ExternalActionDetection?> InspectAsync(CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class FakeProbe(bool online) : IConnectivityProbe
    {
        public int Calls { get; private set; }
        public Task<ConnectivityResult> CheckAsync(Uri probeUri, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult(new ConnectivityResult(online, $"test_{++Calls}"));
    }
}
