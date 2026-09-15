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
        await coordinator.EvaluateAsync(AppSettings.CreateDefault(), default);
        Assert.Equal(AuthenticationState.WaitingForTargetWifi, coordinator.State);
        Assert.Equal(0, probe.Calls);
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public async Task Evaluate_OfflineOnTargetWifi_RunsAuthentication()
    {
        var runner = new FakeRunner(AuthenticationResult.Success());
        var coordinator = new AuthenticationCoordinator(new FakeNetwork("NSU-SDN"), new FakeProbe(false), runner);
        await coordinator.EvaluateAsync(AppSettings.CreateDefault(), default);
        Assert.Equal(AuthenticationState.Online, coordinator.State);
        Assert.Equal(1, runner.Calls);
    }

    private sealed class FakeNetwork(string ssid) : INetworkMonitor { public Task<NetworkSnapshot> GetSnapshotAsync(CancellationToken token) => Task.FromResult(new NetworkSnapshot(ssid, true)); }
    private sealed class FakeProbe(bool online) : IConnectivityProbe { public int Calls; public Task<ConnectivityResult> CheckAsync(Uri uri, TimeSpan timeout, CancellationToken token) { Calls++; return Task.FromResult(new ConnectivityResult(online, "test")); } }
    private sealed class FakeRunner(AuthenticationResult result) : IAuthenticationRunner { public int Calls; public Task<AuthenticationResult> AuthenticateAsync(AppSettings settings, AuthenticationAttempt attempt, CancellationToken token) { Calls++; return Task.FromResult(result); } }
}
