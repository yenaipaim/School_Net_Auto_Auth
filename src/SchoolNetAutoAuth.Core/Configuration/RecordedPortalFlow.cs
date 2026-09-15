namespace SchoolNetAutoAuth.Core.Configuration;

public sealed record RecordedPortalFlow(
    RecordedLocator Username,
    RecordedLocator Password,
    RecordedLocator Login,
    IReadOnlyDictionary<string, RecordedLocator> Providers,
    DateTimeOffset RecordedAtUtc);
