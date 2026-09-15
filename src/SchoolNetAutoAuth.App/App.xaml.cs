using System.Windows;
using SchoolNetAutoAuth.App.Views;

namespace SchoolNetAutoAuth.App;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private MainWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new Mutex(true, "Local\\SchoolNetAutoAuth.Singleton", out var firstInstance);
        if (!firstInstance)
        {
            Shutdown();
            return;
        }
        var background = e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase);
        _window = new MainWindow(background);
        _window.Show();
        if (background) _window.Hide();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _window?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
