namespace SchoolNetAutoAuth.Infrastructure.Automation;

public sealed class FailedEdgeSessionKeeper : IAsyncDisposable
{
    private EdgeSession? _session;
    public async Task ReplaceAsync(EdgeSession session)
    {
        if (_session is not null) await _session.DisposeAsync().ConfigureAwait(false);
        _session = session;
    }
    public async Task CloseAsync()
    {
        if (_session is null) return;
        await _session.DisposeAsync().ConfigureAwait(false);
        _session = null;
    }
    public async ValueTask DisposeAsync() => await CloseAsync().ConfigureAwait(false);
}
