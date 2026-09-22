using System.Net;
using System.Net.Http;
using System.Text;
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

    [Fact]
    public async Task CheckAsync_RejectsSuccessfulPortalLoginPage()
    {
        const string portalPage = """
            <html><body>
              <form action="/login">
                <h1>校园网登录</h1>
                <input type="text" name="username">
                <input type="password" name="password">
              </form>
            </body></html>
            """;
        using var probe = new HttpConnectivityProbe(new StubHandler(HttpStatusCode.OK, portalPage));

        var result = await probe.CheckAsync(new Uri("https://external.example/"), TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.False(result.IsOnline);
        Assert.Equal("captive_portal_detected", result.Reason);
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

    private sealed class StubHandler(HttpStatusCode status, string? body = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status) { RequestMessage = request };
            if (body is not null)
                response.Content = new StringContent(body, Encoding.UTF8, "text/html");
            return Task.FromResult(response);
        }
    }
}
