using Microsoft.Playwright;

namespace SchoolNetAutoAuth.Infrastructure.Automation;

public sealed class EdgeSessionFactory(string userDataDirectory)
{
    public Task<EdgeSession> LaunchAsync(EdgeSessionMode mode, CancellationToken cancellationToken)
    {
        var headless = mode == EdgeSessionMode.BackgroundAuthentication;
        return LaunchCoreAsync(headless, cancellationToken);
    }

    // Kept for source compatibility with integrations compiled against the pre-mode API.
    [Obsolete("Use LaunchAsync(EdgeSessionMode, CancellationToken).")]
    public Task<EdgeSession> LaunchAsync(bool headless, CancellationToken cancellationToken) =>
        LaunchCoreAsync(headless, cancellationToken);

    private async Task<EdgeSession> LaunchCoreAsync(bool headless, CancellationToken cancellationToken)
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
        try { await Context.CloseAsync().ConfigureAwait(false); }
        finally { playwright.Dispose(); }
    }
}
