using SchoolNetAutoAuth.Core.Authentication;
using SchoolNetAutoAuth.Core.Configuration;

namespace SchoolNetAutoAuth.Core.Contracts;

public sealed record AuthenticationAttempt(int Number, int Maximum, bool KeepBrowserOpenOnFailure);

public interface IAuthenticationRunner
{
    Task<AuthenticationResult> AuthenticateAsync(AppSettings settings, AuthenticationAttempt attempt, CancellationToken cancellationToken);
}
