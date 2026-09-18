using SchoolNetAutoAuth.Core.Authentication;

namespace SchoolNetAutoAuth.Infrastructure.Logging;

public enum AppLogEvent
{
    ApplicationStarted,
    ApplicationStopped,
    StateChanged,
    AuthenticationAttempt,
    AuthenticationResult,
    SettingsSaved,
    RecordingStarted,
    RecordingCompleted,
    UninstallStarted,
    UninstallCompleted,
    UnexpectedError
}

public sealed record AppLogEntry(
    AppLogEvent Event,
    AuthenticationState? State = null,
    AuthenticationOutcome? Outcome = null,
    ExternalActionKind? ExternalAction = null,
    int? Attempt = null);
