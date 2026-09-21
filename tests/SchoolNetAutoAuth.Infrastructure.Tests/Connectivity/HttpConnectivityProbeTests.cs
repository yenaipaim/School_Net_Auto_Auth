using System.Net;
using System.Net.Http;
using SchoolNetAutoAuth.Infrastructure.Connectivity;

namespace SchoolNetAutoAuth.Infrastructure.Tests.Connectivity;

public sealed class HttpConnectivityProbeTests
{
    [Fact]
    public async Task CheckAsync_AcceptsSuccessfulExternalResponse()
    {
        using var probe = new HttpConnectivityProbe(new StubHandler(HttpStatusCode.OK));

        var result = await probe.CheckAsync(new Uri("https://external.example/"), TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.True(result.IsOnline);
        Assert.Equal("http_200", result.Reason);
    }

    [Theory]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.NetworkAuthenticationRequired)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task CheckAsync_RejectsPortalRedirectAndNonSuccessfulResponses(HttpStatusCode status)
    {
        using var probe = new HttpConnectivityProbe(new StubHandler(status));

        var result = await probe.CheckAsync(new Uri("https://external.example/"), TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.False(result.IsOnline);
        Assert.Equal(status == HttpStatusCode.NetworkAuthenticationRequired
            ? "network_authentication_required"
            : $"http_{(int)status}", result.Reason);
    }

    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { RequestMessage = request });
    }
}
