namespace SchoolNetAutoAuth.Infrastructure.Automation;

public static class RecordedPageMatcher
{
    public static bool Matches(string currentUrl, string recordedPattern)
    {
        if (string.IsNullOrWhiteSpace(recordedPattern)) return true;
        return string.Equals(Normalize(currentUrl), Normalize(recordedPattern), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return value.TrimEnd('/');
        return new UriBuilder(uri) { Query = string.Empty, Fragment = string.Empty }.Uri.ToString().TrimEnd('/');
    }
}
