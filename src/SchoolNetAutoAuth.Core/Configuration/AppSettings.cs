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
    RecordedClickSequence? RecordedSequence)
{
    public static AppSettings CreateDefault() => new(
        2, "NSU-SDN", new Uri("http://2.2.2.2"), new Uri("https://www.yuanshen.com"),
        TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(10), 3, true, null);

    public ValidationResult Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(TargetSsid)) errors.Add("目标 Wi-Fi 不能为空。");
        if (PortalUri is null || !PortalUri.IsAbsoluteUri) errors.Add("认证地址必须是完整地址。");
        if (ProbeUri is null || !ProbeUri.IsAbsoluteUri || ProbeUri.Scheme != Uri.UriSchemeHttps) errors.Add("联网检测地址必须使用 HTTPS。");
        if (RetryInterval <= TimeSpan.Zero) errors.Add("重试间隔必须大于零。");
        if (MaximumAttempts < 1) errors.Add("最大重试次数至少为 1。");
        return new ValidationResult(errors);
    }
}

public sealed record ValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
