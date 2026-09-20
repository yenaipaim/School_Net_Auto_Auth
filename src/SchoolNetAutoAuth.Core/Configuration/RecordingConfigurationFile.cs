namespace SchoolNetAutoAuth.Core.Configuration;

public sealed record RecordingConfigurationFile(
    string Format,
    int Version,
    DateTimeOffset ExportedAtUtc,
    RecordedClickSequence RecordedSequence)
{
    public const string ExpectedFormat = "SchoolNetAutoAuth.Recording";
    public const int CurrentVersion = 1;
}
