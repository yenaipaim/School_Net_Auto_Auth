using System.ComponentModel;
using System.Runtime.InteropServices;
using SchoolNetAutoAuth.Core.Contracts;

namespace SchoolNetAutoAuth.Infrastructure.Credentials;

public sealed class WindowsCredentialStore(string target = "SchoolNetAutoAuth/Portal") : ICredentialStore
{
    public Task<PortalCredential?> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Native.CredRead(target, 1, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == 1168) return Task.FromResult<PortalCredential?>(null);
            throw new Win32Exception(error);
        }
        try
        {
            var credential = Marshal.PtrToStructure<Native.Credential>(pointer);
            var password = credential.CredentialBlobSize == 0 ? string.Empty : Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2) ?? string.Empty;
            return Task.FromResult<PortalCredential?>(new(credential.UserName ?? string.Empty, password));
        }
        finally { Native.CredFree(pointer); }
    }

    public Task WriteAsync(PortalCredential value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = System.Text.Encoding.Unicode.GetBytes(value.Password);
        var blob = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new Native.Credential { Type = 1, TargetName = target, CredentialBlobSize = (uint)bytes.Length, CredentialBlob = blob, Persist = 2, UserName = value.Username };
            if (!Native.CredWrite(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return Task.CompletedTask;
        }
        finally
        {
            Marshal.Copy(new byte[bytes.Length], 0, blob, bytes.Length);
            Marshal.FreeCoTaskMem(blob);
            Array.Clear(bytes);
        }
    }

    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Native.CredDelete(target, 1, 0) && Marshal.GetLastWin32Error() != 1168) throw new Win32Exception(Marshal.GetLastWin32Error());
        return Task.CompletedTask;
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct Credential { internal uint Flags; internal uint Type; internal string TargetName; internal string? Comment; internal System.Runtime.InteropServices.ComTypes.FILETIME LastWritten; internal uint CredentialBlobSize; internal IntPtr CredentialBlob; internal uint Persist; internal uint AttributeCount; internal IntPtr Attributes; internal string? TargetAlias; internal string UserName; }
        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool CredWrite(ref Credential credential, uint flags);
        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool CredDelete(string target, uint type, uint flags);
        [DllImport("advapi32.dll")] internal static extern void CredFree(IntPtr credential);
    }
}
