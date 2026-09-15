namespace SchoolNetAutoAuth.Core.Contracts;

public sealed record PortalCredential(string Username, string Password);

public interface ICredentialStore
{
    Task<PortalCredential?> ReadAsync(CancellationToken cancellationToken);
    Task WriteAsync(PortalCredential credential, CancellationToken cancellationToken);
    Task DeleteAsync(CancellationToken cancellationToken);
}
