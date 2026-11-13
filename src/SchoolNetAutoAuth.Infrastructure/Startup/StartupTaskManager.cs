using System.Diagnostics;

namespace SchoolNetAutoAuth.Infrastructure.Startup;

public sealed class StartupTaskManager
{
    public const string TaskName = "SchoolNetAutoAuth";
    private readonly RegistryStartupManager _registry;
    private readonly Func<string, string[], int> _run;

    public StartupTaskManager(RegistryStartupManager registry, Func<string, string[], int>? run = null)
    {
        _registry = registry;
        _run = run ?? RunProcess;
    }

    public void SetEnabled(bool enabled, string executablePath)
    {
        if (!enabled)
        {
            Remove();
            return;
        }

        try
        {
            var exitCode = _run("schtasks.exe", BuildRegistrationArguments(executablePath));
            if (exitCode != 0) throw new InvalidOperationException($"schtasks.exe exited with code {exitCode}.");
            _registry.Remove();
        }
        catch
        {
            _registry.SetEnabled(true, executablePath);
        }
    }

    /// <summary>计划任务与旧版 Run 项任一存在即视为已注册，供设置保存时自愈判断。</summary>
    public bool IsRegistered()
    {
        if (_registry.IsEnabled()) return true;
        try { return _run("schtasks.exe", ["/Query", "/TN", TaskName]) == 0; }
        catch { return false; }
    }

    public void Remove()
    {
        try { _run("schtasks.exe", BuildRemovalArguments()); }
        catch { /* Cleanup must still remove the legacy startup value. */ }
        _registry.Remove();
    }

    public string BuildRegistrationCommand(string executablePath) =>
        $"schtasks.exe {JoinArguments(BuildRegistrationArguments(executablePath))}";

    private static string[] BuildRegistrationArguments(string executablePath) =>
    [
        "/Create", "/TN", TaskName, "/SC", "ONLOGON", "/RL", "LIMITED", "/F",
        "/TR", $"\"{executablePath}\" --background"
    ];

    private static string[] BuildRemovalArguments() => ["/Delete", "/TN", TaskName, "/F"];

    private static string JoinArguments(IEnumerable<string> arguments) =>
        string.Join(' ', arguments.Select(argument => argument.Contains(' ') || argument.Contains('"')
            ? $"\"{argument.Replace("\"", "\\\"")}\""
            : argument));

    private static int RunProcess(string fileName, string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo);
        if (process is null) return -1;
        process.WaitForExit();
        return process.ExitCode;
    }
}
