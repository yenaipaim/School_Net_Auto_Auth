using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace SchoolNetAutoAuth.Installer;

internal static class Program
{
    private const string AppName = "校园网自动认证";
    private const string ExeName = "SchoolNetAutoAuth.App.exe";

    [STAThread]
    private static void Main(string[] args)
    {
        Forms.Application.EnableVisualStyles();
        Forms.Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallProgressForm());
    }

    internal static void Install(Action<InstallProgress> report)
    {
        report(new(5, "正在准备安装目录..."));
        var target = InstallDirectory();
        StopExistingApplication(target);
        Directory.CreateDirectory(target);
        report(new(15, "正在解压程序文件..."));
        using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("SchoolNetAutoAuth.payload.zip") ?? throw new InvalidDataException("安装包内容缺失。");
        using var archive = new ZipArchive(payload, ZipArchiveMode.Read);
        ExtractArchive(archive, target, report);

        var appExe = Path.Combine(target, ExeName);
        if (!File.Exists(appExe)) throw new FileNotFoundException("安装包中缺少主程序。", appExe);
        var registration = InstallRegistration.Create(appExe);
        report(new(78, "正在注册开机启动..."));
        RegisterStartup(appExe);
        report(new(86, "正在写入卸载信息..."));
        using (var uninstall = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\SchoolNetAutoAuth"))
        {
            uninstall.SetValue("DisplayName", AppName);
            uninstall.SetValue("DisplayVersion", "0.1.0");
            uninstall.SetValue("Publisher", "SchoolNetAutoAuth");
            uninstall.SetValue("InstallLocation", target);
            uninstall.SetValue("DisplayIcon", appExe);
            uninstall.SetValue("UninstallString", registration.UninstallString);
            uninstall.SetValue("QuietUninstallString", registration.QuietUninstallString);
            uninstall.SetValue("NoModify", 1, RegistryValueKind.DWord);
            uninstall.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
        report(new(92, "正在创建开始菜单快捷方式..."));
        CreateShortcuts(appExe);
        report(new(96, "正在启动主程序..."));
        var application = Process.Start(new ProcessStartInfo(appExe) { UseShellExecute = true })
            ?? throw new InvalidOperationException("无法启动主程序。");
        if (application.WaitForExit(3000))
            throw new InvalidOperationException($"主程序启动失败，退出代码：{application.ExitCode}。");
    }

    private static void ExtractArchive(ZipArchive archive, string target, Action<InstallProgress> report)
    {
        var entries = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToArray();
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var destination = Path.GetFullPath(Path.Combine(target, entry.FullName));
            if (!destination.StartsWith(Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("安装包包含非法路径。");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
            report(new(15 + (int)((index + 1) * 60.0 / entries.Length), $"正在解压程序文件（{index + 1}/{entries.Length}）..."));
        }
    }

    private static void StopExistingApplication(string installDirectory)
    {
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExeName)))
        {
            try
            {
                if (!string.Equals(process.MainModule?.FileName, Path.Combine(installDirectory, ExeName), StringComparison.OrdinalIgnoreCase))
                    continue;
                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void CreateShortcuts(string appExe)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("无法创建开始菜单快捷方式。");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk"));
        shortcut.TargetPath = appExe;
        shortcut.IconLocation = appExe + ",0";
        shortcut.WorkingDirectory = Path.GetDirectoryName(appExe);
        shortcut.Description = AppName;
        shortcut.Save();

        dynamic uninstallShortcut = shell.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "卸载" + AppName + ".lnk"));
        uninstallShortcut.TargetPath = appExe;
        uninstallShortcut.Arguments = "--uninstall";
        uninstallShortcut.IconLocation = appExe + ",0";
        uninstallShortcut.WorkingDirectory = Path.GetDirectoryName(appExe);
        uninstallShortcut.Description = "卸载" + AppName;
        uninstallShortcut.Save();
    }

    private static void RegisterStartup(string appExe)
    {
        try
        {
            var startInfo = new ProcessStartInfo("schtasks.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("/Create");
            startInfo.ArgumentList.Add("/TN");
            startInfo.ArgumentList.Add("SchoolNetAutoAuth");
            startInfo.ArgumentList.Add("/SC");
            startInfo.ArgumentList.Add("ONLOGON");
            startInfo.ArgumentList.Add("/RL");
            startInfo.ArgumentList.Add("LIMITED");
            startInfo.ArgumentList.Add("/F");
            startInfo.ArgumentList.Add("/TR");
            startInfo.ArgumentList.Add($"\"{appExe}\" --background");
            using var process = Process.Start(startInfo);
            if (process is null) throw new InvalidOperationException("无法启动任务计划程序。");
            process.WaitForExit();
            if (process.ExitCode == 0)
            {
                using var oldRun = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
                oldRun?.DeleteValue("CampusNetworkAutoAuth", throwOnMissingValue: false);
                return;
            }
        }
        catch { }

        using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        run.SetValue("CampusNetworkAutoAuth", $"\"{appExe}\" --background");
    }

    private static string InstallDirectory() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "SchoolNetAutoAuth");
}
