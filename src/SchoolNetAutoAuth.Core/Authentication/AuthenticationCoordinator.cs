using SchoolNetAutoAuth.Core.Configuration;
using SchoolNetAutoAuth.Core.Contracts;

namespace SchoolNetAutoAuth.Core.Authentication;

public sealed class AuthenticationCoordinator
{
    private readonly INetworkMonitor _network;
    private readonly IConnectivityProbe _probe;
    private readonly IAuthenticationRunner _runner;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AuthenticationState State { get; private set; } = AuthenticationState.WaitingForTargetWifi;
    public event EventHandler<AuthenticationState>? StateChanged;

    public AuthenticationCoordinator(INetworkMonitor network, IConnectivityProbe probe, IAuthenticationRunner runner)
    {
        _network = network;
        _probe = probe;
        _runner = runner;
    }

    public async Task EvaluateAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken)) return;
        try
        {
            var snapshot = await _network.GetSnapshotAsync(cancellationToken);
            if (!snapshot.IsConnected || !string.Equals(snapshot.Ssid, settings.TargetSsid, StringComparison.Ordinal))
            {
                SetState(AuthenticationState.WaitingForTargetWifi);
                return;
            }

            SetState(AuthenticationState.CheckingConnectivity);
            if ((await _probe.CheckAsync(settings.ProbeUri, settings.ProbeTimeout, cancellationToken)).IsOnline)
            {
                SetState(AuthenticationState.Online);
                return;
            }

            for (var attempt = 1; attempt <= settings.MaximumAttempts; attempt++)
            {
                SetState(AuthenticationState.Authenticating);
                var result = await _runner.AuthenticateAsync(settings, new(attempt, settings.MaximumAttempts, attempt == settings.MaximumAttempts), cancellationToken);
                switch (result.Outcome)
                {
                    case AuthenticationOutcome.Succeeded:
                        SetState(AuthenticationState.Online);
                        return;
                    case AuthenticationOutcome.CredentialsRequired:
                        SetState(AuthenticationState.WaitingForCredentials);
                        return;
                    case AuthenticationOutcome.RecordingRequired:
                        SetState(AuthenticationState.ActionRequired);
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

    private void SetState(AuthenticationState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }
}
