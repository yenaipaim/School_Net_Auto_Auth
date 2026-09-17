namespace SchoolNetAutoAuth.Core.Configuration;

public sealed record RecordedCredentialLocators(
    RecordedLocator Username,
    RecordedLocator Password,
    string PageKey = "page-1",
    string UrlPattern = "");

public sealed record RecordedClickStep(
    int Order,
    string PageKey,
    string UrlPattern,
    RecordedLocator Locator,
    string Description);

public sealed record RecordedClickSequence(
    int Version,
    DateTimeOffset RecordedAtUtc,
    RecordedCredentialLocators Credentials,
    IReadOnlyList<RecordedClickStep> Clicks);
