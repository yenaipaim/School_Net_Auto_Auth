using SchoolNetAutoAuth.Core.Contracts;
using Windows.Networking.Connectivity;

namespace SchoolNetAutoAuth.Infrastructure.Connectivity;

/// <summary>
/// 联网判定以 Windows 自身的连接状态为准，而不是"某个网站能打开"。
///
/// 判定顺序：
/// 1. 系统是否还有 Internet 连接配置（NetworkInformation）——没有就是断开；
/// 2. 系统给出的连通级别是否 InternetAccess —— 只有本地/受限访问都算未联网；
/// 3. 再做一次 HTTP 探测，用来识别"系统以为联网、实际被劫持到认证页"的校园网典型状态。
///
/// 前两步保证判定来自操作系统的真实网络状态（NCSI），第三步避免把认证页误判成联网。
/// </summary>
public sealed class SystemAwareConnectivityProbe : IConnectivityProbe, IDisposable
{
    private readonly HttpConnectivityProbe _httpProbe;

    public SystemAwareConnectivityProbe(HttpConnectivityProbe httpProbe) => _httpProbe = httpProbe;

    public async Task<ConnectivityResult> CheckAsync(Uri probeUri, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var system = ReadSystemState();
        if (!system.IsInternetAccess)
            return new ConnectivityResult(false, system.Reason);

        var http = await _httpProbe.CheckAsync(probeUri, timeout, cancellationToken).ConfigureAwait(false);
        return http.IsOnline
            ? new ConnectivityResult(true, $"system_internet+{http.Reason}")
            : new ConnectivityResult(false, http.Reason);
    }

    private static (bool IsInternetAccess, string Reason) ReadSystemState()
    {
        try
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            if (profile is null)
                return (false, "system_no_connection_profile");

            var level = profile.GetNetworkConnectivityLevel();
            return level switch
            {
                NetworkConnectivityLevel.InternetAccess => (true, "system_internet"),
                NetworkConnectivityLevel.ConstrainedInternetAccess => (false, "system_constrained_internet"),
                NetworkConnectivityLevel.LocalAccess => (false, "system_local_access_only"),
                _ => (false, "system_no_connectivity")
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            // 部分精简系统不暴露 NetworkInformation，退回纯 HTTP 判定而不是谎报离线。
            return (true, "system_state_unavailable");
        }
    }

    public void Dispose() => _httpProbe.Dispose();
}
