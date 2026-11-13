using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolNetAutoAuth.App.Services;
using SchoolNetAutoAuth.Core.Configuration;
using SchoolNetAutoAuth.Core.Contracts;

namespace SchoolNetAutoAuth.App.ViewModels;

public partial class NetworkSettingsViewModel : ViewModelBase
{
    private const string DetectionEndpoint = "http://www.msftconnecttest.com/connecttest.txt";
    private const string DetectionFailedHint = "未检测到认证入口。请连上校园网后打开浏览器，复制地址栏里的地址。";

    private static readonly Regex UrlPattern = new(@"https?://[^\s""'<>]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AppController _controller;
    private readonly DialogService _dialogs;
    private readonly IWifiScanner _scanner;

    [ObservableProperty] public partial string TargetSsid { get; set; } = string.Empty;
    [ObservableProperty] public partial string PortalUri { get; set; } = string.Empty;
    [ObservableProperty] public partial string ProbeUri { get; set; } = string.Empty;
    [ObservableProperty] public partial double NetworkCheckIntervalSeconds { get; set; }
    [ObservableProperty] public partial bool AutomaticAuthenticationEnabled { get; set; }
    [ObservableProperty] public partial bool IsScanning { get; set; }
    [ObservableProperty] public partial string ScanSummary { get; set; } = "点「重新扫描」获取附近的 Wi-Fi";
    [ObservableProperty] public partial string PortalHint { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsDetectingPortal { get; set; }

    public ObservableCollection<WifiNetwork> NearbyNetworks { get; } = [];

    public NetworkSettingsViewModel(AppController controller, DialogService dialogs, IWifiScanner scanner)
    {
        _controller = controller;
        _dialogs = dialogs;
        _scanner = scanner;
        Load(controller.Settings);
        controller.SettingsChanged += (_, settings) => Dispatch(() => Load(settings));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            Uri? portal = null;
            var portalText = PortalUri.Trim();
            if (portalText.Length > 0)
            {
                if (!Uri.TryCreate(portalText, UriKind.Absolute, out portal))
                    throw new InvalidDataException("认证地址必须是完整地址，例如 http://2.2.2.2。");
            }

            var probeText = ProbeUri.Trim();
            if (probeText.Length == 0) probeText = AppSettings.DefaultProbeUriText;
            if (!Uri.TryCreate(probeText, UriKind.Absolute, out var probe) || probe.Scheme != Uri.UriSchemeHttps)
                throw new InvalidDataException("联网检测地址必须是 HTTPS 完整地址。");

            var settings = _controller.Settings with
            {
                TargetSsid = TargetSsid.Trim(),
                PortalUri = portal,
                ProbeUri = probe,
                NetworkCheckInterval = TimeSpan.FromSeconds(NetworkCheckIntervalSeconds),
                AutomaticAuthenticationEnabled = AutomaticAuthenticationEnabled
            };
            await _controller.SaveSettingsAsync(settings);
            await _dialogs.ShowInfoAsync("网络设置", settings.IsConfigured
                ? "设置已保存。"
                : "设置已保存，但目标 Wi-Fi 与认证地址尚未填全，自动认证不会运行。");
        }
        catch (Exception ex) { await _dialogs.ShowErrorAsync("设置无效", ex.Message); }
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsScanning) return;
        IsScanning = true;
        ScanSummary = "正在扫描…";
        try
        {
            var networks = await _scanner.ScanAsync(CancellationToken.None);
            NearbyNetworks.Clear();
            foreach (var network in networks) NearbyNetworks.Add(network);
            ScanSummary = networks.Count > 0
                ? $"已扫描到 {networks.Count} 个网络 · 刚刚"
                : "没有扫描到 Wi-Fi，可直接输入名称";
        }
        catch (Exception)
        {
            // 扫描失败不影响手输：保留上一次结果，只改提示文案。
            ScanSummary = "扫描失败，可直接手动输入 Wi-Fi 名称";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task DetectPortalAsync()
    {
        if (IsDetectingPortal) return;
        IsDetectingPortal = true;
        PortalHint = string.Empty;
        try
        {
            using var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectTimeout = TimeSpan.FromSeconds(3)
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
            using var response = await client.GetAsync(DetectionEndpoint);
            var detected = response.Headers.Location?.ToString();
            if (detected is null && (int)response.StatusCode == 511)
            {
                var body = await response.Content.ReadAsStringAsync();
                detected = UrlPattern.Match(body) is { Success: true } match ? match.Value : null;
            }

            if (!string.IsNullOrWhiteSpace(detected) && Uri.TryCreate(detected, UriKind.Absolute, out var uri))
            {
                PortalUri = uri.ToString();
                PortalHint = "已填入检测到的认证入口，保存前请确认地址正确。";
            }
            else
            {
                PortalHint = DetectionFailedHint;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            PortalHint = DetectionFailedHint;
        }
        finally
        {
            IsDetectingPortal = false;
        }
    }

    private void Load(AppSettings settings)
    {
        TargetSsid = settings.TargetSsid;
        PortalUri = settings.PortalUri?.ToString() ?? string.Empty;
        ProbeUri = settings.ProbeUri.ToString();
        NetworkCheckIntervalSeconds = settings.NetworkCheckInterval.TotalSeconds;
        AutomaticAuthenticationEnabled = settings.AutomaticAuthenticationEnabled;
    }
}
