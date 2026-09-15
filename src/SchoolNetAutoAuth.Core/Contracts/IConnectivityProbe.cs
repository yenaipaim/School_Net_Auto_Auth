namespace SchoolNetAutoAuth.Core.Contracts;

public sealed record ConnectivityResult(bool IsOnline, string Reason);

public interface IConnectivityProbe
{
    Task<ConnectivityResult> CheckAsync(Uri probeUri, TimeSpan timeout, CancellationToken cancellationToken);
}
