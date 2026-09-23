using System.Net;
using System.Text;
using SchoolNetAutoAuth.App.Services;

namespace SchoolNetAutoAuth.App.Tests;

public sealed class UpdateServiceTests
{
    [Fact]
    public async Task CheckAsync_ReturnsNewerRelease()
    {
        using var client = new HttpClient(new StubHandler(HttpStatusCode.OK, """
            {
              "tag_name": "v1.4.0",
              "html_url": "https://github.com/yenaipaim/School_Net_Auto_Auth/releases/tag/v1.4.0"
            }
            """));
        var service = new UpdateService(client);

        var result = await service.CheckAsync(new Version(1, 3, 0), CancellationToken.None);

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal(new Version(1, 4, 0), result.LatestVersion);
        Assert.Equal(
            new Uri("https://github.com/yenaipaim/School_Net_Auto_Auth/releases/tag/v1.4.0"),
            result.ReleaseUri);
    }

    [Fact]
    public async Task CheckAsync_ReturnsNoUpdateForCurrentRelease()
    {
        using var client = new HttpClient(new StubHandler(HttpStatusCode.OK, """
            {
              "tag_name": "v1.3.0",
              "html_url": "https://github.com/yenaipaim/School_Net_Auto_Auth/releases/tag/v1.3.0"
            }
            """));
        var service = new UpdateService(client);

        var result = await service.CheckAsync(new Version(1, 3, 0), CancellationToken.None);

        Assert.False(result.IsUpdateAvailable);
        Assert.Equal(new Version(1, 3, 0), result.LatestVersion);
    }

    [Fact]
    public async Task CheckAsync_UsesReleaseNameWhenTagIsNotAVersion()
    {
        using var client = new HttpClient(new StubHandler(HttpStatusCode.OK, """
            {
              "tag_name": "Three",
              "name": "School_Net_Auto_Auth V1.1.1-beta",
              "html_url": "https://github.com/yenaipaim/School_Net_Auto_Auth/releases/tag/Three"
            }
            """));
        var service = new UpdateService(client);

        var result = await service.CheckAsync(new Version(1, 3, 0), CancellationToken.None);

        Assert.False(result.IsUpdateAvailable);
        Assert.Equal(new Version(1, 1, 1), result.LatestVersion);
    }

    private sealed class StubHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }
}
