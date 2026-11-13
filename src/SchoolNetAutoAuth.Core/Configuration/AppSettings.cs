namespace SchoolNetAutoAuth.Core.Configuration;

public sealed record AppSettings(
    int SchemaVersion,
    string TargetSsid,
    Uri? PortalUri,
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
    string? InterfaceFontFamily,
    bool FirstRunPromptShown)
{
    public const int CurrentSchemaVersion = 7;
    public const string DarkTheme = "dark";
    public const string LightTheme = "light";
    public const string DefaultAccentColor = "#0078D4";
    public const string DefaultProbeUriText = "https://www.yuanshen.com";
    public const int MaximumSsidBytes = 32;

    /// <summary>不预置任何学校信息：Wi-Fi 名称与认证地址都由用户首次使用时填写。</summary>
    public static AppSettings CreateDefault() => new(
        CurrentSchemaVersion, string.Empty, null, new Uri(DefaultProbeUriText),
        TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(10), 3, true, true,
        null, 0.45, null, DarkTheme, DefaultAccentColor, null, false);

    /// <summary>目标 Wi-Fi 与认证地址都齐备时才允许自动认证。</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TargetSsid) && PortalUri is { IsAbsoluteUri: true };

    /// <summary>已配置但还没有录制认证流程：自动认证缺少可重放的点击序列。</summary>
    public bool HasRecordedFlow => RecordedSequence is { Clicks.Count: > 0 };

    public ValidationResult Validate()
    {
        var errors = new List<string>();
        // 空值代表「尚未配置」，是合法状态；只有填了内容才校验格式。
        if (!string.IsNullOrWhiteSpace(TargetSsid)
            && System.Text.Encoding.UTF8.GetByteCount(TargetSsid) > MaximumSsidBytes)
        {
            errors.Add($"目标 Wi-Fi 名称过长，SSID 最多 {MaximumSsidBytes} 字节（中文约 10 个字）。");
        }

        if (PortalUri is not null && !PortalUri.IsAbsoluteUri)
            errors.Add("认证地址必须是完整地址。");
        if (ProbeUri is null || !ProbeUri.IsAbsoluteUri || ProbeUri.Scheme != Uri.UriSchemeHttps)
            errors.Add("联网检测地址必须使用 HTTPS。");
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
