using SchoolNetAutoAuth.Core.Authentication;
using SchoolNetAutoAuth.Core.Configuration;
using SchoolNetAutoAuth.Core.Contracts;

namespace SchoolNetAutoAuth.Core.Tests.Authentication;

public sealed class AuthenticationCoordinatorTests
{
    [Fact]
    public async Task Evaluate_WrongSsid_DoesNotProbeOrAuthenticate()
    {
        var probe = new FakeProbe(true);
        var runner = new FakeRunner(AuthenticationResult.Success());
        var coordinator = new AuthenticationCoordinator(new FakeNetwork("Other"), probe, runner);
        await coordinator.EvaluateAsync(AppSettings.CreateDefault(), AuthenticationTrigger.Background, default);
        Assert.Equal(AuthenticationState.WaitingForTargetWifi, coordinator.State);
        Assert.Equal(0, probe.Calls);
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public async Task Evaluate_WrongSsidThenOnlineTargetWifi_RaisesConnectedNotice()
    {
        var network = new SequenceNetwork("Other", "NSU-SDN", "NSU-SDN");
        var coordinator = new AuthenticationCoordinator(network, new FakeProbe(true), new FakeRunner(AuthenticationResult.Success()));
        var notices = new List<AuthenticationNotice>();
        coordinator.NoticeRaised += (_, notice) => notices.Add(notice);

        await coordinator.EvaluateAsync(AppSettings.CreateDefault(), AuthenticationTrigger.Background, default);
        await coordinator.EvaluateAsync(AppSettings.CreateDefault(), AuthenticationTrigger.Background, default);
        await coordinator.EvaluateAsync(AppSettings.CreateDefault(), AuthenticationTrigger.Background, default);

        Assert.Single(notices.Where(notice => notice.Kind == AuthenticationNoticeKind.Connected));
    }

    [Fact]
    public async Task Evaluate_OfflineOnTargetWifi_RunsAuthentication()
    {
        var runner = new FakeRunner(AuthenticationResult.Success());
        var coordinator = new AuthenticationCoordinator(new FakeNetwork("NSU-SDN"), new FakeProbe(false), runner);
        await coordinator.EvaluateAsync(AppSettings.CreateDefault(), AuthenticationTrigger.Background, default);
        Assert.Equal(AuthenticationState.Online, coordinator.State);
        Assert.Equal(1, runner.Calls);
    }

    [Fact]
    public async Task Evaluate_ExternalAction_StartsFiveMinuteCooldown()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.Zero));
        var runner = new FakeRunner(new(AuthenticationOutcome.ExternalActionRequired, "phone_verification", ExternalActionKind.PhoneVerification, "需要电话验证"));
        var coordinator = new AuthenticationCoordinator(new FakeNetwork("NSU-SDN"), new FakeProbe(false), runner, clock);
        AuthenticationNotice? notice = null;
        coordinator.NoticeRaised += (_, value) => notice = value;

        await coordinator.EvaluateAsync(AppSettings.CreateDefault() with { MaximumAttempts = 1 }, AuthenticationTrigger.Background, default);

        Assert.Equal(AuthenticationState.ExternalActionCooldown, coordinator.State);
        Assert.Equal(clock.GetUtcNow().AddMinutes(5), coordinator.RetryNotBeforeUtc);
        Assert.Equal(ExternalActionKind.PhoneVerification, notice?.ExternalAction);
    }

    [Fact]
    public async Task Evaluate_BackgroundDuringCooldown_ProbesWithoutAuthenticating()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.Zero));
        var probe = new FakeProbe(false);
        var runner = new FakeRunner(new(AuthenticationOutcome.ExternalActionRequired, "device_limit", ExternalActionKind.DeviceLimit, "在线设备达到上限"));
        var coordinator = new AuthenticationCoordinator(new FakeNetwork("NSU-SDN"), probe, runner, clock);
        var settings = AppSettings.CreateDefault() with { MaximumAttempts = 1 };
        await coordinator.EvaluateAsync(settings, AuthenticationTrigger.Background, default);

        await coordinator.EvaluateAsync(settings, AuthenticationTrigger.Background, default);

        Assert.Equal(1, runner.Calls);
        Assert.Equal(3, probe.Calls);
        Assert.Equal(AuthenticationState.ExternalActionCooldown, coordinator.State);
    }

    [Fact]
    public async Task Evaluate_AfterCooldownExpires_AuthenticatesAgain()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.Zero));
        var runner = new FakeRunner(new(AuthenticationOutcome.ExternalActionRequired, "device_limit", ExternalActionKind.DeviceLimit, "在线设备达到上限"));
        var coordinator = new AuthenticationCoordinator(new FakeNetwork("NSU-SDN"), new FakeProbe(false), runner, clock);
        var settings = AppSettings.CreateDefault() with { MaximumAttempts = 1 };
        await coordinator.EvaluateAsync(settings, AuthenticationTrigger.Background, default);
        clock.Advance(TimeSpan.FromMinutes(5));

        await coordinator.EvaluateAsync(settings, AuthenticationTrigger.Background, default);

        Assert.Equal(2, runner.Calls);
        Assert.Equal(clock.GetUtcNow().AddMinutes(5), coordinator.RetryNotBeforeUtc);
    }

    [Fact]
    public async Task Evaluate_ManualTrigger_BypassesCooldown()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.Zero));
        var runner = new FakeRunner(new(AuthenticationOutcome.ExternalActionRequired, "phone_verification", ExternalActionKind.PhoneVerification, "需要电话验证"));
        var coordinator = new AuthenticationCoordinator(new FakeNetwork("NSU-SDN"), new FakeProbe(false), runner, clock);
        var settings = AppSettings.CreateDefault() with { MaximumAttempts = 1 };
        await coordinator.EvaluateAsync(settings, AuthenticationTrigger.Background, default);

        await coordinator.EvaluateAsync(settings, AuthenticationTrigger.Manual, default);

        Assert.Equal(2, runner.Calls);
    }

    [Fact]
    public async Task Evaluate_ManualTriggerDuringBackgroundEvaluation_WaitsAndRunsNext()
    {
        var runner = new BlockingThenSuccessRunner();
        var coordinator = new AuthenticationCoordinator(new FakeNetwork("NSU-SDN"), new FakeProbe(false), runner);
        var settings = AppSettings.CreateDefault() with { MaximumAttempts = 1 };
        var background = coordinator.EvaluateAsync(settings, AuthenticationTrigger.Background, default);
        await runner.FirstCallStarted.Task;

        var manual = coordinator.EvaluateAsync(settings, AuthenticationTrigger.Manual, default);
        Assert.False(manual.IsCompleted);
        runner.ReleaseFirstCall.SetResult();
        await Task.WhenAll(background, manual);

        Assert.Equal(2, runner.Calls);
        Assert.Equal(AuthenticationState.Online, coordinator.State);
    }

    [Fact]
    public async Task Evaluate_OnlineDuringCooldown_ClearsCooldownAndNotifiesOnce()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.Zero));
        var probe = new SequenceProbe(false, false, true, true);
        var runner = new FakeRunner(new(AuthenticationOutcome.ExternalActionRequired, "phone_verification", ExternalActionKind.PhoneVerification, "需要电话验证"));
        var coordinator = new AuthenticationCoordinator(new FakeNetwork("NSU-SDN"), probe, runner, clock);
        var notices = new List<AuthenticationNotice>();
        coordinator.NoticeRaised += (_, value) => notices.Add(value);
        var settings = AppSettings.CreateDefault() with { MaximumAttempts = 1 };
        await coordinator.EvaluateAsync(settings, AuthenticationTrigger.Background, default);

        await coordinator.EvaluateAsync(settings, AuthenticationTrigger.Background, default);
        await coordinator.EvaluateAsync(settings, AuthenticationTrigger.Background, default);

        Assert.Null(coordinator.RetryNotBeforeUtc);
        Assert.Equal(AuthenticationState.Online, coordinator.State);
        Assert.Single(notices.Where(x => x.Kind == AuthenticationNoticeKind.Connected));
    }

    private sealed class FakeNetwork(string ssid) : INetworkMonitor { public Task<NetworkSnapshot> GetSnapshotAsync(CancellationToken token) => Task.FromResult(new NetworkSnapshot(ssid, true)); }
    private sealed class SequenceNetwork(params string[] ssids) : INetworkMonitor
    {
        private readonly Queue<string> _ssids = new(ssids);
        public Task<NetworkSnapshot> GetSnapshotAsync(CancellationToken token) =>
            Task.FromResult(new NetworkSnapshot(_ssids.Dequeue(), true));
    }
    private sealed class FakeProbe(bool online) : IConnectivityProbe { public int Calls; public Task<ConnectivityResult> CheckAsync(Uri uri, TimeSpan timeout, CancellationToken token) { Calls++; return Task.FromResult(new ConnectivityResult(online, "test")); } }
    private sealed class FakeRunner(AuthenticationResult result) : IAuthenticationRunner { public int Calls; public Task<AuthenticationResult> AuthenticateAsync(AppSettings settings, AuthenticationAttempt attempt, CancellationToken token) { Calls++; return Task.FromResult(result); } }

    private sealed class BlockingThenSuccessRunner : IAuthenticationRunner
    {
        public int Calls;
        public TaskCompletionSource FirstCallStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirstCall { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<AuthenticationResult> AuthenticateAsync(AppSettings settings, AuthenticationAttempt attempt, CancellationToken token)
        {
            Calls++;
            if (Calls == 1)
            {
                FirstCallStarted.SetResult();
                await ReleaseFirstCall.Task.WaitAsync(token);
                return new(AuthenticationOutcome.Failed, "first_failed");
            }
            return AuthenticationResult.Success();
        }
    }

    private sealed class SequenceProbe(params bool[] results) : IConnectivityProbe
    {
        private readonly Queue<bool> _results = new(results);
        public Task<ConnectivityResult> CheckAsync(Uri uri, TimeSpan timeout, CancellationToken token) =>
            Task.FromResult(new ConnectivityResult(_results.Count > 0 && _results.Dequeue(), "test"));
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan value) => _now += value;
    }
}
