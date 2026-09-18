namespace SchoolNetAutoAuth.Core.Authentication;

public sealed record AuthenticationResult(
    AuthenticationOutcome Outcome,
    string ReasonCode,
    ExternalActionKind? ExternalAction = null,
    string? UserMessage = null)
{
    public static AuthenticationResult Success() => new(AuthenticationOutcome.Succeeded, "success");
}
