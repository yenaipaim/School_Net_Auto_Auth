using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace SchoolNetAutoAuth.App.Services;

/// <summary>
/// 运行时外观注入，等价于 PVE-VDI 的 applyAppearance()：
/// 主题决定整套令牌取值，强调色与字体决定少数几个可覆盖项。
/// 所有笔刷都是 Application.Resources 里的同一实例，这里原地改 Color，
/// 因此已经渲染的界面会立即刷新，不需要重建视觉树。
/// </summary>
public static class ThemeService
{
    public const string DarkTheme = "dark";
    public const string LightTheme = "light";

    public const string DefaultAccent = "#0078D4";

    private static readonly Dictionary<string, string> LightAccents = new(StringComparer.Ordinal)
    {
        ["#0078D4"] = "#0067C0",
        ["#3B82F6"] = "#2563EB",
        ["#0D9488"] = "#0F766E",
        ["#8B5CF6"] = "#7C3AED",
        ["#E11D48"] = "#BE123C",
        ["#F26B1D"] = "#C2510F"
    };

    private static readonly (string Key, Color Dark, Color Light)[] Palette =
    [
        ("AppBgBrush", C(0x1F, 0x1F, 0x1F), C(0xF3, 0xF3, 0xF3)),
        ("AppFgBrush", C(0xF3, 0xF3, 0xF3), C(0x1F, 0x1F, 0x1F)),
        ("AppCardBrush", C(0x25, 0x25, 0x26), C(0xFF, 0xFF, 0xFF)),
        ("AppPanelBrush", C(0x2B, 0x2B, 0x2D), C(0xFA, 0xFA, 0xFA)),
        ("AppPanelHoverBrush", C(0x32, 0x32, 0x34), C(0xF2, 0xF2, 0xF2)),
        ("AppSurfaceBrush", C(0x2E, 0x2E, 0x31), C(0xFF, 0xFF, 0xFF)),
        ("AppSurfaceHoverBrush", C(0x3A, 0x3A, 0x3D), C(0xF5, 0xF5, 0xF5)),
        ("AppPopoverBrush", C(0x2B, 0x2B, 0x2D), C(0xFF, 0xFF, 0xFF)),
        ("AppMutedBrush", C(0x33, 0x33, 0x36), C(0xF0, 0xF0, 0xF0)),
        ("AppMutedFgBrush", C(0xB8, 0xB8, 0xB8), C(0x5F, 0x5F, 0x5F)),
        ("AppPlaceholderBrush", A(0x99, 0xB8, 0xB8, 0xB8), A(0x99, 0x5F, 0x5F, 0x5F)),
        ("AppBorderBrush", C(0x3F, 0x3F, 0x46), C(0xD1, 0xD1, 0xD1)),
        ("AppBorderStrongBrush", C(0x52, 0x52, 0x5B), C(0xB8, 0xB8, 0xB8)),
        ("AppInputBrush", C(0x4A, 0x4A, 0x50), C(0xC8, 0xC8, 0xC8)),
        ("AppPrimaryBrush", C(0xF3, 0xF3, 0xF3), C(0x1F, 0x1F, 0x1F)),
        ("AppPrimaryFgBrush", C(0x1F, 0x1F, 0x1F), C(0xFA, 0xFA, 0xFA)),
        ("AppSecondaryBrush", C(0x33, 0x33, 0x36), C(0xEC, 0xEC, 0xEC)),
        ("AppSecondaryFgBrush", C(0xE8, 0xE8, 0xE8), C(0x27, 0x27, 0x27)),
        ("AppAccentBrush", C(0x3A, 0x3A, 0x3D), C(0xF2, 0xF2, 0xF2)),
        ("AppAccentFgBrush", C(0xFA, 0xFA, 0xFA), C(0x1F, 0x1F, 0x1F)),
        ("AppDestructiveBrush", C(0xF1, 0x4C, 0x4C), C(0xC4, 0x2B, 0x1C)),
        ("AppDestructiveFgBrush", C(0xFF, 0xF7, 0xF7), C(0xFF, 0xFF, 0xFF)),
        ("AppSuccessBrush", C(0x4E, 0xC9, 0xB0), C(0x10, 0x7C, 0x10)),
        ("AppWarningBrush", C(0xD7, 0xBA, 0x7D), C(0x9D, 0x5D, 0x00)),
        ("AppInfoBrush", C(0x4C, 0xC2, 0xFF), C(0x00, 0x67, 0xC0)),
        ("AppOverlayBrush", A(0xC8, 0x0C, 0x0C, 0x0C), A(0x61, 0x18, 0x18, 0x1B)),
        ("AppGridLineBrush", A(0x0B, 0xFF, 0xFF, 0xFF), A(0x0E, 0x14, 0x14, 0x14)),
        ("AppSuccessSoftBgBrush", A(0x1A, 0x4E, 0xC9, 0xB0), A(0x14, 0x10, 0x7C, 0x10)),
        ("AppSuccessSoftBorderBrush", A(0x40, 0x4E, 0xC9, 0xB0), A(0x3D, 0x10, 0x7C, 0x10)),
        ("AppWarningSoftBgBrush", A(0x1A, 0xD7, 0xBA, 0x7D), A(0x14, 0x9D, 0x5D, 0x00)),
        ("AppWarningSoftBorderBrush", A(0x4D, 0xD7, 0xBA, 0x7D), A(0x3D, 0x9D, 0x5D, 0x00)),
        ("AppDestructiveSoftBgBrush", A(0x1A, 0xF1, 0x4C, 0x4C), A(0x14, 0xC4, 0x2B, 0x1C)),
        ("AppDestructiveSoftBorderBrush", A(0x73, 0xF1, 0x4C, 0x4C), A(0x59, 0xC4, 0x2B, 0x1C)),
        ("AppInfoSoftBgBrush", A(0x1A, 0x4C, 0xC2, 0xFF), A(0x14, 0x00, 0x67, 0xC0)),
        ("AppInfoSoftBorderBrush", A(0x4D, 0x4C, 0xC2, 0xFF), A(0x3D, 0x00, 0x67, 0xC0))
    ];

    private static readonly Dictionary<string, SolidColorBrush> Tracked = [];

    public static bool IsLight { get; private set; }

    public static void Apply(string? theme, string? accent, string? fontFamily)
    {
        EnsureTracked();
        IsLight = string.Equals(theme, LightTheme, StringComparison.OrdinalIgnoreCase);
        foreach (var (key, dark, light) in Palette)
            SetColor(key, IsLight ? light : dark);

        var brand = NormalizeAccent(accent) ?? C(0x00, 0x78, 0xD4);
        if (IsLight && LightAccents.TryGetValue(ToHex(brand), out var lighter))
            brand = ParseHex(lighter) ?? brand;

        SetColor("AppBrandBrush", brand);
        SetColor("AppRingBrush", brand);
        SetColor("AppBrandSoftBgBrush", WithAlpha(brand, IsLight ? (byte)0x14 : (byte)0x1A));
        SetColor("AppBrandSoftBorderBrush", WithAlpha(brand, (byte)0x66));
        SetColor("AppBrandFgBrush", ReadableForeground(brand));

        // Palette 每次都会被重置成不透明色，所以有背景图时必须再压一次 alpha。
        ApplySurfaceTranslucency();
        ThemeChanged?.Invoke();
    }

    /// <summary>是存在背景图。为真时卡片/面板留出透光率，让背景图能从内容块里透出来。</summary>
    private static bool _translucent;

    /// <summary>这些表面在背景图开启后需要半透明，否则背景图只会在块与块的缝隙里露出来。</summary>
    private static readonly string[] TranslucentSurfaces =
    [
        "AppCardBrush",
        "AppPanelBrush",
        "AppPopoverBrush",
        "AppSurfaceBrush",
        "AppMutedBrush"
    ];

    /// <summary>背景图开关变化时调用；内部会重刷当前调色板上的 alpha。</summary>
    public static void SetSurfaceTranslucency(bool translucent)
    {
        if (_translucent == translucent) return;
        _translucent = translucent;
        ApplySurfaceTranslucency();
        ThemeChanged?.Invoke();
    }

    private static void ApplySurfaceTranslucency()
    {
        if (Tracked.Count == 0) return;
        var alpha = _translucent ? (byte)0xC8 : (byte)0xFF;
        foreach (var key in TranslucentSurfaces)
        {
            if (!Tracked.TryGetValue(key, out var brush)) continue;
            var color = brush.Color;
            brush.Color = Color.FromArgb(alpha, color.R, color.G, color.B);
        }
    }

    /// <summary>
    /// 主题切换后由窗口自己把根元素的 RequestedTheme / FontFamily 应用一次：
    /// 原生控件模板靠 RequestedTheme 决定取哪一套主题资源，
    /// 等价于 PVE-VDI 在 html 上切 light/dark class。
    /// </summary>
    public static event Action? ThemeChanged;

    /// <summary>界面字体族。字体资源本身不可变，所以由 MainWindow 把结果设到根元素上，
    /// 让所有文本通过继承拿到它（等价于 PVE-VDI 的 --font-sans 覆盖）。</summary>
    public static FontFamily ResolveFontFamily(string? fontFamily)
    {
        if (string.IsNullOrWhiteSpace(fontFamily))
            return (FontFamily)Application.Current.Resources["AppFontSans"];
        var safe = new string(fontFamily.Where(c => c is not ('{' or '}' or ';' or '"' or '<' or '>')).ToArray()).Trim();
        if (safe.Length is 0 or > 120)
            return (FontFamily)Application.Current.Resources["AppFontSans"];
        return new FontFamily(safe);
    }

    private static void EnsureTracked()
    {
        if (Tracked.Count > 0) return;
        var resources = Application.Current.Resources;
        foreach (var key in Palette.Select(item => item.Key)
                     .Append("AppBrandBrush").Append("AppRingBrush")
                     .Append("AppBrandFgBrush").Append("AppBrandSoftBgBrush")
                     .Append("AppBrandSoftBorderBrush"))
        {
            if (resources.TryGetValue(key, out object? brush) && brush is SolidColorBrush solid)
                Tracked[key] = solid;
        }
    }

    private static void SetColor(string key, Color color)
    {
        if (Tracked.TryGetValue(key, out var brush))
            brush.Color = color;
    }

    private static Color WithAlpha(Color color, byte alpha) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);

    /// <summary>WCAG 相对亮度：与 PVE-VDI 的 readableForeground() 同一公式。</summary>
    public static Color ReadableForeground(Color background)
    {
        static double Linear(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        var luminance = 0.2126 * Linear(background.R) + 0.7152 * Linear(background.G) + 0.0722 * Linear(background.B);
        return luminance > 0.46 ? C(0x0B, 0x0B, 0x0B) : C(0xFF, 0xFA, 0xF5);
    }

    public static Color? NormalizeAccent(string? value)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        if (trimmed.Length == 4 && trimmed[0] == '#')
        {
            trimmed = $"#{trimmed[1]}{trimmed[1]}{trimmed[2]}{trimmed[2]}{trimmed[3]}{trimmed[3]}";
        }
        return trimmed.Length == 7 && trimmed[0] == '#' ? ParseHex(trimmed) : null;
    }

    private static Color? ParseHex(string hex)
    {
        if (hex.Length != 7 || hex[0] != '#') return null;
        try
        {
            return Color.FromArgb(
                0xFF,
                byte.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string ToHex(Color color) =>
        $"#{color.R:x2}{color.G:x2}{color.B:x2}";

    private static Color C(byte r, byte g, byte b) => Color.FromArgb(0xFF, r, g, b);

    private static Color A(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);
}
