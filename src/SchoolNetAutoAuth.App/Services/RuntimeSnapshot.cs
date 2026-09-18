using SchoolNetAutoAuth.Core.Authentication;

namespace SchoolNetAutoAuth.App.Services;

public sealed record RuntimeSnapshot(
    string WifiName,
    bool IsConnected,
    AuthenticationState AuthenticationState,
    string LastAuthenticationResult);
