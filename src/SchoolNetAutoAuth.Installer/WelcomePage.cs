using System.Drawing;
using System.Windows.Forms;

namespace SchoolNetAutoAuth.Installer;

internal sealed class WelcomePage : InstallerPage
{
    private readonly Label _title;
    private readonly Label _product;
    private readonly Label _version;
    private readonly Label _description;
    private readonly Label _eyebrow;

    internal event EventHandler? NextRequested;

    internal WelcomePage(string version)
    {
        _eyebrow = MakeText("安装程序", 11, InstallerTheme.MutedFg);
        Place(this, _eyebrow, ContentLeft, InstallerMetrics.Px(56), ContentWidth, InstallerMetrics.Px(16));

        _title = MakeText("感谢你选择本项目", 28, InstallerTheme.Fg, style: FontStyle.Bold);
        Place(this, _title, ContentLeft, InstallerMetrics.Px(80), ContentWidth, InstallerMetrics.Px(40));

        _product = MakeText("校园网自动认证", 13, InstallerTheme.Fg);
        Place(this, _product, ContentLeft, InstallerMetrics.Px(132), ContentWidth, InstallerMetrics.Px(20));

        _version = MakeText("版本 " + version, 13, InstallerTheme.Fg, mono: true);
        Place(this, _version, ContentLeft, InstallerMetrics.Px(156), ContentWidth, InstallerMetrics.Px(18));

        _description = MakeText("面向 Windows 10 / 11", 12, InstallerTheme.MutedFg);
        Place(this, _description, ContentLeft, InstallerMetrics.Px(188), ContentWidth, InstallerMetrics.Px(16));

        var next = PrimaryButton("下一步");
        Place(this, next, ContentLeft + ContentWidth - next.Width, BottomButtonTop, next.Width, next.Height);
        next.Click += (_, _) => NextRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>注册表存在旧版本时换一套文案，不改变布局。</summary>
    internal void UseUpgradeMode(string installedVersion, string incomingVersion)
    {
        _title.Text = "正在升级";
        _version.Text = $"{installedVersion} → {incomingVersion}";
        _description.Text = "你的网络设置、录制流程和课程表都会保留。";
        _description.Height = InstallerMetrics.Px(40);
    }
}
