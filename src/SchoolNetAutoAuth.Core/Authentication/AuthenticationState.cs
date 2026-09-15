namespace SchoolNetAutoAuth.Core.Authentication;

public enum AuthenticationState
{
    WaitingForTargetWifi,
    CheckingConnectivity,
    Online,
    Authenticating,
    WaitingForCredentials,
    RetryDelay,
    ActionRequired
}

public enum AuthenticationOutcome
{
    Succeeded,
    Failed,
    CredentialsRequired,
    RecordingRequired,
    Cancelled
}
