using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using SchoolNetAutoAuth.Infrastructure.Network;

namespace SchoolNetAutoAuth.Installer;

/// <summary>单窗口三态四页：欢迎 → 安装位置 → 进度 → 完成。左图创建一次，切页只换右栏。</summary>
internal sealed class InstallerWindow : Form
{
    private const string ExeName = "SchoolNetAutoAuth.App.exe";
    private const string CoverResourceName = "SchoolNetAutoAuth.installer-left.jpg";

    private readonly Panel _host;
    private readonly WelcomePage _welcome;
    private readonly LocationPage _location;
    private readonly ProgressPage _progress;
    private readonly CompletePage _complete;
    private InstallerPage? _current;
    private CancellationTokenSource? _cancellation;
    private string? _installedDirectory;
    private readonly string? _previousDirectory;

    internal InstallerWindow()
    {
        Text = "校园网自动认证 安装程序";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = InstallerTheme.Bg;
        DoubleBuffered = true;
        ClientSize = new Size(InstallerMetrics.Px(960), InstallerMetrics.Px(620));

        var cover = new CoverImageBox(LoadCoverImage())
        {
            Bounds = new Rectangle(0, 0, InstallerMetrics.Px(576), InstallerMetrics.Px(620))
        };
        Controls.Add(cover);

        var separator = new Panel
        {
            Bounds = new Rectangle(InstallerMetrics.Px(576) - 1, 0, 1, InstallerMetrics.Px(620)),
            BackColor = InstallerTheme.Border
        };
        Controls.Add(separator);
        separator.BringToFront();

        _host = new Panel
        {
            Bounds = new Rectangle(InstallerMetrics.Px(576), 0, InstallerMetrics.Px(384), InstallerMetrics.Px(620)),
            BackColor = InstallerTheme.Bg
        };
        Controls.Add(_host);

        _previousDirectory = Program.ReadInstallLocation();
        var initialDirectory = _previousDirectory ?? Program.DefaultInstallDirectory();
        var version = InstallerVersion();

        _welcome = new WelcomePage(version);
        _location = new LocationPage(initialDirectory);
        _progress = new ProgressPage();
        _complete = new CompletePage();

        foreach (var page in new InstallerPage[] { _welcome, _location, _progress, _complete })
        {
            page.Bounds = new Rectangle(0, 0, InstallerMetrics.Px(384), InstallerMetrics.Px(620));
            page.Visible = false;
            _host.Controls.Add(page);
        }

        WireEvents(version);
        ShowPage(_welcome);
    }

    private void WireEvents(string incomingVersion)
    {
        _welcome.NextRequested += (_, _) => ShowPage(_location);
        _location.BackRequested += (_, _) => ShowPage(_welcome);
        _location.InstallRequested += async (_, directory) => await RunInstallAsync(directory);
        _progress.CancelRequested += (_, _) => _cancellation?.Cancel();
        _complete.FinishRequested += (_, launch) => Finish(launch);

        var installedVersion = _previousDirectory is null
            ? null
            : InstallVersionPolicy.NormalizeDisplayVersion(ReadVersion(Path.Combine(_previousDirectory, ExeName)));
        if (installedVersion is not null
            && !string.Equals(installedVersion, incomingVersion, StringComparison.OrdinalIgnoreCase)
            && !InstallVersionPolicy.IsDowngrade(installedVersion, incomingVersion))
        {
            _welcome.UseUpgradeMode(installedVersion, incomingVersion);
        }
    }

    private async Task RunInstallAsync(string directory)
    {
        _cancellation = new CancellationTokenSource();
        _progress.Begin();
        ShowPage(_progress);

        var progress = new Progress<InstallProgress>(value => _progress.Report(value));
        try
        {
            await Task.Run(() => Program.Install(
                new InstallRequest(directory, LaunchAfterInstall: false), progress, _cancellation.Token));
            _progress.MarkComplete();
        }
        catch (OperationCanceledException)
        {
            ShowPage(_location);
            _location.SetMessage("已取消，未对系统做任何更改。", InstallerTheme.MutedFg);
            return;
        }
        catch (Exception ex)
        {
            InstallerLog.Write("install failed (ui)", ex);
            ShowPage(_location);
            _location.SetMessage("安装失败：" + ex.Message, InstallerTheme.Destructive);
            return;
        }
        finally
        {
            _progress.Stop();
            _cancellation.Dispose();
            _cancellation = null;
        }

        _installedDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        AskRemovePreviousDirectory();
        _complete.SetSummary(
            InstallVersionPolicy.NormalizeDisplayVersion(
                ReadVersion(Path.Combine(_installedDirectory, ExeName)) ?? InstallerVersion()),
            _installedDirectory,
            CurrentWifi(),
            ReadPortalText());
        ShowPage(_complete);
    }

    private void AskRemovePreviousDirectory()
    {
        if (_previousDirectory is null || _installedDirectory is null) return;
        if (string.Equals(
                Path.GetFullPath(_previousDirectory).TrimEnd(Path.DirectorySeparatorChar),
                _installedDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!Directory.Exists(_previousDirectory)) return;
        if (IsUnder(_installedDirectory, _previousDirectory) || IsUnder(_previousDirectory, _installedDirectory)) return;

        var result = MessageBox.Show(
            this,
            $"旧版本的安装目录仍然保留：\n{_previousDirectory}\n\n是否删除？\n网络设置、录制流程和课程表位于用户数据目录，不受影响。",
            "安装完成",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (result != DialogResult.Yes) return;

        try { Directory.Delete(_previousDirectory, recursive: true); }
        catch (Exception ex)
        {
            InstallerLog.Write($"previous directory cleanup failed path=\"{_previousDirectory}\"", ex);
            MessageBox.Show(this, "旧目录删除失败，可以稍后手动删除。", "安装完成",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void Finish(bool launch)
    {
        if (launch && _installedDirectory is not null)
        {
            var exe = Path.Combine(_installedDirectory, ExeName);
            if (File.Exists(exe))
            {
                try { Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true }); }
                catch (Exception ex) { InstallerLog.Write("launch after install failed", ex); }
            }
        }

        Close();
    }

    private void ShowPage(InstallerPage page)
    {
        if (ReferenceEquals(_current, page))
        {
            page.Visible = true;
            return;
        }

        if (_current is not null) _current.Visible = false;
        page.Visible = true;
        page.BringToFront();
        _current = page;
    }

    private static string CurrentWifi() =>
        WlanNative.TryGetConnectedSsid(out var ssid) && !string.IsNullOrWhiteSpace(ssid) ? ssid : "未连接";

    /// <summary>安装器只读 settings.json，用于完成页展示；绝不写入。</summary>
    private static string ReadPortalText()
    {
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SchoolNetAutoAuth",
            "settings.json");
        try
        {
            if (!File.Exists(settingsPath)) return "安装后在应用内配置";
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            if (document.RootElement.TryGetProperty("portalUri", out var portal)
                && portal.ValueKind == JsonValueKind.String
                && Uri.TryCreate(portal.GetString(), UriKind.Absolute, out var uri))
            {
                return uri.ToString();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            InstallerLog.Write("read settings for summary failed", ex);
        }

        return "安装后在应用内配置";
    }

    private static string InstallerVersion() =>
        (typeof(InstallerWindow).Assembly.GetName().Version ?? new Version(1, 0, 0)).ToString(3);

    private static string? ReadVersion(string exePath)
    {
        try { return File.Exists(exePath) ? InstallVersionPolicy.ResolveApplicationVersion(exePath) : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InstallerLog.Write($"read version failed path=\"{exePath}\"", ex);
            return null;
        }
    }

    private static bool IsUnder(string path, string parent) =>
        path.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static Image? LoadCoverImage()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(CoverResourceName);
            if (stream is null) return null;
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            InstallerLog.Write("cover image load failed", ex);
            return null;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        OfferBackupRestore();
    }

    /// <summary>上次安装在「替换目录」阶段被强杀时，backup 里是完整的旧版本：提示恢复，绝不自动删。</summary>
    private void OfferBackupRestore()
    {
        var target = _previousDirectory ?? Program.DefaultInstallDirectory();
        if (Directory.Exists(target)) return;
        var parent = Path.GetDirectoryName(target);
        if (parent is null || !Directory.Exists(parent)) return;

        string? backup;
        try
        {
            backup = Directory.EnumerateDirectories(parent, ".backup-*")
                .OrderByDescending(Directory.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InstallerLog.Write("backup scan failed", ex);
            return;
        }

        if (backup is null || !File.Exists(Path.Combine(backup, ExeName))) return;

        var result = MessageBox.Show(
            this,
            $"检测到上次安装中断留下的备份：\n{backup}\n\n是否恢复到 {target}？",
            "安装程序",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button1);
        if (result != DialogResult.Yes) return;

        try
        {
            Directory.Move(backup, target);
            _location.SetMessage("已恢复上次中断前的版本。", InstallerTheme.Success);
        }
        catch (Exception ex)
        {
            InstallerLog.Write("backup restore failed", ex);
            MessageBox.Show(this, "恢复失败，备份目录仍然保留，可手动改名恢复。", "安装程序",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var enabled = 1;
        try { DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int)); }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_cancellation is not null)
        {
            // 安装中关窗不等于放弃回滚：先取消，留在进度页显示「正在回滚…」。
            e.Cancel = true;
            _progress.MarkRollingBack();
            _cancellation.Cancel();
        }

        base.OnFormClosing(e);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
