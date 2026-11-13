using System.Drawing;
using System.Windows.Forms;

namespace SchoolNetAutoAuth.Installer;

internal sealed class CompletePage : InstallerPage
{
    private readonly Label _version;
    private readonly Label _location;
    private readonly Label _wifi;
    private readonly Label _portal;
    private readonly CheckBox _launch;
    private readonly FlatButton _finish;

    internal event EventHandler<bool>? FinishRequested;

    internal CompletePage()
    {
        var title = MakeText("安装完成！", 24, InstallerTheme.Fg, style: FontStyle.Bold);
        Place(this, title, ContentLeft, InstallerMetrics.Px(56), ContentWidth, InstallerMetrics.Px(36));

        _version = AddRow("版本", InstallerMetrics.Px(100));
        _location = AddRow("安装位置", InstallerMetrics.Px(128));
        _wifi = AddRow("当前 Wi-Fi", InstallerMetrics.Px(156));
        _portal = AddRow("认证入口", InstallerMetrics.Px(184));

        _launch = new CheckBox
        {
            Text = "启动校园网自动认证",
            Checked = true,
            AutoSize = false,
            ForeColor = InstallerTheme.Fg,
            BackColor = Color.Transparent,
            Font = InstallerTheme.Ui(InstallerMetrics.Px(12)),
            FlatStyle = FlatStyle.System
        };
        Place(this, _launch, ContentLeft, InstallerMetrics.Px(220), ContentWidth, InstallerMetrics.Px(24));

        _finish = PrimaryButton("完成");
        Place(this, _finish, ContentLeft + ContentWidth - _finish.Width, BottomButtonTop,
            _finish.Width, _finish.Height);
        _finish.Click += (_, _) => FinishRequested?.Invoke(this, _launch.Checked);
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool LaunchAfterFinish
    {
        get => _launch.Checked;
        set => _launch.Checked = value;
    }

    internal void SetSummary(string version, string installPath, string wifi, string portal)
    {
        _version.Text = version;
        _location.Text = installPath;
        _wifi.Text = wifi;
        _portal.Text = portal;
    }

    private Label AddRow(string labelText, int top)
    {
        var label = MakeText(labelText, 11, InstallerTheme.MutedFg);
        Place(this, label, ContentLeft, top, InstallerMetrics.Px(64), InstallerMetrics.Px(28));

        var value = MakeText(string.Empty, 11, InstallerTheme.Fg, mono: true);
        value.AutoEllipsis = true;
        Place(this, value, ContentLeft + InstallerMetrics.Px(72), top,
            ContentWidth - InstallerMetrics.Px(72), InstallerMetrics.Px(28));
        return value;
    }
}
