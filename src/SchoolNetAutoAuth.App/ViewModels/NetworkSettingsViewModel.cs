using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolNetAutoAuth.App.Services;

namespace SchoolNetAutoAuth.App.ViewModels;

public partial class NetworkSettingsViewModel : ViewModelBase
{
    private readonly AppController _controller;
    private readonly DialogService _dialogs;

    [ObservableProperty] public partial string TargetSsid { get; set; } = string.Empty;
    [ObservableProperty] public partial string PortalUri { get; set; } = string.Empty;
    [ObservableProperty] public partial string ProbeUri { get; set; } = string.Empty;
    [ObservableProperty] public partial double NetworkCheckIntervalSeconds { get; set; }
    [ObservableProperty] public partial bool AutomaticAuthenticationEnabled { get; set; }

    public NetworkSettingsViewModel(AppController controller, DialogService dialogs)
    {
        _controller = controller;
        _dialogs = dialogs;
        Load(controller.Settings);
        controller.SettingsChanged += (_, settings) => Dispatch(() => Load(settings));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            if (!Uri.TryCreate(PortalUri.Trim(), UriKind.Absolute, out var portal) ||
                !Uri.TryCreate(ProbeUri.Trim(), UriKind.Absolute, out var probe))
                throw new InvalidDataException("请输入有效的认证地址和联网检测地址。");
            var settings = _controller.Settings with
            {
                TargetSsid = TargetSsid.Trim(),
                PortalUri = portal,
                ProbeUri = probe,
                NetworkCheckInterval = TimeSpan.FromSeconds(NetworkCheckIntervalSeconds),
                AutomaticAuthenticationEnabled = AutomaticAuthenticationEnabled
            };
            await _controller.SaveSettingsAsync(settings);
            await _dialogs.ShowInfoAsync("网络设置", "设置已保存。");
        }
        catch (Exception ex) { await _dialogs.ShowErrorAsync("设置无效", ex.Message); }
    }

    private void Load(Core.Configuration.AppSettings settings)
    {
        TargetSsid = settings.TargetSsid;
        PortalUri = settings.PortalUri.ToString();
        ProbeUri = settings.ProbeUri.ToString();
        NetworkCheckIntervalSeconds = settings.NetworkCheckInterval.TotalSeconds;
        AutomaticAuthenticationEnabled = settings.AutomaticAuthenticationEnabled;
    }
}
