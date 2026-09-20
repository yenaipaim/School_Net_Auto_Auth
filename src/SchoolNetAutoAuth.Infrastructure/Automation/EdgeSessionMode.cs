namespace SchoolNetAutoAuth.Infrastructure.Automation;

/// <summary>Describes why an Edge session is being opened.</summary>
public enum EdgeSessionMode
{
    Recording,
    BackgroundAuthentication,
    InteractiveRecovery
}
