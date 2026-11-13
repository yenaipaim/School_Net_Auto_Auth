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
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            InstallerLog.Write(
                "installer unhandled exception",
                eventArgs.ExceptionObject as Exception
                    ?? new InvalidOperationException(eventArgs.ExceptionObject?.ToString() ?? "未知错误。"));
        };

        try
        {
            InstallerLog.Write("installer process start");
            Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            InstallerMetrics.MeasureDpi();
            Forms.Application.EnableVisualStyles();
            Forms.Application.SetCompatibleTextRenderingDefault(false);
            Forms.Application.SetUnhandledExceptionMode(Forms.UnhandledExceptionMode.CatchException);
            Forms.Application.ThreadException += (_, eventArgs) =>
                InstallerLog.Write("installer UI exception", eventArgs.Exception);

            if (args.Any(argument => argument.Equals("--silent", StringComparison.OrdinalIgnoreCase)))
            {
                RunSilentInstall();
                return;
            }

            Forms.Application.Run(new InstallerWindow());
        }
        catch (Exception ex)
        {
            InstallerLog.Write("installer startup failed", ex);
            Forms.MessageBox.Show(
                $"安装程序启动失败。\n\n{ex.Message}\n\n错误详情已写入 installer.log。",
                AppName,
                Forms.MessageBoxButtons.OK,
                Forms.MessageBoxIcon.Error);
        }
    }

    private static void RunSilentInstall()
    {
        try
        {
            Install(new InstallRequest(DefaultInstallDirectory(), LaunchAfterInstall: false),
                new Progress<InstallProgress>(_ => { }), CancellationToken.None);
            Environment.ExitCode = 0;
        }
        catch (Exception ex)
        {
            InstallerLog.Write("silent install failed", ex);
            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// 解压 + 目录替换 + 注册 + 快捷方式。取消语义：
    /// 替换目录之前取消 = 删 staging；替换之后取消 = 反向把 backup 换回来。
    /// </summary>
    internal static void Install(InstallRequest request, IProgress<InstallProgress> progress, CancellationToken cancellationToken)
    {
        var target = Path.GetFullPath(request.TargetDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(target) ?? throw new InvalidOperationException("无法确定安装目录。");
        var staging = Path.Combine(parent, $".staging-{Guid.NewGuid():N}");
        var backup = Path.Combine(parent, $".backup-{Guid.NewGuid():N}");
        var targetExisted = Directory.Exists(target);
        var registrySnapshot = UninstallRegistrySnapshot.Capture();
        var shortcuts = new List<string>();
        var previousInstallMoved = false;
        var replaced = false;

        InstallerLog.Write($"install start target=\"{target}\"");
        try
        {
            progress.Report(new(0, "正在准备安装目录...", InstallStage.Prepare));
            Directory.CreateDirectory(parent);
            StopExistingApplication(target);
            CleanupStaleStaging(parent);
            Directory.CreateDirectory(staging);
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(new(5, "安装目录准备完成。", InstallStage.Prepare));

            using var payload = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("SchoolNetAutoAuth.payload.zip")
                ?? throw new InvalidDataException("安装包内容缺失。");
            using var archive = new ZipArchive(payload, ZipArchiveMode.Read);
            ExtractArchive(archive, staging, progress, cancellationToken);

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

            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(new(76, "正在安全替换旧版本...", InstallStage.Replace));
            if (targetExisted)
            {
                Directory.Move(target, backup);
                previousInstallMoved = true;
            }

            Directory.Move(staging, target);
            replaced = true;
            var appExe = Path.Combine(target, ExeName);

            // 目录移动是原子的，取消只在移动完成后生效：随后整段反向回滚。
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(new(80, "正在写入卸载信息...", InstallStage.Register));
            WriteUninstallRegistration(appExe, applicationVersion);

            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(new(88, "正在创建快捷方式...", InstallStage.Register));
            CreateShortcuts(appExe, shortcuts);

            if (request.LaunchAfterInstall)
            {
                progress.Report(new(96, "正在启动主程序...", InstallStage.Launch));
                StartApplication(appExe);
            }

            progress.Report(new(94, "正在清理旧版本备份...", InstallStage.Register));
            TryDeleteDirectory(backup);
            progress.Report(new(100, "安装完成。", InstallStage.Launch));
            InstallerLog.Write($"install complete version={applicationVersion}");
        }
        catch
        {
            RollBackInstall(target, backup, previousInstallMoved, replaced, shortcuts, registrySnapshot);
            throw;
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    private static void ExtractArchive(
        ZipArchive archive,
        string target,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken)
    {
        var entries = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToArray();
        var targetFull = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        for (var index = 0; index < entries.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = entries[index];
            var destination = Path.GetFullPath(Path.Combine(target, entry.FullName));
            if (!destination.StartsWith(targetFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("安装包包含非法路径。");

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
            var percent = 5 + (int)((index + 1) * 70.0 / entries.Length);
            progress.Report(new(percent, $"正在解压程序文件（{index + 1}/{entries.Length}）...",
                InstallStage.Extract, index + 1, entries.Length));
        }
    }

    /// <summary>卸载信息写到 HKCU；写入前的旧值进快照，回滚时恢复。</summary>
    private static void WriteUninstallRegistration(string appExe, string applicationVersion)
    {
        var registration = InstallRegistration.Create(appExe);
        using var uninstall = Registry.CurrentUser.CreateSubKey(UninstallKeyPath)
            ?? throw new InvalidOperationException("无法写入卸载信息。");
        uninstall.SetValue("DisplayName", AppName);
        uninstall.SetValue("DisplayVersion", applicationVersion);
        uninstall.SetValue("Publisher", "SchoolNetAutoAuth");
        uninstall.SetValue("InstallLocation", Path.GetDirectoryName(appExe) ?? DefaultInstallDirectory());
        uninstall.SetValue("DisplayIcon", appExe);
        uninstall.SetValue("UninstallString", registration.UninstallString);
        uninstall.SetValue("QuietUninstallString", registration.QuietUninstallString);
        uninstall.SetValue("NoModify", 1, RegistryValueKind.DWord);
        uninstall.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    /// <summary>每创建一个就登记路径：中途失败时回滚也能把已生成的 .lnk 删干净。</summary>
    private static void CreateShortcuts(string appExe, ICollection<string> created)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("无法创建快捷方式。");
        dynamic shell = Activator.CreateInstance(shellType)!;
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var paths = new[]
        {
            Path.Combine(programs, AppName + ".lnk"),
            Path.Combine(programs, "卸载" + AppName + ".lnk"),
            Path.Combine(desktop, AppName + ".lnk")
        };

        CreateShortcut(shell, paths[0], appExe, string.Empty, Path.GetDirectoryName(appExe), AppName, created);
        CreateShortcut(shell, paths[1], appExe, "--uninstall", Path.GetDirectoryName(appExe), "卸载" + AppName, created);
        CreateShortcut(shell, paths[2], appExe, string.Empty, Path.GetDirectoryName(appExe), AppName, created);
    }

    private static void CreateShortcut(dynamic shell, string path, string target, string arguments,
        string? workingDirectory, string description, ICollection<string> created)
    {
        created.Add(path);
        dynamic shortcut = shell.CreateShortcut(path);
        shortcut.TargetPath = target;
        if (arguments.Length > 0) shortcut.Arguments = arguments;
        shortcut.IconLocation = target + ",0";
        if (!string.IsNullOrEmpty(workingDirectory)) shortcut.WorkingDirectory = workingDirectory;
        shortcut.Description = description;
        shortcut.Save();
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
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                // 正在退出或权限不足时继续：替换目录会再失败一次并走回滚。
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void RollBackInstall(
        string target,
        string backup,
        bool previousInstallMoved,
        bool replaced,
        IReadOnlyList<string> shortcuts,
        UninstallRegistrySnapshot registrySnapshot)
    {
        InstallerLog.Write("install rollback");
        StopExistingApplication(target);
        if (replaced) TryDeleteDirectory(target);
        if (previousInstallMoved && Directory.Exists(backup))
        {
            try { Directory.Move(backup, target); }
            catch (Exception ex) { InstallerLog.Write("rollback restore failed", ex); }
        }

        foreach (var shortcut in shortcuts)
        {
            try { if (File.Exists(shortcut)) File.Delete(shortcut); }
            catch (Exception ex) { InstallerLog.Write($"shortcut cleanup failed path=\"{shortcut}\"", ex); }
        }

        registrySnapshot.TryRestore();
    }

    private static void StartApplication(string appExe)
    {
        var application = Process.Start(new ProcessStartInfo(appExe) { UseShellExecute = true })
            ?? throw new InvalidOperationException("无法启动主程序。");
        if (application.WaitForExit(3000))
            throw new InvalidOperationException($"主程序启动失败，退出代码：{application.ExitCode}。");
    }

    private static string? ReadInstalledVersion(string target)
    {
        var installedExe = Path.Combine(target, ExeName);
        if (File.Exists(installedExe))
            return InstallVersionPolicy.ResolveApplicationVersion(installedExe);

        using var uninstall = Registry.CurrentUser.OpenSubKey(UninstallKeyPath);
        return uninstall?.GetValue("DisplayVersion") as string;
    }

    private static void CleanupStaleStaging(string parent)
    {
        try
        {
            if (!Directory.Exists(parent)) return;
            foreach (var directory in Directory.EnumerateDirectories(parent, ".staging-*"))
            {
                if (DateTime.UtcNow - Directory.GetLastWriteTimeUtc(directory) <= TimeSpan.FromHours(24)) continue;
                TryDeleteDirectory(directory);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InstallerLog.Write("staging cleanup failed", ex);
        }
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

    internal static string DefaultInstallDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        "SchoolNetAutoAuth");

    internal static string? ReadInstallLocation()
    {
        using var uninstall = Registry.CurrentUser.OpenSubKey(UninstallKeyPath);
        var value = uninstall?.GetValue("InstallLocation") as string;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

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
