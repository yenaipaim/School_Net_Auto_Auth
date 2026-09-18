using System.Diagnostics;

namespace SchoolNetAutoAuth.App.Services;

public sealed class SystemActionService(string dataDirectory)
{
    public string DataDirectory { get; } = dataDirectory;
    public string LogPath => Path.Combine(DataDirectory, "SchoolNetAutoAuth.log");
    public string EdgeProfilePath => Path.Combine(DataDirectory, "EdgeProfile");

    public void OpenDataDirectory() => OpenPath(DataDirectory);
    public void OpenEdgeProfileDirectory() => OpenPath(EdgeProfilePath);

    private static void OpenPath(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }
}
