using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolNetAutoAuth.App.Services;
using SchoolNetAutoAuth.Core.Authentication;

namespace SchoolNetAutoAuth.App.ViewModels;

public partial class StatusViewModel : ViewModelBase
{
    private readonly AppController _controller;
    private readonly DialogService _dialogs;
    private bool _initialized;

    [ObservableProperty] public partial string WifiName { get; set; } = "正在检测";
    [ObservableProperty] public partial string ConnectivityText { get; set; } = "正在检测";
    [ObservableProperty] public partial string AuthenticationStateText { get; set; } = "正在初始化";
    [ObservableProperty] public partial string LastAuthenticationResult { get; set; } = "尚未认证";
    [ObservableProperty] public partial bool AutomaticAuthenticationEnabled { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }

    public StatusViewModel(AppController controller, DialogService dialogs)
    {
        _controller = controller;
        _dialogs = dialogs;
        AutomaticAuthenticationEnabled = controller.Settings.AutomaticAuthenticationEnabled;
        ApplySnapshot(controller.Snapshot);
        controller.SnapshotChanged += Controller_SnapshotChanged;
        controller.SettingsChanged += Controller_SettingsChanged;
        _initialized = true;
    }

    public static string FormatAuthenticationState(AuthenticationState state) => state switch
    {
        AuthenticationState.WaitingForTargetWifi => "等待目标 Wi-Fi",
        AuthenticationState.CheckingConnectivity => "检查联网",
        AuthenticationState.Online => "已联网",
        AuthenticationState.Authenticating => "正在认证",
        AuthenticationState.WaitingForCredentials => "缺少凭据",
        AuthenticationState.RetryDelay => "等待重试",
        AuthenticationState.ExternalActionCooldown => "等待人工处理",
        AuthenticationState.Paused => "自动认证已暂停",
        _ => "需要处理"
    };

    [RelayCommand]
    private async Task AuthenticateAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try { await _controller.AuthenticateNowAsync(); }
        catch (Exception ex) { await _dialogs.ShowErrorAsync("认证失败", ex.Message); }
        finally { IsBusy = false; }
    }

    partial void OnAutomaticAuthenticationEnabledChanged(bool value)
    {
        if (!_initialized) return;
        _ = SaveAutomaticAuthenticationAsync(value);
    }

    private async Task SaveAutomaticAuthenticationAsync(bool value)
    {
        try
        {
            await _controller.SaveSettingsAsync(_controller.Settings with { AutomaticAuthenticationEnabled = value });
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("保存失败", ex.Message);
        }
    }

    private void Controller_SnapshotChanged(object? sender, RuntimeSnapshot snapshot) =>
        Dispatch(() => ApplySnapshot(snapshot));

    private void Controller_SettingsChanged(object? sender, Core.Configuration.AppSettings settings) =>
        Dispatch(() => AutomaticAuthenticationEnabled = settings.AutomaticAuthenticationEnabled);

    private void ApplySnapshot(RuntimeSnapshot snapshot)
    {
        WifiName = snapshot.WifiName;
        ConnectivityText = snapshot.IsConnected ? "互联网可用" : "互联网不可用";
        AuthenticationStateText = FormatAuthenticationState(snapshot.AuthenticationState);
        LastAuthenticationResult = snapshot.LastAuthenticationResult;
    }
}
