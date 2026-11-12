namespace SchoolNetAutoAuth.Core.Configuration;

public sealed record AppSettings(
    int SchemaVersion,
    string TargetSsid,
    Uri PortalUri,
    Uri ProbeUri,
    TimeSpan NetworkCheckInterval,
    TimeSpan ProbeTimeout,
    TimeSpan AuthenticationTimeout,
    TimeSpan RetryInterval,
    int MaximumAttempts,
    bool StartWithWindows,
    bool AutomaticAuthenticationEnabled,
    string? BackgroundImagePath,
    double BackgroundImageOpacity,
    RecordedClickSequence? RecordedSequence,
    string Theme,
    string AccentColor,
    string? InterfaceFontFamily)
{
    public const int CurrentSchemaVersion = 6;
    public const string DarkTheme = "dark";
    public const string LightTheme = "light";
    public const string DefaultAccentColor = "#0078D4";

    public static AppSettings CreateDefault() => new(
        CurrentSchemaVersion, "NSU-SDN", new Uri("http://2.2.2.2"), new Uri("https://www.yuanshen.com"),
        TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(10), 3, true, true,
        null, 0.45, null, DarkTheme, DefaultAccentColor, null);

    public ValidationResult Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(TargetSsid)) errors.Add("目标 Wi-Fi 不能为空。");
        if (PortalUri is null || !PortalUri.IsAbsoluteUri) errors.Add("认证地址必须是完整地址。");
        if (ProbeUri is null || !ProbeUri.IsAbsoluteUri || ProbeUri.Scheme != Uri.UriSchemeHttps) errors.Add("联网检测地址必须使用 HTTPS。");
        if (RetryInterval <= TimeSpan.Zero) errors.Add("重试间隔必须大于零。");
        if (MaximumAttempts < 1) errors.Add("最大重试次数至少为 1。");
        if (BackgroundImageOpacity is < 0 or > 1) errors.Add("背景图像透明度必须在 0 到 1 之间。");
        if (Theme is not (DarkTheme or LightTheme)) errors.Add("界面主题只能是 dark 或 light。");
        if (!IsHexColor(AccentColor)) errors.Add("强调色必须是 #RGB 或 #RRGGBB 形式。");
        if (InterfaceFontFamily is { Length: > 120 }) errors.Add("界面字体名称过长。");
        return new ValidationResult(errors);
    }

    private static bool IsHexColor(string? value) =>
        value is { Length: 4 or 7 } && value[0] == '#'
        && value.Skip(1).All(Uri.IsHexDigit);
}

public sealed record ValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
