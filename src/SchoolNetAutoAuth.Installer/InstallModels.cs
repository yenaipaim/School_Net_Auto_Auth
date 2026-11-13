namespace SchoolNetAutoAuth.Installer;

internal sealed record InstallRequest(string TargetDirectory, bool LaunchAfterInstall);

internal enum InstallStage
{
    Prepare = 0,
    Extract = 1,
    Replace = 2,
    Register = 3,
    Launch = 4
}

internal readonly record struct InstallProgress(
    int Percent,
    string Message,
    InstallStage Stage,
    int CompletedItems = 0,
    int TotalItems = 0);
