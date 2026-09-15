namespace SchoolNetAutoAuth.Core.Authentication;

public sealed record AuthenticationResult(AuthenticationOutcome Outcome, string ReasonCode)
{
    public static AuthenticationResult Success() => new(AuthenticationOutcome.Succeeded, "success");
}
