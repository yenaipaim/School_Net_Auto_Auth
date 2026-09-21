using SchoolNetAutoAuth.Core.Authentication;
using SchoolNetAutoAuth.Core.Configuration;
using SchoolNetAutoAuth.Core.Contracts;
using SchoolNetAutoAuth.Infrastructure.Automation;
using SchoolNetAutoAuth.Infrastructure.Logging;
using SchoolNetAutoAuth.Infrastructure.Startup;
using System.Diagnostics;

namespace SchoolNetAutoAuth.App.Services;

public sealed class AppController : IAsyncDisposable
{
    private static readonly TimeSpan OnlineMonitoringInterval = TimeSpan.FromMinutes(30);
    private readonly ISettingsStore _settingsStore;
    private readonly ICredentialStore _credentials;
    private readonly INetworkMonitor _network;
    private readonly IConnectivityProbe _probe;
    private readonly AuthenticationCoordinator _coordinator;
    private readonly EdgeSessionFactory _sessions;
    private readonly LocatorResolver _resolver;
    private readonly StartupTaskManager _startup;
    private readonly RollingFileLogger _logger;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _recordingCancellation;
    private Task<RecordedClickSequence>? _recordingTask;
    private Task _monitorTask = Task.CompletedTask;
    private RuntimeSnapshot _snapshot = new("未连接", false, AuthenticationState.WaitingForTargetWifi, "尚未认证");
    private bool _disposed;

    public AppController(
        ISettingsStore settingsStore,
        ICredentialStore credentials,
        INetworkMonitor network,
        IConnectivityProbe probe,
        AuthenticationCoordinator coordinator,
        EdgeSessionFactory sessions,
        LocatorResolver resolver,
        StartupTaskManager startup,
        RollingFileLogger logger)
    {
        _settingsStore = settingsStore;
        _credentials = credentials;
        _network = network;
        _probe = probe;
        _coordinator = coordinator;
        _sessions = sessions;
        _resolver = resolver;
        _startup = startup;
        _logger = logger;
        Settings = AppSettings.CreateDefault();
        _coordinator.StateChanged += Coordinator_StateChanged;
        _coordinator.NoticeRaised += Coordinator_NoticeRaised;
    }

    public AppSettings Settings { get; private set; }
    public RuntimeSnapshot Snapshot => _snapshot;
    public bool IsRecording => _recordingTask is not null;

    public event EventHandler<RuntimeSnapshot>? SnapshotChanged;
    public event EventHandler<AppSettings>? SettingsChanged;
    public event EventHandler<AuthenticationNotice>? NoticeRaised;
    public event EventHandler<string>? RecordingProgressChanged;
    public event EventHandler<string>? RecordingFailed;

    public async Task InitializeAsync()
    {
        _logger.Write(new(AppLogEvent.ApplicationStarted));
        Settings = await _settingsStore.LoadAsync(_lifetime.Token);
        SettingsChanged?.Invoke(this, Settings);
        await RefreshNetworkSnapshotAsync(_lifetime.Token);
        _monitorTask = MonitorAsync(_lifetime.Token);
    }

    public async Task AuthenticateNowAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
        await _coordinator.EvaluateAsync(Settings, AuthenticationTrigger.Manual, linked.Token);
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var validation = settings.Validate();
        if (!validation.IsValid)
            throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));

        await _settingsStore.SaveAsync(settings, cancellationToken);
        Settings = settings;
        _startup.SetEnabled(settings.StartWithWindows, Environment.ProcessPath ?? throw new InvalidOperationException("无法确定程序路径。"));
        _logger.Write(new(AppLogEvent.SettingsSaved));
        SettingsChanged?.Invoke(this, settings);
    }

    public Task SaveCredentialAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            throw new InvalidDataException("请输入账号和密码。");
        return _credentials.WriteAsync(new(username.Trim(), password), cancellationToken);
    }

    public Task DeleteCredentialAsync(CancellationToken cancellationToken = default) =>
        _credentials.DeleteAsync(cancellationToken);

    public async Task StartRecordingAsync(CancellationToken cancellationToken = default)
    {
        if (_recordingTask is not null) return;
        var recordingCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
        _recordingCancellation = recordingCancellation;
        var progress = new Progress<RecorderStep>(step => RecordingProgressChanged?.Invoke(this, step.Message));
        var recordingTask = new PlaywrightActionRecorder(_sessions, _resolver)
            .RecordAsync(new(Settings.PortalUri), progress, recordingCancellation.Token);
        _recordingTask = recordingTask;
        _ = ObserveRecordingFailureAsync(recordingTask, recordingCancellation);
        _logger.Write(new(AppLogEvent.RecordingStarted));
        await Task.Yield();
    }

    private async Task ObserveRecordingFailureAsync(Task<RecordedClickSequence> recordingTask, CancellationTokenSource recordingCancellation)
    {
        try { await recordingTask; }
        catch (OperationCanceledException) when (recordingCancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.Write(new(AppLogEvent.UnexpectedError));
            RecordingFailed?.Invoke(this, ex.Message);
            if (ReferenceEquals(_recordingTask, recordingTask))
                ResetRecording();
        }
    }

    public async Task<RecordedClickSequence> StopRecordingAsync()
    {
        if (_recordingTask is null || _recordingCancellation is null)
            throw new InvalidOperationException("当前没有正在进行的录制。");

        _recordingCancellation.Cancel();
        try
        {
            var sequence = await _recordingTask;
            if (sequence.Clicks.Count == 0)
                throw new InvalidDataException("没有录制到可重放的点击操作。");
            Settings = Settings with { RecordedSequence = sequence };
            await _settingsStore.SaveAsync(Settings, CancellationToken.None);
            _logger.Write(new(AppLogEvent.RecordingCompleted));
            SettingsChanged?.Invoke(this, Settings);
            return sequence;
        }
        finally
        {
            ResetRecording();
        }
    }

    public async Task DeleteRecordedStepAsync(RecordedClickStep selected)
    {
        if (Settings.RecordedSequence is null) return;
        var clicks = Settings.RecordedSequence.Clicks
            .Where(step => step != selected)
            .OrderBy(step => step.Order)
            .Select((step, index) => step with { Order = index + 1 })
            .ToArray();
        Settings = Settings with { RecordedSequence = Settings.RecordedSequence with { Clicks = clicks } };
        await _settingsStore.SaveAsync(Settings, CancellationToken.None);
        SettingsChanged?.Invoke(this, Settings);
    }

    public async Task ClearRecordingAsync()
    {
        Settings = Settings with { RecordedSequence = null };
        await _settingsStore.SaveAsync(Settings, CancellationToken.None);
        SettingsChanged?.Invoke(this, Settings);
    }

    public async Task ImportRecordingAsync(RecordedClickSequence sequence)
    {
        Settings = Settings with { RecordedSequence = sequence };
        await _settingsStore.SaveAsync(Settings, CancellationToken.None);
        SettingsChanged?.Invoke(this, Settings);
    }

    private async Task MonitorAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RefreshNetworkSnapshotAsync(cancellationToken);
                if (Settings.AutomaticAuthenticationEnabled)
                    await _coordinator.EvaluateAsync(Settings, AuthenticationTrigger.Background, cancellationToken);
                var nextCheck = _snapshot.IsConnected
                    ? OnlineMonitoringInterval
                    : Settings.NetworkCheckInterval;
                await Task.Delay(nextCheck, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                _logger.Write(new(AppLogEvent.UnexpectedError));
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
    }

    private async Task RefreshNetworkSnapshotAsync(CancellationToken cancellationToken)
    {
        var network = await _network.GetSnapshotAsync(cancellationToken);
        var online = network.IsConnected &&
            (await _probe.CheckAsync(Settings.ProbeUri, Settings.ProbeTimeout, cancellationToken)).IsOnline;
        PublishSnapshot(_snapshot with
        {
            WifiName = network.Ssid ?? "未连接",
            IsConnected = online,
            AuthenticationState = Settings.AutomaticAuthenticationEnabled
                ? _coordinator.State
                : AuthenticationState.Paused
        });
    }

    private void Coordinator_StateChanged(object? sender, AuthenticationState state)
    {
        _logger.Write(new(AppLogEvent.StateChanged, state));
        PublishSnapshot(_snapshot with { AuthenticationState = state, IsConnected = state == AuthenticationState.Online });
    }

    private void Coordinator_NoticeRaised(object? sender, AuthenticationNotice notice)
    {
        _logger.Write(new(
            AppLogEvent.AuthenticationResult,
            _coordinator.State,
            notice.Kind == AuthenticationNoticeKind.Connected
                ? AuthenticationOutcome.Succeeded
                : AuthenticationOutcome.ExternalActionRequired,
            notice.ExternalAction,
            Message: notice.Message,
            Url: notice.RecoveryUri,
            TechnicalDetail: notice.TechnicalDetail));
        PublishSnapshot(_snapshot with { LastAuthenticationResult = notice.Message });
        if (notice.Kind == AuthenticationNoticeKind.ExternalActionRequired && notice.RecoveryUri is not null)
        {
            try { Process.Start(new ProcessStartInfo(notice.RecoveryUri.ToString()) { UseShellExecute = true }); }
            catch (Exception ex) { _logger.Write(new(AppLogEvent.UnexpectedError, Message: "打开认证地址失败", TechnicalDetail: ex.Message)); }
        }
        NoticeRaised?.Invoke(this, notice);
    }

    private void PublishSnapshot(RuntimeSnapshot snapshot)
    {
        _snapshot = snapshot;
        SnapshotChanged?.Invoke(this, snapshot);
    }

    private void ResetRecording()
    {
        _recordingCancellation?.Dispose();
        _recordingCancellation = null;
        _recordingTask = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _logger.Write(new(AppLogEvent.ApplicationStopped));
        _coordinator.StateChanged -= Coordinator_StateChanged;
        _coordinator.NoticeRaised -= Coordinator_NoticeRaised;
        _recordingCancellation?.Cancel();
        _lifetime.Cancel();
        try { await _monitorTask; } catch (OperationCanceledException) { }
        try { await _coordinator.WaitForIdleAsync(CancellationToken.None); } catch (OperationCanceledException) { }
        if (_recordingTask is not null)
        {
            try { await _recordingTask; } catch { }
        }
        ResetRecording();
        _lifetime.Dispose();
    }
}
