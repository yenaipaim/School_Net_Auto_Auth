using System.Drawing;
using System.Windows.Forms;

namespace SchoolNetAutoAuth.Installer;

/// <summary>右栏页面基类：统一背景、DPI 缩放和一组排版小工具。</summary>
internal abstract class InstallerPage : UserControl
{
    protected InstallerPage()
    {
        BackColor = InstallerTheme.Bg;
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }

    protected static int ContentLeft => InstallerMetrics.Px(32);
    protected static int ContentWidth => InstallerMetrics.Px(320);
    protected static int BottomButtonTop => InstallerMetrics.Px(620) - InstallerMetrics.Px(28) - InstallerMetrics.Px(36);

    protected static Label MakeText(string text, float pixels, Color color, bool mono = false,
        FontStyle style = FontStyle.Regular)
    {
        return new Label
        {
            Text = text,
            AutoSize = false,
            BackColor = Color.Transparent,
            ForeColor = color,
            Font = mono ? InstallerTheme.Mono(InstallerMetrics.Px(pixels)) : InstallerTheme.Ui(InstallerMetrics.Px(pixels), style),
            TextAlign = ContentAlignment.TopLeft,
            UseCompatibleTextRendering = false
        };
    }

    protected static void Place(Control parent, Control child, int x, int y, int width, int height)
    {
        child.Location = new Point(x, y);
        child.Size = new Size(width, height);
        parent.Controls.Add(child);
    }

    protected static FlatButton PrimaryButton(string text) =>
        new(text, primary: true) { Size = new Size(InstallerMetrics.Px(96), InstallerMetrics.Px(36)) };

    protected static FlatButton SecondaryButton(string text, int width = 80) =>
        new(text, primary: false) { Size = new Size(InstallerMetrics.Px(width), InstallerMetrics.Px(36)) };
}
