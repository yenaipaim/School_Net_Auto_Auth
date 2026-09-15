using SchoolNetAutoAuth.Core.Configuration;

namespace SchoolNetAutoAuth.Core.Contracts;

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}
