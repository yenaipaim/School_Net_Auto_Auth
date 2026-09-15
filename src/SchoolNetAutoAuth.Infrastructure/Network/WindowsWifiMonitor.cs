using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using SchoolNetAutoAuth.Core.Contracts;

namespace SchoolNetAutoAuth.Infrastructure.Network;

public sealed class WindowsWifiMonitor : INetworkMonitor
{
    public Task<NetworkSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ReadSnapshot());
    }

    private static NetworkSnapshot ReadSnapshot()
    {
        var result = Native.WlanOpenHandle(2, IntPtr.Zero, out _, out var rawHandle);
        if (result != 0) return new(null, false);
        using var handle = new WlanHandle(rawHandle);
        result = Native.WlanEnumInterfaces(handle, IntPtr.Zero, out var listPointer);
        if (result != 0) return new(null, false);
        try
        {
            var count = Marshal.ReadInt32(listPointer);
            var offset = 8;
            var size = Marshal.SizeOf<Native.WlanInterfaceInfo>();
            for (var i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<Native.WlanInterfaceInfo>(IntPtr.Add(listPointer, offset + i * size));
                if (info.State != Native.WlanInterfaceState.Connected) continue;
                result = Native.WlanQueryInterface(handle, ref info.Guid, Native.WlanIntfOpcode.CurrentConnection, IntPtr.Zero, out var dataSize, out var dataPointer, out _);
                if (result != 0 || dataSize == 0) continue;
                try
                {
                    var connection = Marshal.PtrToStructure<Native.WlanConnectionAttributes>(dataPointer);
                    var ssid = Encoding.UTF8.GetString(connection.AssociationAttributes.Dot11Ssid.Ssid, 0, (int)connection.AssociationAttributes.Dot11Ssid.Length);
                    return new(ssid, true);
                }
                finally { Native.WlanFreeMemory(dataPointer); }
            }
            return new(null, false);
        }
        finally { Native.WlanFreeMemory(listPointer); }
    }

    private sealed class WlanHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public WlanHandle(IntPtr rawHandle) : base(true) => SetHandle(rawHandle);

        protected override bool ReleaseHandle() => Native.WlanCloseHandle(handle, IntPtr.Zero) == 0;
    }

    private static class Native
    {
        internal enum WlanInterfaceState { NotReady, Connected, AdHocNetworkFormed, Disconnecting, Disconnected, Associating, Discovering, Authenticating }
        internal enum WlanIntfOpcode { AutoconfStart = 0, AutoconfEnabled = 1, BackgroundScanEnabled = 2, MediaStreamingMode = 3, RadioState = 4, BssType = 5, InterfaceState = 6, CurrentConnection = 7 }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WlanInterfaceInfo { internal Guid Guid; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] internal string Description; internal WlanInterfaceState State; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Dot11Ssid { internal uint Length; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] internal byte[] Ssid; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct WlanAssociationAttributes { internal Dot11Ssid Dot11Ssid; internal int BssType; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] internal byte[] Bssid; internal int PhyType; internal uint PhyIndex; internal uint SignalQuality; internal uint RxRate; internal uint TxRate; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WlanConnectionAttributes { internal WlanInterfaceState State; internal int ConnectionMode; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] internal string ProfileName; internal WlanAssociationAttributes AssociationAttributes; internal int SecurityEnabled; internal int OneXEnabled; internal int AuthAlgorithm; internal int CipherAlgorithm; }

        [DllImport("wlanapi.dll")] internal static extern int WlanOpenHandle(uint clientVersion, IntPtr reserved, out uint negotiatedVersion, out IntPtr handle);
        [DllImport("wlanapi.dll")] internal static extern int WlanCloseHandle(IntPtr handle, IntPtr reserved);
        [DllImport("wlanapi.dll")] internal static extern int WlanEnumInterfaces(WlanHandle handle, IntPtr reserved, out IntPtr interfaceList);
        [DllImport("wlanapi.dll")] internal static extern int WlanQueryInterface(WlanHandle handle, ref Guid interfaceGuid, WlanIntfOpcode opcode, IntPtr reserved, out uint dataSize, out IntPtr data, out int opcodeValueType);
        [DllImport("wlanapi.dll")] internal static extern void WlanFreeMemory(IntPtr memory);
    }
}
