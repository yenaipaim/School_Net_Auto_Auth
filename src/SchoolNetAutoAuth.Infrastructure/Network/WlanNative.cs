using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SchoolNetAutoAuth.Infrastructure.Network;

/// <summary>一条扫描结果。安装器会链接编译本文件，因此这里只允许依赖 BCL。</summary>
public sealed record WlanNetworkInfo(string Ssid, uint SignalQuality, bool IsSecured, bool IsConnected);

/// <summary>
/// wlanapi.dll 的最小封装：当前连接 SSID + 附近网络列表。
/// 单独成文件是为了让独立安装器也能链接编译，而不必引用整个 Infrastructure 程序集。
/// </summary>
public static class WlanNative
{
    private const int ClientVersion = 2;
    private const uint ConnectedFlag = 0x00000001;

    /// <summary>读取当前已连接的 Wi-Fi 名称。未连接或网卡不可用时返回 false。</summary>
    public static bool TryGetConnectedSsid(out string ssid)
    {
        ssid = string.Empty;
        if (WlanOpenHandle(ClientVersion, IntPtr.Zero, out _, out var rawHandle) != 0) return false;
        using var handle = new WlanHandle(rawHandle);
        if (WlanEnumInterfaces(handle, IntPtr.Zero, out var listPointer) != 0) return false;
        try
        {
            var count = Marshal.ReadInt32(listPointer);
            var size = Marshal.SizeOf<WlanInterfaceInfo>();
            for (var index = 0; index < count; index++)
            {
                var info = Marshal.PtrToStructure<WlanInterfaceInfo>(IntPtr.Add(listPointer, 8 + index * size));
                if (info.State != WlanInterfaceState.Connected) continue;
                if (WlanQueryInterface(handle, ref info.Guid, WlanIntfOpcode.CurrentConnection, IntPtr.Zero,
                        out var dataSize, out var dataPointer, out _) != 0 || dataSize == 0)
                {
                    continue;
                }

                try
                {
                    var connection = Marshal.PtrToStructure<WlanConnectionAttributes>(dataPointer);
                    var bytes = connection.AssociationAttributes.Dot11Ssid.Ssid;
                    var length = (int)Math.Min(connection.AssociationAttributes.Dot11Ssid.Length, (uint)bytes.Length);
                    var name = Encoding.UTF8.GetString(bytes, 0, length).TrimEnd('\0');
                    if (name.Length == 0) continue;
                    ssid = name;
                    return true;
                }
                finally
                {
                    WlanFreeMemory(dataPointer);
                }
            }

            return false;
        }
        finally
        {
            WlanFreeMemory(listPointer);
        }
    }

    /// <summary>
    /// 触发一次网卡扫描并轮询结果。WlanScan 本身是异步的，这里在超时窗口内反复取列表，
    /// 一旦拿到非空结果就提前返回，避免固定睡眠拖慢设置页。
    /// </summary>
    public static IReadOnlyList<WlanNetworkInfo> GetAvailableNetworks(TimeSpan? wait = null)
    {
        var interfaces = new List<Guid>();
        if (WlanOpenHandle(ClientVersion, IntPtr.Zero, out _, out var rawHandle) != 0) return [];
        using var handle = new WlanHandle(rawHandle);
        if (WlanEnumInterfaces(handle, IntPtr.Zero, out var listPointer) != 0) return [];
        try
        {
            var count = Marshal.ReadInt32(listPointer);
            var size = Marshal.SizeOf<WlanInterfaceInfo>();
            for (var index = 0; index < count; index++)
            {
                var info = Marshal.PtrToStructure<WlanInterfaceInfo>(IntPtr.Add(listPointer, 8 + index * size));
                interfaces.Add(info.Guid);
            }
        }
        finally
        {
            WlanFreeMemory(listPointer);
        }

        if (interfaces.Count == 0) return [];

        foreach (var guid in interfaces)
        {
            var target = guid;
            // 扫描失败（正在扫描、射频关闭）不阻断读取上一次的缓存结果。
            WlanScan(handle, ref target, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        }

        var deadline = DateTime.UtcNow + (wait ?? TimeSpan.FromSeconds(3));
        List<WlanNetworkInfo> networks = [];
        do
        {
            networks = QueryAvailableNetworks(handle, interfaces);
            if (networks.Count > 0) break;
            Thread.Sleep(250);
        }
        while (DateTime.UtcNow < deadline);

        return networks;
    }

    private static List<WlanNetworkInfo> QueryAvailableNetworks(WlanHandle handle, IEnumerable<Guid> interfaces)
    {
        var merged = new Dictionary<string, WlanNetworkInfo>(StringComparer.Ordinal);
        foreach (var guid in interfaces)
        {
            var target = guid;
            if (WlanGetAvailableNetworkList(handle, ref target, 0, IntPtr.Zero, out var listPointer) != 0) continue;
            try
            {
                var count = Marshal.ReadInt32(listPointer);
                var size = Marshal.SizeOf<WlanAvailableNetwork>();
                for (var index = 0; index < count; index++)
                {
                    var network = Marshal.PtrToStructure<WlanAvailableNetwork>(IntPtr.Add(listPointer, 8 + index * size));
                    var bytes = network.Dot11Ssid.Ssid;
                    var length = (int)Math.Min(network.Dot11Ssid.Length, (uint)bytes.Length);
                    var name = Encoding.UTF8.GetString(bytes, 0, length).TrimEnd('\0');
                    // 隐藏网络在列表里 SSID 为空，无法展示也无法让用户确认，直接丢掉。
                    if (name.Length == 0) continue;

                    var candidate = new WlanNetworkInfo(
                        name,
                        Math.Min(network.SignalQuality, 100),
                        network.SecurityEnabled != 0,
                        (network.Flags & ConnectedFlag) != 0);
                    if (!merged.TryGetValue(name, out var existing) || IsBetter(candidate, existing))
                        merged[name] = candidate;
                }
            }
            finally
            {
                WlanFreeMemory(listPointer);
            }
        }

        return merged.Values
            .OrderByDescending(network => network.IsConnected)
            .ThenByDescending(network => network.SignalQuality)
            .ThenBy(network => network.Ssid, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsBetter(WlanNetworkInfo candidate, WlanNetworkInfo existing) =>
        candidate.IsConnected != existing.IsConnected
            ? candidate.IsConnected
            : candidate.SignalQuality > existing.SignalQuality;

    private sealed class WlanHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal WlanHandle(IntPtr rawHandle) : base(ownsHandle: true) => SetHandle(rawHandle);

        protected override bool ReleaseHandle() => WlanCloseHandle(handle, IntPtr.Zero) == 0;
    }

    private enum WlanInterfaceState
    {
        NotReady,
        Connected,
        AdHocNetworkFormed,
        Disconnecting,
        Disconnected,
        Associating,
        Discovering,
        Authenticating
    }

    private enum WlanIntfOpcode
    {
        AutoconfStart = 0,
        AutoconfEnabled = 1,
        BackgroundScanEnabled = 2,
        MediaStreamingMode = 3,
        RadioState = 4,
        BssType = 5,
        InterfaceState = 6,
        CurrentConnection = 7
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WlanInterfaceInfo
    {
        internal Guid Guid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        internal string Description;
        internal WlanInterfaceState State;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Dot11Ssid
    {
        internal uint Length;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        internal byte[] Ssid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WlanAssociationAttributes
    {
        internal Dot11Ssid Dot11Ssid;
        internal int BssType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        internal byte[] Bssid;
        internal int PhyType;
        internal uint PhyIndex;
        internal uint SignalQuality;
        internal uint RxRate;
        internal uint TxRate;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WlanConnectionAttributes
    {
        internal WlanInterfaceState State;
        internal int ConnectionMode;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        internal string ProfileName;
        internal WlanAssociationAttributes AssociationAttributes;
        internal int SecurityEnabled;
        internal int OneXEnabled;
        internal int AuthAlgorithm;
        internal int CipherAlgorithm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WlanAvailableNetwork
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        internal string ProfileName;
        internal Dot11Ssid Dot11Ssid;
        internal int BssType;
        internal uint NumberOfBssids;
        internal int NetworkConnectable;
        internal uint NotConnectableReason;
        internal uint NumberOfPhyTypes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        internal int[] PhyTypes;
        internal int MorePhyTypes;
        internal uint SignalQuality;
        internal int SecurityEnabled;
        internal int DefaultAuthAlgorithm;
        internal int DefaultCipherAlgorithm;
        internal uint Flags;
        internal uint Reserved;
    }

    [DllImport("wlanapi.dll")]
    private static extern int WlanOpenHandle(uint clientVersion, IntPtr reserved, out uint negotiatedVersion, out IntPtr handle);

    [DllImport("wlanapi.dll")]
    private static extern int WlanCloseHandle(IntPtr handle, IntPtr reserved);

    [DllImport("wlanapi.dll")]
    private static extern int WlanEnumInterfaces(WlanHandle handle, IntPtr reserved, out IntPtr interfaceList);

    [DllImport("wlanapi.dll")]
    private static extern int WlanQueryInterface(
        WlanHandle handle,
        ref Guid interfaceGuid,
        WlanIntfOpcode opcode,
        IntPtr reserved,
        out uint dataSize,
        out IntPtr data,
        out int opcodeValueType);

    [DllImport("wlanapi.dll")]
    private static extern int WlanScan(
        WlanHandle handle,
        ref Guid interfaceGuid,
        IntPtr ssid,
        IntPtr ieData,
        IntPtr reserved);

    [DllImport("wlanapi.dll")]
    private static extern int WlanGetAvailableNetworkList(
        WlanHandle handle,
        ref Guid interfaceGuid,
        uint flags,
        IntPtr reserved,
        out IntPtr networkList);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr memory);
}
