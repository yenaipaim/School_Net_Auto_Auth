namespace SchoolNetAutoAuth.App.Uninstallation;

public sealed record UninstallPlan(
    string InstallDirectory,
    string UserDataDirectory,
    bool DeleteUserData)
{
    public bool DeleteCredential => DeleteUserData;
    public bool DeleteEdgeProfile => DeleteUserData;
    public bool DeleteSettings => DeleteUserData;

    public static UninstallPlan CreateDefault(string installDirectory, string userDataDirectory) =>
        new(installDirectory, userDataDirectory, DeleteUserData: false);
}
