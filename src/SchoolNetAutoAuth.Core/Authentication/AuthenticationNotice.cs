namespace SchoolNetAutoAuth.Core.Authentication;

public enum AuthenticationNoticeKind
{
    Connected,
    ExternalActionRequired
}

public sealed record AuthenticationNotice(
    AuthenticationNoticeKind Kind,
    string Message,
    ExternalActionKind? ExternalAction = null,
    DateTimeOffset? RetryAtUtc = null);
