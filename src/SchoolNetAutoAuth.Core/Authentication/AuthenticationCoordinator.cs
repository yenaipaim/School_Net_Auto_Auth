using SchoolNetAutoAuth.Core.Configuration;
using SchoolNetAutoAuth.Core.Contracts;

namespace SchoolNetAutoAuth.Core.Authentication;

public sealed class AuthenticationCoordinator
{
    private readonly INetworkMonitor _network;
    private readonly IConnectivityProbe _probe;
    private readonly IAuthenticationRunner _runner;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _observedOffline;

    public AuthenticationState State { get; private set; } = AuthenticationState.WaitingForTargetWifi;
    public DateTimeOffset? RetryNotBeforeUtc { get; private set; }
    public event EventHandler<AuthenticationState>? StateChanged;
    public event EventHandler<AuthenticationNotice>? NoticeRaised;

    public AuthenticationCoordinator(
        INetworkMonitor network,
        IConnectivityProbe probe,
        IAuthenticationRunner runner,
        TimeProvider? timeProvider = null)
    {
        _network = network;
        _probe = probe;
        _runner = runner;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task EvaluateAsync(AppSettings settings, AuthenticationTrigger trigger, CancellationToken cancellationToken)
    {
        if (trigger == AuthenticationTrigger.Manual)
            await _gate.WaitAsync(cancellationToken);
        else if (!await _gate.WaitAsync(0, cancellationToken))
            return;
        try
        {
            if (trigger == AuthenticationTrigger.Manual) RetryNotBeforeUtc = null;
            var snapshot = await _network.GetSnapshotAsync(cancellationToken);
            if (!snapshot.IsConnected || !string.Equals(snapshot.Ssid, settings.TargetSsid, StringComparison.Ordinal))
            {
                _observedOffline = true;
                SetState(AuthenticationState.WaitingForTargetWifi);
                return;
            }

            SetState(AuthenticationState.CheckingConnectivity);
            if ((await _probe.CheckAsync(settings.ProbeUri, settings.ProbeTimeout, cancellationToken)).IsOnline)
            {
                SetOnline();
                return;
            }
            _observedOffline = true;

            if (trigger == AuthenticationTrigger.Background && RetryNotBeforeUtc > _timeProvider.GetUtcNow())
            {
                SetState(AuthenticationState.ExternalActionCooldown);
                return;
            }

            for (var attempt = 1; attempt <= settings.MaximumAttempts; attempt++)
            {
                SetState(AuthenticationState.Authenticating);
                var result = await _runner.AuthenticateAsync(settings, new(attempt, settings.MaximumAttempts, attempt == settings.MaximumAttempts), cancellationToken);
                switch (result.Outcome)
                {
                    case AuthenticationOutcome.Succeeded:
                        RetryNotBeforeUtc = null;
                        SetOnline();
                        return;
                    case AuthenticationOutcome.CredentialsRequired:
                        SetState(AuthenticationState.WaitingForCredentials);
                        return;
                    case AuthenticationOutcome.RecordingRequired:
                        SetState(AuthenticationState.ActionRequired);
                        return;
                    case AuthenticationOutcome.ExternalActionRequired:
                        if ((await _probe.CheckAsync(settings.ProbeUri, settings.ProbeTimeout, cancellationToken)).IsOnline)
                        {
                            RetryNotBeforeUtc = null;
                            SetOnline();
                            return;
                        }
                        RetryNotBeforeUtc = _timeProvider.GetUtcNow().AddMinutes(5);
                        SetState(AuthenticationState.ExternalActionCooldown);
                        NoticeRaised?.Invoke(this, new(
                            AuthenticationNoticeKind.ExternalActionRequired,
                            result.UserMessage ?? result.ReasonCode,
                            result.ExternalAction,
                            RetryNotBeforeUtc));
                        return;
                    case AuthenticationOutcome.Cancelled:
                        SetState(AuthenticationState.WaitingForTargetWifi);
                        return;
                }

                if (attempt < settings.MaximumAttempts)
                {
                    SetState(AuthenticationState.RetryDelay);
                    await Task.Delay(settings.RetryInterval, cancellationToken);
                }
            }

            SetState(AuthenticationState.ActionRequired);
        }
        finally { _gate.Release(); }
    }

    private void SetOnline()
    {
        var notify = _observedOffline;
        RetryNotBeforeUtc = null;
        _observedOffline = false;
        SetState(AuthenticationState.Online);
        if (notify)
            NoticeRaised?.Invoke(this, new(AuthenticationNoticeKind.Connected, "校园网已连接，可以上网了。"));
    }

    public async Task WaitForIdleAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        _gate.Release();
    }

    private void SetState(AuthenticationState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }
}
