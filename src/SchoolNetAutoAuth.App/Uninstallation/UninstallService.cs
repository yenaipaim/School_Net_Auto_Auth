using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SchoolNetAutoAuth.Infrastructure.Startup;

namespace SchoolNetAutoAuth.App.Uninstallation;

public static class UninstallService
{
    private const string AppName = "校园网自动认证";
    private const string CredentialTarget = "SchoolNetAutoAuth/Portal";

    public static void Execute(UninstallPlan plan)
    {
        StopRunningApplication();
        new StartupTaskManager(new RegistryStartupManager()).Remove();
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\SchoolNetAutoAuth", throwOnMissingSubKey: false);

        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        foreach (var shortcut in new[]
                 {
                     Path.Combine(programs, AppName + ".lnk"),
                     Path.Combine(programs, "卸载" + AppName + ".lnk"),
                     Path.Combine(desktop, AppName + ".lnk")
                 })
        {
            if (File.Exists(shortcut)) File.Delete(shortcut);
        }

        if (plan.DeleteCredential) DeleteCredential();
        if (plan.DeleteUserData && Directory.Exists(plan.UserDataDirectory))
            Directory.Delete(plan.UserDataDirectory, recursive: true);
    }

    /// <summary>
    /// 卸载只拒绝「明显不该删」的位置：根目录、网络位置、移动盘、Windows 目录。
    /// 不再限定 %LOCALAPPDATA%\Programs，否则自定义安装路径的用户永远卸不干净。
    /// </summary>
    public static string ValidateInstallDirectory(string installDirectory)
    {
        if (string.IsNullOrWhiteSpace(installDirectory))
            throw new InvalidOperationException("安装目录为空。");

        var normalized = Path.GetFullPath(installDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (normalized.Length > 240)
            throw new InvalidOperationException("安装目录过长，已拒绝自动删除。");
        if (normalized.StartsWith(@"\\", StringComparison.Ordinal))
            throw new InvalidOperationException("不支持网络位置上的安装目录。");

        var root = Path.GetPathRoot(normalized);
        if (string.IsNullOrEmpty(root)
            || normalized.Length <= root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length)
        {
            throw new InvalidOperationException("安装目录不能是磁盘根目录。");
        }

        if (new DriveInfo(root).DriveType != DriveType.Fixed)
            throw new InvalidOperationException("安装目录不在本地固定磁盘上。");

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (IsUnder(normalized, windows) || IsUnder(normalized, Environment.SystemDirectory))
            throw new InvalidOperationException("安装目录位于系统目录中。");

        return normalized;
    }

    private static bool IsUnder(string path, string parent) =>
        !string.IsNullOrEmpty(parent)
        && (path.Equals(parent, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));

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
