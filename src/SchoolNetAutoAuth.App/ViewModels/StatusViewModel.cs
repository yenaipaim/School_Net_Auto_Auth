using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using SchoolNetAutoAuth.App.Services;
using SchoolNetAutoAuth.Core.Authentication;

namespace SchoolNetAutoAuth.App.ViewModels;

public partial class StatusViewModel : ViewModelBase
{
    private readonly AppController _controller;
    private readonly DialogService _dialogs;
    private bool _initialized;
    private bool _suppressWriteBack;

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

    /// <summary>联网状态读数颜色：可用取 success，不可用取 destructive。</summary>
    public SolidColorBrush ConnectivityBrush =>
        AppearanceBrush(ConnectivityText == "互联网可用" ? "AppSuccessBrush" : "AppDestructiveBrush");

    /// <summary>认证状态读数颜色：与标题栏状态点同一套语义色。</summary>
    public SolidColorBrush StateBrush => AppearanceBrush(_controller.Snapshot.AuthenticationState switch
    {
        AuthenticationState.Online => "AppSuccessBrush",
        AuthenticationState.Authenticating or AuthenticationState.CheckingConnectivity => "AppInfoBrush",
        AuthenticationState.RetryDelay or AuthenticationState.WaitingForCredentials => "AppWarningBrush",
        AuthenticationState.ExternalActionCooldown => "AppDestructiveBrush",
        _ => "AppMutedFgBrush"
    });

    public string PortalUriText => _controller.Settings.PortalUri.ToString();
    public string ProbeUriText => _controller.Settings.ProbeUri.ToString();

    private static SolidColorBrush AppearanceBrush(string key) =>
        new((Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out object? value)
             && value is SolidColorBrush brush) ? brush.Color : Colors.Gray);

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
        // 设置变更事件会把外部的值同步进来，这不是用户操作，不能再写回磁盘。
        if (!_initialized || _suppressWriteBack) return;
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

    private void Controller_SettingsChanged(object? sender, Core.Configuration.AppSettings settings) => Dispatch(() =>
    {
        _suppressWriteBack = true;
        AutomaticAuthenticationEnabled = settings.AutomaticAuthenticationEnabled;
        _suppressWriteBack = false;
        OnPropertyChanged(nameof(PortalUriText));
        OnPropertyChanged(nameof(ProbeUriText));
    });

    private void ApplySnapshot(RuntimeSnapshot snapshot)
    {
        WifiName = snapshot.WifiName;
        ConnectivityText = snapshot.IsConnected ? "互联网可用" : "互联网不可用";
        AuthenticationStateText = FormatAuthenticationState(snapshot.AuthenticationState);
        LastAuthenticationResult = snapshot.LastAuthenticationResult;
        OnPropertyChanged(nameof(ConnectivityBrush));
        OnPropertyChanged(nameof(StateBrush));
    }
}
