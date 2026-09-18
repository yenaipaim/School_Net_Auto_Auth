using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SchoolNetAutoAuth.App.Uninstallation;

public static class UninstallService
{
    private const string AppName = "校园网自动认证";
    private const string CredentialTarget = "SchoolNetAutoAuth/Portal";

    public static void Execute(UninstallPlan plan)
    {
        StopRunningApplication();
        using (var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true))
            run?.DeleteValue("CampusNetworkAutoAuth", throwOnMissingValue: false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\SchoolNetAutoAuth", throwOnMissingSubKey: false);

        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        foreach (var shortcut in new[] { Path.Combine(programs, AppName + ".lnk"), Path.Combine(programs, "卸载" + AppName + ".lnk") })
            if (File.Exists(shortcut)) File.Delete(shortcut);

        if (plan.DeleteCredential) DeleteCredential();
        if (plan.DeleteUserData && Directory.Exists(plan.UserDataDirectory))
            Directory.Delete(plan.UserDataDirectory, recursive: true);
    }

    public static string ValidateInstallDirectory(string installDirectory)
    {
        var normalized = Path.GetFullPath(installDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var expectedRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!normalized.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("安装目录不在允许的用户程序目录中。");
        return normalized;
    }

    public static void ScheduleInstallDirectoryRemoval(string installDirectory)
    {
        var normalized = ValidateInstallDirectory(installDirectory);
        Process.Start(new ProcessStartInfo("cmd.exe", $"/d /c timeout /t 2 /nobreak >nul & rmdir /s /q \"{normalized}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetTempPath()
        });
    }

    private static void StopRunningApplication()
    {
        var currentId = Environment.ProcessId;
        foreach (var process in Process.GetProcessesByName("SchoolNetAutoAuth.App").Where(process => process.Id != currentId))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
            finally { process.Dispose(); }
        }
    }

    private static void DeleteCredential()
    {
        if (!Native.CredDelete(CredentialTarget, 1, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 1168) throw new Win32Exception(error);
        }
    }

    private static class Native
    {
        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CredDelete(string target, uint type, uint flags);
    }
}
