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
        try
        {
            if (args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase)) Uninstall();
            else Install();
        }
        catch (Exception ex) { Forms.MessageBox.Show($"操作失败：{ex.Message}", AppName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error); }
    }

    private static void Install()
    {
        var target = InstallDirectory();
        Directory.CreateDirectory(target);
        using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("SchoolNetAutoAuth.payload.zip") ?? throw new InvalidDataException("安装包内容缺失。");
        using var archive = new ZipArchive(payload, ZipArchiveMode.Read);
        archive.ExtractToDirectory(target, overwriteFiles: true);

        var installedSetup = Path.Combine(target, "Uninstall.exe");
        File.Copy(Environment.ProcessPath ?? throw new InvalidOperationException("无法确定安装器路径。"), installedSetup, overwrite: true);
        var appExe = Path.Combine(target, ExeName);
        using (var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            run.SetValue("CampusNetworkAutoAuth", $"\"{appExe}\" --background");
        using (var uninstall = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\SchoolNetAutoAuth"))
        {
            uninstall.SetValue("DisplayName", AppName);
            uninstall.SetValue("DisplayVersion", "0.1.0");
            uninstall.SetValue("Publisher", "SchoolNetAutoAuth");
            uninstall.SetValue("InstallLocation", target);
            uninstall.SetValue("UninstallString", $"\"{installedSetup}\" --uninstall");
            uninstall.SetValue("NoModify", 1, RegistryValueKind.DWord);
            uninstall.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
        CreateShortcut(appExe);
        Process.Start(new ProcessStartInfo(appExe) { UseShellExecute = true });
        Forms.MessageBox.Show("安装完成，程序已启动并驻留系统托盘。", AppName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
    }

    private static void Uninstall()
    {
        using (var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true)) run?.DeleteValue("CampusNetworkAutoAuth", false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\SchoolNetAutoAuth", false);
        var shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");
        if (File.Exists(shortcut)) File.Delete(shortcut);
        var directory = InstallDirectory();
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"{directory}\"") { CreateNoWindow = true, UseShellExecute = false });
        Forms.MessageBox.Show("卸载已开始。用户配置和 Edge 专用登录状态仍保留在本地应用数据目录。", AppName, Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
    }

    private static void CreateShortcut(string appExe)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("无法创建开始菜单快捷方式。");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk"));
        shortcut.TargetPath = appExe;
        shortcut.IconLocation = appExe + ",0";
        shortcut.WorkingDirectory = Path.GetDirectoryName(appExe);
        shortcut.Description = AppName;
        shortcut.Save();
    }

    private static string InstallDirectory() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "SchoolNetAutoAuth");
}
