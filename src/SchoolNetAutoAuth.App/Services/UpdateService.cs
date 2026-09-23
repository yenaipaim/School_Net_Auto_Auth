using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SchoolNetAutoAuth.App.Services;

public sealed record UpdateCheckResult(
    Version CurrentVersion,
    Version? LatestVersion,
    Uri? ReleaseUri)
{
    public bool IsUpdateAvailable => LatestVersion is not null && LatestVersion > CurrentVersion;
}

public sealed class UpdateService
{
    private static readonly Uri LatestReleaseEndpoint =
        new("https://api.github.com/repos/yenaipaim/School_Net_Auto_Auth/releases/latest");
    private static readonly Regex VersionRegex = new(
        @"(?<!\d)(?<version>\d+(?:\.\d+){0,3})(?:-[0-9A-Za-z.-]+)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly HttpClient _client;

    public UpdateService(HttpClient? client = null)
    {
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        if (!_client.DefaultRequestHeaders.UserAgent.Any())
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("SchoolNetAutoAuth");
    }

    public async Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(LatestReleaseEndpoint, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return new(currentVersion, null, null);
        response.EnsureSuccessStatusCode();

        var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken)
            ?? throw new InvalidDataException("版本服务返回了空响应。");
        var latestVersion = ParseVersion(release.TagName, release.Name)
            ?? throw new InvalidDataException("版本服务返回的版本号无效。");
        if (!Uri.TryCreate(release.HtmlUrl, UriKind.Absolute, out var releaseUri))
            throw new InvalidDataException("版本服务返回的下载地址无效。");

        return new(currentVersion, latestVersion, releaseUri);
    }

    private static Version? ParseVersion(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var normalized = candidate.Trim().TrimStart('v', 'V').Split('-', 2)[0];
            if (Version.TryParse(normalized, out var directVersion)) return directVersion;

            var match = VersionRegex.Match(candidate);
            if (match.Success && Version.TryParse(match.Groups["version"].Value, out var matchedVersion))
                return matchedVersion;
        }
        return null;
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string? TagName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("html_url")] string? HtmlUrl);
}
