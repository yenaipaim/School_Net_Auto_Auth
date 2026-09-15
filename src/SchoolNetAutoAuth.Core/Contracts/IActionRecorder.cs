using SchoolNetAutoAuth.Core.Configuration;

namespace SchoolNetAutoAuth.Core.Contracts;

public sealed record RecorderRequest(Uri PortalUri, string ProviderName);
public sealed record RecorderStep(string Key, string Message);

public interface IActionRecorder
{
    Task<RecordedPortalFlow> RecordAsync(RecorderRequest request, IProgress<RecorderStep> progress, CancellationToken cancellationToken);
}
