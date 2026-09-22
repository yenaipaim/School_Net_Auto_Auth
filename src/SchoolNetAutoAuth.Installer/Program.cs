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
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SchoolNetAutoAuth";

    [STAThread]
    private static void Main(string[] args)
    {
        Forms.Application.EnableVisualStyles();
        Forms.Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallProgressForm());
    }

    internal static void Install(Action<InstallProgress> report)
    {
        var target = InstallDirectory();
        var parent = Path.GetDirectoryName(target) ?? throw new InvalidOperationException("无法确定安装目录。");
        var staging = Path.Combine(parent, $".staging-{Guid.NewGuid():N}");
        var backup = Path.Combine(parent, $".backup-{Guid.NewGuid():N}");
        var targetExisted = Directory.Exists(target);
        var registrySnapshot = UninstallRegistrySnapshot.Capture();

        InstallerLog.Write($"install start target=\"{target}\"");
        try
        {
            report(new(5, "正在准备安装目录..."));
            Directory.CreateDirectory(parent);
            StopExistingApplication(target);
            Directory.CreateDirectory(staging);

            report(new(15, "正在解压程序文件..."));
            using var payload = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SchoolNetAutoAuth.payload.zip")
                ?? throw new InvalidDataException("安装包内容缺失。");
            using var archive = new ZipArchive(payload, ZipArchiveMode.Read);
            ExtractArchive(archive, staging, report);

            var stagedExe = Path.Combine(staging, ExeName);
            if (!File.Exists(stagedExe))
                throw new FileNotFoundException("安装包中缺少主程序。", stagedExe);

            var applicationVersion = InstallVersionPolicy.ResolveApplicationVersion(stagedExe);
            var installedVersion = ReadInstalledVersion(target);
            if (InstallVersionPolicy.IsDowngrade(installedVersion, applicationVersion))
            {
                throw new InvalidOperationException(
                    $"已安装更高版本 {installedVersion}，不能降级到 {applicationVersion}。");
            }

            report(new(76, "正在安全替换旧版本..."));
            var previousInstallMoved = false;
            if (targetExisted)
            {
                Directory.Move(target, backup);
                previousInstallMoved = true;
            }

            try
            {
                Directory.Move(staging, target);
                var appExe = Path.Combine(target, ExeName);

                report(new(80, "正在注册开机启动..."));
                RegisterStartup(appExe);
                report(new(86, "正在写入卸载信息..."));
                WriteUninstallRegistration(appExe, applicationVersion);
                report(new(92, "正在创建开始菜单快捷方式..."));
                CreateShortcuts(appExe);
                report(new(96, "正在启动主程序..."));
                StartApplication(appExe);
            }
            catch (Exception installError)
            {
                InstallerLog.Write("install rollback", installError);
                RollBackInstall(target, backup, previousInstallMoved);
                registrySnapshot.TryRestore();
                throw;
            }

            TryDeleteDirectory(backup);
            InstallerLog.Write($"install complete version={applicationVersion}");
        }
        catch (Exception ex)
        {
            InstallerLog.Write("install failed", ex);
            throw;
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    private static void ExtractArchive(ZipArchive archive, string target, Action<InstallProgress> report)
    {
        var entries = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToArray();
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            var destination = Path.GetFullPath(Path.Combine(target, entry.FullName));
            if (!destination.StartsWith(
                    Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("安装包包含非法路径。");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
            report(new(
                15 + (int)((index + 1) * 60.0 / entries.Length),
                $"正在解压程序文件（{index + 1}/{entries.Length}）..."));
        }
    }

    private static void StopExistingApplication(string installDirectory)
    {
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExeName)))
        {
            try
            {
                if (!string.Equals(
                        process.MainModule?.FileName,
                        Path.Combine(installDirectory, ExeName),
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void RollBackInstall(string target, string backup, bool previousInstallMoved)
    {
        StopExistingApplication(target);
        TryDeleteDirectory(target);
        if (previousInstallMoved && Directory.Exists(backup))
            Directory.Move(backup, target);
    }

    private static void StartApplication(string appExe)
    {
        var application = Process.Start(new ProcessStartInfo(appExe) { UseShellExecute = true })
            ?? throw new InvalidOperationException("无法启动主程序。");
        if (application.WaitForExit(3000))
            throw new InvalidOperationException($"主程序启动失败，退出代码：{application.ExitCode}。");
    }

    private static void WriteUninstallRegistration(string appExe, string applicationVersion)
    {
        var registration = InstallRegistration.Create(appExe);
        using var uninstall = Registry.CurrentUser.CreateSubKey(UninstallKeyPath)
            ?? throw new InvalidOperationException("无法写入卸载信息。");
        uninstall.SetValue("DisplayName", AppName);
        uninstall.SetValue("DisplayVersion", applicationVersion);
        uninstall.SetValue("Publisher", "SchoolNetAutoAuth");
        uninstall.SetValue("InstallLocation", Path.GetDirectoryName(appExe) ?? InstallDirectory());
        uninstall.SetValue("DisplayIcon", appExe);
        uninstall.SetValue("UninstallString", registration.UninstallString);
        uninstall.SetValue("QuietUninstallString", registration.QuietUninstallString);
        uninstall.SetValue("NoModify", 1, RegistryValueKind.DWord);
        uninstall.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static string? ReadInstalledVersion(string target)
    {
        var installedExe = Path.Combine(target, ExeName);
        if (File.Exists(installedExe))
            return InstallVersionPolicy.ResolveApplicationVersion(installedExe);

        using var uninstall = Registry.CurrentUser.OpenSubKey(UninstallKeyPath);
        return uninstall?.GetValue("DisplayVersion") as string;
    }

    private static void CreateShortcuts(string appExe)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("无法创建开始菜单快捷方式。");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk"));
        shortcut.TargetPath = appExe;
        shortcut.IconLocation = appExe + ",0";
        shortcut.WorkingDirectory = Path.GetDirectoryName(appExe);
        shortcut.Description = AppName;
        shortcut.Save();

        dynamic uninstallShortcut = shell.CreateShortcut(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "卸载" + AppName + ".lnk"));
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
                using var oldRun = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run",
                    writable: true);
                oldRun?.DeleteValue("CampusNetworkAutoAuth", throwOnMissingValue: false);
                return;
            }
        }
        catch
        {
            // The registry Run key is the compatibility fallback.
        }

        using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")
            ?? throw new InvalidOperationException("无法注册开机启动。");
        run.SetValue("CampusNetworkAutoAuth", $"\"{appExe}\" --background");
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            InstallerLog.Write($"cleanup failed path=\"{path}\"", ex);
        }
    }

    private static string InstallDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        "SchoolNetAutoAuth");

    private sealed class UninstallRegistrySnapshot
    {
        private readonly bool _existed;
        private readonly IReadOnlyDictionary<string, RegistryValueSnapshot> _values;

        private UninstallRegistrySnapshot(bool existed, IReadOnlyDictionary<string, RegistryValueSnapshot> values)
        {
            _existed = existed;
            _values = values;
        }

        public static UninstallRegistrySnapshot Capture()
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallKeyPath);
            if (key is null)
                return new(false, new Dictionary<string, RegistryValueSnapshot>(StringComparer.Ordinal));

            var values = new Dictionary<string, RegistryValueSnapshot>(StringComparer.Ordinal);
            foreach (var name in key.GetValueNames())
            {
                var value = key.GetValue(name);
                if (value is not null)
                    values[name] = new(value, key.GetValueKind(name));
            }
            return new(true, values);
        }

        public void TryRestore()
        {
            try
            {
                if (!_existed)
                {
                    Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);
                    return;
                }

                using var key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath)
                    ?? throw new InvalidOperationException("无法恢复卸载信息。");
                foreach (var name in key.GetValueNames().Where(name => !_values.ContainsKey(name)).ToArray())
                    key.DeleteValue(name, throwOnMissingValue: false);
                foreach (var pair in _values)
                {
                    var value = pair.Value;
                    if (value.Kind == RegistryValueKind.Unknown)
                        key.SetValue(pair.Key, value.Value);
                    else
                        key.SetValue(pair.Key, value.Value, value.Kind);
                }
            }
            catch (Exception ex)
            {
                InstallerLog.Write("uninstall registry rollback failed", ex);
            }
        }

        private sealed record RegistryValueSnapshot(object Value, RegistryValueKind Kind);
    }
}
