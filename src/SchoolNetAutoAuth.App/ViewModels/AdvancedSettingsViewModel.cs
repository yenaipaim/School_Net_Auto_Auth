using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolNetAutoAuth.App.Services;

namespace SchoolNetAutoAuth.App.ViewModels;

public partial class AdvancedSettingsViewModel : ViewModelBase
{
    private readonly AppController _controller;
    private readonly DialogService _dialogs;
    private readonly SystemActionService _systemActions;

    [ObservableProperty] public partial double RetryIntervalSeconds { get; set; }
    [ObservableProperty] public partial int MaximumAttempts { get; set; }
    [ObservableProperty] public partial double AuthenticationTimeoutSeconds { get; set; }
    [ObservableProperty] public partial bool StartWithWindows { get; set; }
    [ObservableProperty] public partial string Username { get; set; } = string.Empty;
    [ObservableProperty] public partial string Password { get; set; } = string.Empty;

    public AdvancedSettingsViewModel(AppController controller, DialogService dialogs, SystemActionService systemActions)
    {
        _controller = controller;
        _dialogs = dialogs;
        _systemActions = systemActions;
        Load(controller.Settings);
        controller.SettingsChanged += (_, settings) => Dispatch(() => Load(settings));
    }

    public string LogPath => _systemActions.LogPath;
    public string EdgeProfilePath => _systemActions.EdgeProfilePath;

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            var settings = _controller.Settings with
            {
                RetryInterval = TimeSpan.FromSeconds(RetryIntervalSeconds),
                MaximumAttempts = MaximumAttempts,
                AuthenticationTimeout = TimeSpan.FromSeconds(AuthenticationTimeoutSeconds),
                StartWithWindows = StartWithWindows
            };
            await _controller.SaveSettingsAsync(settings);
            await _dialogs.ShowInfoAsync("高级设置", "设置已保存。");
        }
        catch (Exception ex) { await _dialogs.ShowErrorAsync("设置无效", ex.Message); }
    }

    [RelayCommand]
    private async Task SaveCredentialAsync()
    {
        try
        {
            await _controller.SaveCredentialAsync(Username, Password);
            Password = string.Empty;
            await _dialogs.ShowInfoAsync("凭据", "账号密码已保存到 Windows 凭据管理器。");
        }
        catch (Exception ex) { await _dialogs.ShowErrorAsync("无法保存凭据", ex.Message); }
    }

    [RelayCommand]
    private async Task DeleteCredentialAsync()
    {
        if (!await _dialogs.ConfirmAsync("删除凭据", "确认删除已保存的校园网凭据？", "删除")) return;
        await _controller.DeleteCredentialAsync();
    }

    [RelayCommand] private void OpenLogDirectory() => _systemActions.OpenDataDirectory();
    [RelayCommand] private void OpenEdgeProfileDirectory() => _systemActions.OpenEdgeProfileDirectory();

    private void Load(Core.Configuration.AppSettings settings)
    {
        RetryIntervalSeconds = settings.RetryInterval.TotalSeconds;
        MaximumAttempts = settings.MaximumAttempts;
        AuthenticationTimeoutSeconds = settings.AuthenticationTimeout.TotalSeconds;
        StartWithWindows = settings.StartWithWindows;
    }
}
