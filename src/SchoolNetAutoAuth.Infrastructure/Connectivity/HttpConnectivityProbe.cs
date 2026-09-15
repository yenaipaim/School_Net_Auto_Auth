using System.Net;
using SchoolNetAutoAuth.Core.Contracts;

namespace SchoolNetAutoAuth.Infrastructure.Connectivity;

public sealed class HttpConnectivityProbe : IConnectivityProbe, IDisposable
{
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
            return response.StatusCode == HttpStatusCode.NetworkAuthenticationRequired
                ? new(false, "network_authentication_required")
                : new(true, $"http_{(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(false, "timeout"); }
        catch (HttpRequestException) { return new(false, "request_failed"); }
    }

    public void Dispose() => _client.Dispose();
}
