using Microsoft.Playwright;

namespace SchoolNetAutoAuth.Infrastructure.Automation;

public sealed class EdgeSessionFactory(string userDataDirectory)
{
    public async Task<EdgeSession> LaunchAsync(bool headless, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(userDataDirectory);
        var playwright = await Playwright.CreateAsync();
        try
        {
            var context = await playwright.Chromium.LaunchPersistentContextAsync(userDataDirectory, new()
            {
                Channel = "msedge",
                Headless = headless,
                Args = ["--no-first-run"],
                ViewportSize = ViewportSize.NoViewport
            }).WaitAsync(cancellationToken);
            return new(playwright, context);
        }
        catch { playwright.Dispose(); throw; }
    }
}

public sealed class EdgeSession(IPlaywright playwright, IBrowserContext context) : IAsyncDisposable
{
    public IBrowserContext Context { get; } = context;
    public async ValueTask DisposeAsync()
    {
        await Context.CloseAsync();
        playwright.Dispose();
    }
}
