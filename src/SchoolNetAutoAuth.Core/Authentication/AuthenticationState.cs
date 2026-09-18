namespace SchoolNetAutoAuth.Core.Authentication;

public enum AuthenticationState
{
    WaitingForTargetWifi,
    CheckingConnectivity,
    Online,
    Authenticating,
    WaitingForCredentials,
    RetryDelay,
    ExternalActionCooldown,
    Paused,
    ActionRequired
}

public enum AuthenticationOutcome
{
    Succeeded,
    Failed,
    CredentialsRequired,
    RecordingRequired,
    ExternalActionRequired,
    Cancelled
}

public enum ExternalActionKind
{
    PhoneVerification,
    DeviceLimit
}

public enum AuthenticationTrigger
{
    Background,
    Manual
}
