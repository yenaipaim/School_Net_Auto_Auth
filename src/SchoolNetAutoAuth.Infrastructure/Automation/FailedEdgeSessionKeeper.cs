namespace SchoolNetAutoAuth.Infrastructure.Automation;

public sealed class FailedEdgeSessionKeeper : IAsyncDisposable
{
    private EdgeSession? _session;
    public bool HasSession => _session is not null;
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

    /// <summary>Transfers ownership of the retained session to recovery orchestration.</summary>
    public Task<EdgeSession?> TakeAsync()
    {
        var session = Interlocked.Exchange(ref _session, null);
        return Task.FromResult(session);
    }
    public async ValueTask DisposeAsync() => await CloseAsync().ConfigureAwait(false);
}
