namespace SchoolNetAutoAuth.Core.Contracts;

public sealed record NetworkSnapshot(string? Ssid, bool IsConnected);

public interface INetworkMonitor
{
    Task<NetworkSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
}
