using System.Text.RegularExpressions;
using SchoolNetAutoAuth.Core.Authentication;
using SchoolNetAutoAuth.Core.Contracts;

namespace SchoolNetAutoAuth.Infrastructure.Automation;

public sealed record ExternalActionDetection(ExternalActionKind Kind, string SafeExcerpt);

public static partial class ExternalActionClassifier
{
    private static readonly string[] PhoneSubjects = ["电话", "手机", "拨打", "语音", "phone"];
    private static readonly string[] VerificationTerms = ["验证", "认证", "verification", "verify"];
    private static readonly string[] DeviceSubjects = ["设备", "终端", "在线设备", "device", "terminal"];
    private static readonly string[] LimitTerms = ["上限", "已满", "超出", "达到", "limit", "exceeded", "maximum"];

    public static ExternalActionDetection? Classify(IEnumerable<string> sources, PortalCredential? credential)
    {
        foreach (var source in sources.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var normalized = Normalize(source);
            if (ContainsAny(normalized, PhoneSubjects) && ContainsAny(normalized, VerificationTerms))
                return new(ExternalActionKind.PhoneVerification, Sanitize(source, credential));
            if (ContainsAny(normalized, DeviceSubjects) && ContainsAny(normalized, LimitTerms))
                return new(ExternalActionKind.DeviceLimit, Sanitize(source, credential));
        }
        return null;
    }

    private static string Normalize(string value) => CollapseWhitespace(value).ToLowerInvariant();

    private static bool ContainsAny(string value, IEnumerable<string> terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string Sanitize(string value, PortalCredential? credential)
    {
        var result = CollapseWhitespace(value);
        if (credential is not null)
        {
            if (!string.IsNullOrEmpty(credential.Username))
                result = result.Replace(credential.Username, "[已隐藏]", StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(credential.Password))
                result = result.Replace(credential.Password, "[已隐藏]", StringComparison.Ordinal);
        }
        result = LongTokenRegex().Replace(result, "[已隐藏]");
        return result[..Math.Min(300, result.Length)];
    }

    private static string CollapseWhitespace(string value) => WhitespaceRegex().Replace(value, " ").Trim();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[A-Za-z0-9_+/=-]{32,}", RegexOptions.CultureInvariant)]
    private static partial Regex LongTokenRegex();
}
