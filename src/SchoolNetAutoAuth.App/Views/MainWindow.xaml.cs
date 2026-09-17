using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;
using WpfMessageBox = System.Windows.MessageBox;
using SchoolNetAutoAuth.Core.Authentication;
using SchoolNetAutoAuth.Core.Configuration;
using SchoolNetAutoAuth.Core.Contracts;
using SchoolNetAutoAuth.Infrastructure.Automation;
using SchoolNetAutoAuth.Infrastructure.Configuration;
using SchoolNetAutoAuth.Infrastructure.Connectivity;
using SchoolNetAutoAuth.Infrastructure.Credentials;
using SchoolNetAutoAuth.Infrastructure.Network;
using SchoolNetAutoAuth.Infrastructure.Startup;

namespace SchoolNetAutoAuth.App.Views;

public partial class MainWindow : Window, IDisposable
{
    private readonly JsonSettingsStore _settingsStore;
    private readonly WindowsCredentialStore _credentials = new();
    private readonly HttpConnectivityProbe _probe = new();
    private readonly WindowsWifiMonitor _wifi = new();
    private readonly FailedEdgeSessionKeeper _failedSessions = new();
    private readonly EdgeSessionFactory _sessions;
    private readonly LocatorResolver _resolver = new();
    private readonly AuthenticationCoordinator _coordinator;
    private readonly RegistryStartupManager _startup = new();
    private readonly Forms.NotifyIcon _tray;
    private AppSettings _settings = AppSettings.CreateDefault();
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _recording;
    private Task<RecordedClickSequence>? _recordingTask;
    private bool _disposed;

    public MainWindow(bool background)
    {
        InitializeComponent();
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SchoolNetAutoAuth");
        _settingsStore = new(root);
        _sessions = new(Path.Combine(root, "EdgeProfile"));
        _coordinator = new(_wifi, _probe, new PlaywrightAuthenticationRunner(_sessions, _resolver, _credentials, _probe, _failedSessions));
        _coordinator.StateChanged += (_, state) => Dispatcher.Invoke(() => UpdateStatus(state));
        _tray = BuildTray();
        Loaded += async (_, _) =>
        {
            _settings = await _settingsStore.LoadAsync(CancellationToken.None);
            LoadFields();
            if (background) Hide();
            _ = MonitorAsync(_lifetime.Token);
        };
        Closing += (_, args) => { if (!_disposed) { args.Cancel = true; Hide(); } };
    }

    private Forms.NotifyIcon BuildTray()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "App.ico");
        var tray = new Forms.NotifyIcon { Icon = File.Exists(iconPath) ? new System.Drawing.Icon(iconPath) : System.Drawing.SystemIcons.Application, Visible = true, Text = "校园网自动认证" };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("立即认证", null, async (_, _) => await AuthenticateAsync());
        menu.Items.Add("打开设置", null, (_, _) => ShowFromTray());
        menu.Items.Add("退出", null, (_, _) => System.Windows.Application.Current.Shutdown());
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => ShowFromTray();
        return tray;
    }

    private async Task MonitorAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try { await _coordinator.EvaluateAsync(_settings, token); await Task.Delay(_settings.NetworkCheckInterval, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            catch { await Task.Delay(TimeSpan.FromSeconds(10), token); }
        }
    }

    private async Task AuthenticateAsync()
    {
        try { await SaveSettingsAsync(); await _coordinator.EvaluateAsync(_settings, CancellationToken.None); }
        catch (Exception ex) { Dispatcher.Invoke(() => WpfMessageBox.Show(this, ex.Message, "认证失败", MessageBoxButton.OK, MessageBoxImage.Error)); }
    }

    private async void StartRecord_Click(object sender, RoutedEventArgs e)
    {
        if (_recordingTask is not null) return;
        try
        {
            await SaveSettingsAsync();
            _recording = new CancellationTokenSource();
            StartRecordButton.IsEnabled = false;
            StopRecordButton.IsEnabled = true;
            var progress = new Progress<RecorderStep>(step => StatusText.Text = step.Message);
            _recordingTask = new PlaywrightActionRecorder(_sessions, _resolver).RecordAsync(new(_settings.PortalUri), progress, _recording.Token);
            _ = ObserveRecordingFailureAsync(_recordingTask);
        }
        catch (Exception ex) { ResetRecording(); WpfMessageBox.Show(this, ex.Message, "无法开始录制", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void StopRecord_Click(object sender, RoutedEventArgs e)
    {
        if (_recordingTask is null || _recording is null) return;
        _recording.Cancel();
        try
        {
            var sequence = await _recordingTask;
            if (sequence.Clicks.Count == 0) throw new InvalidDataException("没有录制到可重放的点击操作。");
            _settings = _settings with { RecordedSequence = sequence };
            await _settingsStore.SaveAsync(_settings, CancellationToken.None);
            RefreshSteps();
            WpfMessageBox.Show(this, $"录制完成，共 {sequence.Clicks.Count} 个点击步骤。", "校园网自动认证", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { WpfMessageBox.Show(this, ex.Message, "录制失败", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { ResetRecording(); }
    }

    private async Task ObserveRecordingFailureAsync(Task<RecordedClickSequence> task)
    {
        try { await task; }
        catch (OperationCanceledException) when (_recording?.IsCancellationRequested == true) { }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                ResetRecording();
                WpfMessageBox.Show(this, ex.Message, "录制失败", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
    }

    private async void ClearRecord_Click(object sender, RoutedEventArgs e)
    {
        if (WpfMessageBox.Show(this, "确认清除当前录制流程？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _settings = _settings with { RecordedSequence = null };
        await _settingsStore.SaveAsync(_settings, CancellationToken.None);
        RefreshSteps();
    }

    private async void DeleteStep_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.RecordedSequence is null || StepsList.SelectedItem is not RecordedClickStep selected) return;
        var clicks = _settings.RecordedSequence.Clicks
            .Where(step => step != selected)
            .OrderBy(step => step.Order)
            .Select((step, index) => step with { Order = index + 1 })
            .ToArray();
        _settings = _settings with { RecordedSequence = _settings.RecordedSequence with { Clicks = clicks } };
        await _settingsStore.SaveAsync(_settings, CancellationToken.None);
        RefreshSteps();
    }

    private void ResetRecording()
    {
        _recording?.Dispose(); _recording = null; _recordingTask = null;
        StartRecordButton.IsEnabled = true; StopRecordButton.IsEnabled = false;
    }

    private async void Authenticate_Click(object sender, RoutedEventArgs e) => await AuthenticateAsync();
    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try { await SaveSettingsAsync(); WpfMessageBox.Show(this, "设置已保存。", "校园网自动认证", MessageBoxButton.OK, MessageBoxImage.Information); }
        catch (Exception ex) { WpfMessageBox.Show(this, ex.Message, "设置无效", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private async void SaveCredential_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(UsernameBox.Text) || PasswordBox.Password.Length == 0) { WpfMessageBox.Show(this, "请输入账号和密码。"); return; }
        await _credentials.WriteAsync(new(UsernameBox.Text.Trim(), PasswordBox.Password), CancellationToken.None); PasswordBox.Clear(); StatusText.Text = "账号密码已安全保存到 Windows 凭据管理器。";
    }
    private async void DeleteCredential_Click(object sender, RoutedEventArgs e)
    {
        if (WpfMessageBox.Show(this, "确认删除已保存的校园网凭据？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) await _credentials.DeleteAsync(CancellationToken.None);
    }

    private async Task SaveSettingsAsync()
    {
        if (!Uri.TryCreate(PortalBox.Text.Trim(), UriKind.Absolute, out var portal) || !Uri.TryCreate(ProbeBox.Text.Trim(), UriKind.Absolute, out var probe)) throw new InvalidDataException("请输入有效的认证地址和联网检测地址。");
        if (!int.TryParse(AttemptsBox.Text, out var attempts) || !double.TryParse(RetryIntervalBox.Text, out var retry) || !double.TryParse(AuthTimeoutBox.Text, out var timeout)) throw new InvalidDataException("重试参数必须是数字。");
        _settings = _settings with { TargetSsid = SsidBox.Text.Trim(), PortalUri = portal, ProbeUri = probe, RetryInterval = TimeSpan.FromSeconds(retry), MaximumAttempts = attempts, AuthenticationTimeout = TimeSpan.FromSeconds(timeout), StartWithWindows = StartupBox.IsChecked == true };
        var validation = _settings.Validate(); if (!validation.IsValid) throw new InvalidDataException(string.Join(Environment.NewLine, validation.Errors));
        await _settingsStore.SaveAsync(_settings, CancellationToken.None);
        _startup.SetEnabled(_settings.StartWithWindows, Environment.ProcessPath ?? throw new InvalidOperationException("无法确定程序路径。"));
    }

    private void LoadFields()
    {
        SsidBox.Text = _settings.TargetSsid; PortalBox.Text = _settings.PortalUri.ToString(); ProbeBox.Text = _settings.ProbeUri.ToString(); StartupBox.IsChecked = _settings.StartWithWindows;
        RetryIntervalBox.Text = _settings.RetryInterval.TotalSeconds.ToString("0"); AttemptsBox.Text = _settings.MaximumAttempts.ToString(); AuthTimeoutBox.Text = _settings.AuthenticationTimeout.TotalSeconds.ToString("0"); RefreshSteps();
    }
    private void RefreshSteps() => StepsList.ItemsSource = _settings.RecordedSequence?.Clicks.OrderBy(x => x.Order).ToArray() ?? [];

    private void UpdateStatus(AuthenticationState state)
    {
        var text = state switch { AuthenticationState.WaitingForTargetWifi => "等待目标 Wi-Fi", AuthenticationState.CheckingConnectivity => "检查联网", AuthenticationState.Online => "已联网", AuthenticationState.Authenticating => "正在认证", AuthenticationState.WaitingForCredentials => "缺少凭据", AuthenticationState.RetryDelay => "等待重试", _ => "需要重新录制或手动处理" };
        StatusText.Text = $"当前状态：{text}"; var tip = $"校园网自动认证 - {text}"; _tray.Text = tip[..Math.Min(63, tip.Length)];
        if (state is AuthenticationState.WaitingForCredentials or AuthenticationState.ActionRequired) _tray.ShowBalloonTip(5000, "校园网自动认证", text, Forms.ToolTipIcon.Warning);
    }
    private void ShowFromTray() { Show(); WindowState = WindowState.Normal; Activate(); }
    public void Dispose() { if (_disposed) return; _disposed = true; _recording?.Cancel(); _lifetime.Cancel(); _recording?.Dispose(); _lifetime.Dispose(); _tray.Visible = false; _tray.Dispose(); _probe.Dispose(); _failedSessions.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
}
