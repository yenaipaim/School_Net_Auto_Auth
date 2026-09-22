using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SchoolNetAutoAuth.App.Notifications;
using SchoolNetAutoAuth.App.Services;
using SchoolNetAutoAuth.App.Uninstallation;
using SchoolNetAutoAuth.App.ViewModels;
using SchoolNetAutoAuth.App.Views;
using SchoolNetAutoAuth.Core.Authentication;
using SchoolNetAutoAuth.Core.Contracts;
using SchoolNetAutoAuth.Infrastructure.Automation;
using SchoolNetAutoAuth.Infrastructure.Configuration;
using SchoolNetAutoAuth.Infrastructure.Connectivity;
using SchoolNetAutoAuth.Infrastructure.Credentials;
using SchoolNetAutoAuth.Infrastructure.Logging;
using SchoolNetAutoAuth.Infrastructure.Network;
using SchoolNetAutoAuth.Infrastructure.Startup;
using SchoolNetAutoAuth.Tray;

namespace SchoolNetAutoAuth.App;

public partial class App : Microsoft.UI.Xaml.Application
{
    private readonly DispatcherQueue _dispatcher;
    private Mutex? _mutex;
    private EventWaitHandle? _showWindowSignal;
    private readonly CancellationTokenSource _activationLifetime = new();
    private MainWindow? _window;
    private ServiceProvider? _services;
    private bool _exiting;
    public MainWindow? MainWindow => _window;

    public App()
    {
        InitializeComponent();
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    public static T GetService<T>() where T : notnull
    {
        var services = ((App)Current)._services
            ?? throw new InvalidOperationException("应用服务尚未初始化。");
        return services.GetRequiredService<T>();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var commandLine = Environment.GetCommandLineArgs().Skip(1).ToArray();
        if (UninstallCommand.TryParse(commandLine, out var quiet))
        {
            await RunUninstallAsync(quiet);
            return;
        }

        _mutex = new Mutex(true, "Local\\SchoolNetAutoAuth.Singleton", out var firstInstance);
        if (!firstInstance)
        {
            using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\SchoolNetAutoAuth.ShowWindow");
            signal.Set();
            _mutex.Dispose();
            _mutex = null;
            Exit();
            return;
        }

        _services = BuildServices();
        var windows = _services.GetRequiredService<WindowService>();
        _window = new MainWindow(_services.GetRequiredService<MainViewModel>(), windows);
        _showWindowSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\SchoolNetAutoAuth.ShowWindow");
        _ = WaitForShowWindowAsync(windows, _activationLifetime.Token);

        var tray = _services.GetRequiredService<TrayIconService>();
        tray.Initialize(Path.Combine(AppContext.BaseDirectory, "App.ico"));
        tray.AuthenticateRequested += (_, _) => _dispatcher.TryEnqueue(async () =>
        {
            try { await _services.GetRequiredService<AppController>().AuthenticateNowAsync(); }
            catch (Exception ex) { await _services.GetRequiredService<DialogService>().ShowErrorAsync("认证失败", ex.Message); }
        });
        tray.ShowRequested += (_, _) => _dispatcher.TryEnqueue(windows.Show);
        tray.ExitRequested += (_, _) => _dispatcher.TryEnqueue(async () => await ExitApplicationAsync());

        var controller = _services.GetRequiredService<AppController>();
        controller.SnapshotChanged += (_, snapshot) => _dispatcher.TryEnqueue(() =>
            tray.UpdateStatus(StatusViewModel.FormatAuthenticationState(snapshot.AuthenticationState)));
        controller.NoticeRaised += (_, notice) => _dispatcher.TryEnqueue(() =>
            _services.GetRequiredService<DesktopNotificationService>().Show(notice));

        var background = commandLine.Contains("--background", StringComparer.OrdinalIgnoreCase);
        if (background) windows.Hide();
        else _window.Activate();
        await controller.InitializeAsync();
    }

    private static ServiceProvider BuildServices()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SchoolNetAutoAuth");
        var services = new ServiceCollection();
        services.AddSingleton<ISettingsStore>(_ => new JsonSettingsStore(root));
        services.AddSingleton<WindowsCredentialStore>();
        services.AddSingleton<ICredentialStore>(provider => provider.GetRequiredService<WindowsCredentialStore>());
        services.AddSingleton<WindowsWifiMonitor>();
        services.AddSingleton<INetworkMonitor>(provider => provider.GetRequiredService<WindowsWifiMonitor>());
        services.AddSingleton<HttpConnectivityProbe>();
        services.AddSingleton<IConnectivityProbe>(provider => provider.GetRequiredService<HttpConnectivityProbe>());
        services.AddSingleton(_ => new EdgeSessionFactory(Path.Combine(root, "EdgeProfile")));
        services.AddSingleton<LocatorResolver>();
        services.AddSingleton<IAuthenticationRunner>(provider => new PlaywrightAuthenticationRunner(
            provider.GetRequiredService<EdgeSessionFactory>(),
            provider.GetRequiredService<LocatorResolver>(),
            provider.GetRequiredService<ICredentialStore>(),
            provider.GetRequiredService<IConnectivityProbe>()));
        services.AddSingleton<AuthenticationCoordinator>();
        services.AddSingleton<RegistryStartupManager>();
        services.AddSingleton<StartupTaskManager>();
        services.AddSingleton(_ => new RollingFileLogger(Path.Combine(root, "SchoolNetAutoAuth.log")));
        services.AddSingleton(_ => new SystemActionService(root));
        services.AddSingleton<AppController>();
        services.AddSingleton<WindowService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<FilePickerService>();
        services.AddSingleton<SchoolNetAutoAuth.Infrastructure.Configuration.RecordingConfigurationSerializer>();
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<DesktopNotificationService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<StatusViewModel>();
        services.AddSingleton<RecordingViewModel>();
        services.AddSingleton<NetworkSettingsViewModel>();
        services.AddSingleton<AdvancedSettingsViewModel>();
        return services.BuildServiceProvider();
    }

    private async Task WaitForShowWindowAsync(WindowService windows, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var signaled = await Task.Run(
                () => _showWindowSignal?.WaitOne(500) == true,
                cancellationToken);
            if (!signaled) continue;
            _dispatcher.TryEnqueue(windows.Show);
        }
    }

    private async Task RunUninstallAsync(bool quiet)
    {
        var plan = UninstallPlan.CreateDefault(
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SchoolNetAutoAuth"));
        if (!quiet)
        {
            var window = new UninstallWindow();
            window.Activate();
            var choice = await window.Completion;
            if (choice is null)
            {
                Exit();
                return;
            }
            plan = plan with { DeleteUserData = choice.Value };
        }

        try
        {
            UninstallService.Execute(plan);
            if (!quiet)
                NativeMessageBox.ShowInfo("卸载完成。");
            UninstallService.ScheduleInstallDirectoryRemoval(plan.InstallDirectory);
        }
        catch (Exception ex)
        {
            if (!quiet)
                NativeMessageBox.ShowError($"卸载失败：{ex.Message}");
        }
        Exit();
    }

    public async Task ExitApplicationAsync()
    {
        if (_exiting) return;
        _exiting = true;
        _activationLifetime.Cancel();
        if (_services is not null)
        {
            _services.GetRequiredService<WindowService>().IsExiting = true;
            await _services.DisposeAsync();
            _services = null;
        }
        _mutex?.Dispose();
        _window?.Close();
        Exit();
    }
}
