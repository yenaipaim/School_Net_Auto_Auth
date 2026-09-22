using System.Text;

namespace SchoolNetAutoAuth.Installer;

internal static class InstallerLog
{
    private static readonly object Sync = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SchoolNetAutoAuth",
        "installer.log");

    public static void Write(string message, Exception? exception = null)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                var builder = new StringBuilder()
                    .Append(DateTimeOffset.Now.ToString("O"))
                    .Append(' ')
                    .Append(message);
                if (exception is not null)
                    builder.Append(" error=").Append(exception.GetType().Name).Append(": ").Append(exception.Message);
                builder.AppendLine();
                File.AppendAllText(LogPath, builder.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Installer logging must never block installation.
        }
    }
}
