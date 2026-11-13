namespace SchoolNetAutoAuth.Core.Contracts;

/// <summary>一条附近 Wi-Fi 记录。<paramref name="SignalQuality"/> 为 0–100 的百分比。</summary>
public sealed record WifiNetwork(string Ssid, uint SignalQuality, bool IsSecured, bool IsConnected)
{
    /// <summary>可编辑 ComboBox 用 ToString 回填文本框，默认的记录格式会把整段属性塞进去。</summary>
    public override string ToString() => Ssid;
}

/// <summary>扫描附近 Wi-Fi。与 <see cref="INetworkMonitor"/> 分开：监控只读当前连接，扫描会主动触发网卡扫描。</summary>
public interface IWifiScanner
{
    Task<IReadOnlyList<WifiNetwork>> ScanAsync(CancellationToken cancellationToken);
}
