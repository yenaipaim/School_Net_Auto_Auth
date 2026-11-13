using System.Drawing;
using System.Windows.Forms;

namespace SchoolNetAutoAuth.Installer;

/// <summary>设计令牌：13 个颜色 + 6 个字号，全部取自安装器设计稿。</summary>
internal static class InstallerTheme
{
    internal static readonly Color Bg = Rgb(0x1F, 0x1F, 0x1F);
    internal static readonly Color Card = Rgb(0x25, 0x25, 0x26);
    internal static readonly Color Panel = Rgb(0x2B, 0x2B, 0x2D);
    internal static readonly Color Border = Rgb(0x3F, 0x3F, 0x46);
    internal static readonly Color BorderStrong = Rgb(0x52, 0x52, 0x5B);
    internal static readonly Color Fg = Rgb(0xF3, 0xF3, 0xF3);
    internal static readonly Color MutedFg = Rgb(0xB8, 0xB8, 0xB8);
    internal static readonly Color Brand = Rgb(0x00, 0x78, 0xD4);
    internal static readonly Color BrandHover = Rgb(0x1A, 0x86, 0xDC);
    internal static readonly Color Success = Rgb(0x4E, 0xC9, 0xB0);
    internal static readonly Color Warning = Rgb(0xD7, 0xBA, 0x7D);
    internal static readonly Color Destructive = Rgb(0xF1, 0x4C, 0x4C);
    internal static readonly Color Input = Rgb(0x4A, 0x4A, 0x50);
    internal static readonly Color Surface = Rgb(0x2E, 0x2E, 0x31);

    internal static Font Ui(float pixels, FontStyle style = FontStyle.Regular) =>
        new("Microsoft YaHei UI", pixels, style, GraphicsUnit.Pixel);

    internal static Font Mono(float pixels, FontStyle style = FontStyle.Regular) =>
        new("Consolas", pixels, style, GraphicsUnit.Pixel);

    private static Color Rgb(int red, int green, int blue) => Color.FromArgb(red, green, blue);
}

/// <summary>96 DPI 设计坐标 → 运行时物理像素。安装器全程手动缩放，免得 AutoScale 二次放大。</summary>
internal static class InstallerMetrics
{
    internal static float Scale { get; set; } = 1f;

    internal static int Px(double dip) => (int)Math.Round(dip * Scale);

    internal static void MeasureDpi()
    {
        using var graphics = Graphics.FromHwnd(IntPtr.Zero);
        Scale = graphics.DpiX / 96f;
    }
}
