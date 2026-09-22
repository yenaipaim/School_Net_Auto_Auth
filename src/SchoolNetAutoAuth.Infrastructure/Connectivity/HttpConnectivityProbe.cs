using System.Net;
using System.Text;
using SchoolNetAutoAuth.Core.Contracts;

namespace SchoolNetAutoAuth.Infrastructure.Connectivity;

public sealed class HttpConnectivityProbe : IConnectivityProbe, IDisposable
{
    private const int MaximumBodyBytes = 16 * 1024;
    private static readonly string[] PortalLoginMarkers =
    [
        "校园网",
        "上网认证",
        "网络认证",
        "登录网络",
        "captive portal",
        "sign in to network",
        "network login"
    ];
    private static readonly string[] PasswordFieldMarkers =
    [
        "type=\"password\"",
        "type='password'",
        "name=\"password\"",
        "name='password'",
        "id=\"password\"",
        "id='password'"
    ];
    private readonly HttpClient _client;

    public HttpConnectivityProbe(HttpMessageHandler? handler = null)
    {
        handler ??= new HttpClientHandler { AllowAutoRedirect = false };
        _client = new HttpClient(handler, disposeHandler: true);
    }

    public async Task<ConnectivityResult> CheckAsync(Uri probeUri, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!probeUri.IsAbsoluteUri || probeUri.Scheme != Uri.UriSchemeHttps)
            return new(false, "probe_must_be_https");
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, probeUri);
            request.Headers.UserAgent.ParseAdd("SchoolNetAutoAuth/1.0");
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token);
            if (response.StatusCode == HttpStatusCode.NetworkAuthenticationRequired)
                return new(false, "network_authentication_required");
            if ((int)response.StatusCode is < 200 or >= 300)
                return new(false, $"http_{(int)response.StatusCode}");

            var body = await ReadBodyPrefixAsync(response.Content, timeoutSource.Token);
            if (LooksLikePortalLoginPage(body))
                return new(false, "captive_portal_detected");
            return new(true, $"http_{(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(false, "timeout"); }
        catch (HttpRequestException) { return new(false, "request_failed"); }
    }

    private static async Task<string> ReadBodyPrefixAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[MaximumBodyBytes];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);
            if (read == 0) break;
            total += read;
        }
        return total == 0 ? string.Empty : Encoding.UTF8.GetString(buffer, 0, total);
    }

    private static bool LooksLikePortalLoginPage(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return false;
        var normalized = body.ToLowerInvariant();
        var hasPortalMarker = PortalLoginMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
        var hasPasswordField = PasswordFieldMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
        return hasPortalMarker && hasPasswordField;
    }

    public void Dispose() => _client.Dispose();
}
