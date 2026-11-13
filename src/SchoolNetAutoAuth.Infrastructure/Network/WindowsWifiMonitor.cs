using SchoolNetAutoAuth.Core.Contracts;

namespace SchoolNetAutoAuth.Infrastructure.Network;

/// <summary>Windows 原生 WLAN 读取：既提供当前连接快照，也提供设置页用的附近网络扫描。</summary>
public sealed class WindowsWifiMonitor : INetworkMonitor, IWifiScanner
{
    public Task<NetworkSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(WlanNative.TryGetConnectedSsid(out var ssid) && !string.IsNullOrWhiteSpace(ssid)
            ? new NetworkSnapshot(ssid, true)
            : new NetworkSnapshot(null, false));
    }

    public Task<IReadOnlyList<WifiNetwork>> ScanAsync(CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<WifiNetwork>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return WlanNative.GetAvailableNetworks()
                .Select(network => new WifiNetwork(network.Ssid, network.SignalQuality, network.IsSecured, network.IsConnected))
                .ToArray();
        }, cancellationToken);
}
