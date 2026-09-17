using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace SchoolNetAutoAuth.Uninstaller;

internal static class Program
{
    private const string AppName = "校园网自动认证";
    private const string CredentialTarget = "SchoolNetAutoAuth/Portal";

    [STAThread]
    private static void Main(string[] args)
    {
        Forms.Application.EnableVisualStyles();
        Forms.Application.SetCompatibleTextRenderingDefault(false);
        var quiet = args.Contains("--quiet", StringComparer.OrdinalIgnoreCase);
        var plan = UninstallPlan.CreateDefault(
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SchoolNetAutoAuth"));

        if (!quiet)
        {
            using var form = new UninstallForm();
            if (form.ShowDialog() != Forms.DialogResult.OK) return;
            plan = plan with { DeleteUserData = form.DeleteUserData };
        }

        try
        {
            Execute(plan);
            if (!quiet) Forms.MessageBox.Show("卸载完成。", AppName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
            ScheduleInstallDirectoryRemoval(plan.InstallDirectory);
        }
        catch (Exception ex)
        {
            if (quiet) Environment.ExitCode = 1;
            else Forms.MessageBox.Show($"卸载失败：{ex.Message}", AppName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
        }
    }

    internal static void Execute(UninstallPlan plan)
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

    private static void StopRunningApplication()
    {
        foreach (var process in Process.GetProcessesByName("SchoolNetAutoAuth.App"))
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

    private static void ScheduleInstallDirectoryRemoval(string installDirectory)
    {
        var normalized = Path.GetFullPath(installDirectory);
        var expectedRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs") + Path.DirectorySeparatorChar;
        if (!normalized.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("安装目录不在允许的用户程序目录中。");

        Process.Start(new ProcessStartInfo("cmd.exe", $"/d /c timeout /t 2 /nobreak >nul & rmdir /s /q \"{normalized}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetTempPath()
        });
    }

    private static void DeleteCredential()
    {
        if (!CredDelete(CredentialTarget, 1, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 1168) throw new Win32Exception(error);
        }
    }

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);
}
