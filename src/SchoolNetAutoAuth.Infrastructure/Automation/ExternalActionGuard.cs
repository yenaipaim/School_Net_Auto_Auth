using SchoolNetAutoAuth.Core.Authentication;
using SchoolNetAutoAuth.Core.Contracts;

namespace SchoolNetAutoAuth.Infrastructure.Automation;

public sealed class ExternalActionGuard(IConnectivityProbe probe)
{
    public async Task<AuthenticationResult?> CheckAsync(
        IExternalActionObserver observer,
        Uri probeUri,
        TimeSpan probeTimeout,
        CancellationToken cancellationToken,
        bool probeWithoutDetection = true)
    {
        if (probeWithoutDetection && (await probe.CheckAsync(probeUri, probeTimeout, cancellationToken)).IsOnline)
            return AuthenticationResult.Success();
        var detection = await observer.InspectAsync(cancellationToken);
        if (detection is null) return null;
        if (!probeWithoutDetection && (await probe.CheckAsync(probeUri, probeTimeout, cancellationToken)).IsOnline)
            return AuthenticationResult.Success();
        var reason = detection.Kind == ExternalActionKind.PhoneVerification ? "phone_verification" : "device_limit";
        return new(AuthenticationOutcome.ExternalActionRequired, reason, detection.Kind, detection.SafeExcerpt);
    }
}
