using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SchoolNetAutoAuth.Installer;

internal sealed class LocationPage : InstallerPage
{
    private readonly FlatTextBox _pathBox;
    private readonly PathEllipsisLabel _fullPath;
    private readonly Label _space;
    private readonly Label _message;
    private readonly FlatButton _install;
    private readonly System.Windows.Forms.Timer _debounce;
    private InstallPathCheck _check = InstallPathCheck.Invalid("请输入安装路径");

    internal event EventHandler<string>? InstallRequested;
    internal event EventHandler? BackRequested;

    internal LocationPage(string initialDirectory)
    {
        var title = MakeText("请选择安装位置", 15, InstallerTheme.Fg);
        Place(this, title, ContentLeft, InstallerMetrics.Px(56), ContentWidth, InstallerMetrics.Px(24));

        var hint = MakeText("不选择则安装到默认位置", 12, InstallerTheme.MutedFg);
        Place(this, hint, ContentLeft, InstallerMetrics.Px(84), ContentWidth, InstallerMetrics.Px(18));

        _pathBox = new FlatTextBox(initialDirectory)
        {
            Size = new Size(ContentWidth, InstallerMetrics.Px(34))
        };
        Place(this, _pathBox, ContentLeft, InstallerMetrics.Px(116), ContentWidth, InstallerMetrics.Px(34));

        var browse = SecondaryButton("浏览…", 56);
        browse.Height = InstallerMetrics.Px(32);
        Place(this, browse, ContentLeft + ContentWidth - browse.Width, InstallerMetrics.Px(158), browse.Width, browse.Height);
        browse.Click += (_, _) => Browse();

        var targetLabel = MakeText("将安装到", 11, InstallerTheme.MutedFg);
        Place(this, targetLabel, ContentLeft, InstallerMetrics.Px(208), ContentWidth, InstallerMetrics.Px(16));

        _fullPath = new PathEllipsisLabel { BackColor = InstallerTheme.Bg };
        Place(this, _fullPath, ContentLeft, InstallerMetrics.Px(224), ContentWidth, InstallerMetrics.Px(16));

        _space = MakeText(string.Empty, 11, InstallerTheme.MutedFg, mono: true);
        Place(this, _space, ContentLeft, InstallerMetrics.Px(244), ContentWidth, InstallerMetrics.Px(16));

        // 固定 20 行高：没有错误时也占位，页面不会上下跳。
        _message = MakeText(string.Empty, 12, InstallerTheme.Destructive);
        Place(this, _message, ContentLeft, InstallerMetrics.Px(264), ContentWidth, InstallerMetrics.Px(20));

        var back = SecondaryButton("上一步", 80);
        Place(this, back, ContentLeft + ContentWidth - InstallerMetrics.Px(96) - InstallerMetrics.Px(8) - back.Width,
            BottomButtonTop, back.Width, back.Height);
        back.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);

        _install = PrimaryButton("安装");
        Place(this, _install, ContentLeft + ContentWidth - _install.Width, BottomButtonTop, _install.Width, _install.Height);
        _install.Click += (_, _) =>
        {
            if (_check.IsValid) InstallRequested?.Invoke(this, _check.FullPath);
        };

        _debounce = new System.Windows.Forms.Timer { Interval = 200 };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            ValidatePath();
        };
        _pathBox.TextChanged += (_, _) =>
        {
            _debounce.Stop();
            _debounce.Start();
        };

        VisibleChanged += (_, _) =>
        {
            if (Visible) ValidatePath();
        };
        ValidatePath();
    }

    internal string CurrentPath => _pathBox.Input.Text.Trim();

    internal void SetPath(string path)
    {
        _pathBox.Input.Text = path;
        ValidatePath();
    }

    /// <summary>取消 / 失败后回到本页时显示的一行提示。</summary>
    internal void SetMessage(string message, Color color)
    {
        _message.Text = message;
        _message.ForeColor = color;
    }

    private void ValidatePath()
    {
        _check = PathValidation.Check(_pathBox.Input.Text);
        _install.Enabled = _check.IsValid;
        _fullPath.FullText = _check.IsValid ? _check.FullPath : _pathBox.Input.Text.Trim();
        _fullPath.Invalidate();

        if (_check.IsValid)
        {
            _space.Text = _check.AvailableFreeBytes > 0
                ? $"可用 {PathValidation.FormatBytes(_check.AvailableFreeBytes)} · 需要约 320 MB"
                : "无法读取磁盘剩余空间";
            _space.ForeColor = _check.AvailableFreeBytes > 0 && _check.AvailableFreeBytes < PathValidation.RequiredBytes
                ? InstallerTheme.Warning
                : InstallerTheme.MutedFg;
        }
        else
        {
            _space.Text = string.Empty;
        }

        if (!_check.IsValid)
        {
            _message.Text = _check.Error;
            _message.ForeColor = InstallerTheme.Destructive;
        }
        else if (!string.IsNullOrEmpty(_check.Warning))
        {
            _message.Text = _check.Warning;
            _message.ForeColor = InstallerTheme.Warning;
        }
        else
        {
            _message.Text = string.Empty;
        }
    }

    private void Browse()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择安装位置",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        var start = _check.IsValid ? _check.FullPath : CurrentPath;
        if (Directory.Exists(start)) dialog.SelectedPath = start;
        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath)) return;
        _pathBox.Input.Text = dialog.SelectedPath;
        ValidatePath();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _debounce.Dispose();
        base.Dispose(disposing);
    }
}
