using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolNetAutoAuth.App.Services;
using SchoolNetAutoAuth.App.Views;

namespace SchoolNetAutoAuth.App.ViewModels;

public partial class GeneralSettingsViewModel : ViewModelBase
{
    private readonly AppController _controller;
    private readonly DialogService _dialogs;
    private readonly FilePickerService _pickers;
    private readonly UpdateService _updates;
    private readonly string _backgroundDirectory;
    private CancellationTokenSource? _opacitySaveCancellation;
    private bool _loading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundImageText))]
    public partial string BackgroundImagePath { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCheckUpdates))]
    public partial bool IsCheckingUpdates { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackgroundImageOpacityText))]
    public partial double BackgroundImageOpacityPercent { get; set; } = 45;

    public GeneralSettingsViewModel(
        AppController controller,
        DialogService dialogs,
        FilePickerService pickers,
        UpdateService updates)
    {
        _controller = controller;
        _dialogs = dialogs;
        _pickers = pickers;
        _updates = updates;
        _backgroundDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SchoolNetAutoAuth",
            "backgrounds");
        Load(controller.Settings);
        controller.SettingsChanged += (_, settings) => Dispatch(() => Load(settings));
    }

    public string CurrentVersion =>
        (typeof(App).Assembly.GetName().Version ?? new Version(1, 0)).ToString(3);

    public string Author => AboutInfo.Author;
    public Uri RepositoryUri => AboutInfo.RepositoryUri;
    public string GroupNumber => AboutInfo.GroupNumber;
    public bool CanCheckUpdates => !IsCheckingUpdates;
    public string BackgroundImageText =>
        string.IsNullOrWhiteSpace(BackgroundImagePath) ? "未设置背景图像" : BackgroundImagePath;
    public string BackgroundImageOpacityText => $"{BackgroundImageOpacityPercent:0}%";

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingUpdates) return;
        IsCheckingUpdates = true;
        try
        {
            var result = await _updates.CheckAsync(
                typeof(App).Assembly.GetName().Version ?? new Version(1, 0),
                CancellationToken.None);
            if (!result.IsUpdateAvailable || result.ReleaseUri is null)
            {
                var message = result.LatestVersion is null
                    ? "当前没有可用的发布版本。"
                    : $"当前已是最新版本：{result.LatestVersion.ToString(3)}";
                await _dialogs.ShowInfoAsync("版本更新", message);
                return;
            }

            var latestVersion = result.LatestVersion;
            if (latestVersion is null || result.ReleaseUri is null) return;
            var confirmed = await _dialogs.ConfirmAsync(
                "发现新版本",
                $"当前版本：{result.CurrentVersion.ToString(3)}\n最新版本：{latestVersion.ToString(3)}\n\n是否打开下载页面？",
                "打开");
            if (confirmed)
                Process.Start(new ProcessStartInfo(result.ReleaseUri.ToString()) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("检查更新失败", ex.Message);
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    [RelayCommand]
    private async Task ChooseBackgroundImageAsync()
    {
        var file = await _pickers.PickOpenFileAsync("背景图像", ".png", ".jpg", ".jpeg", ".bmp");
        if (file is null) return;

        try
        {
            Directory.CreateDirectory(_backgroundDirectory);
            var extension = file.FileType.ToLowerInvariant();
            var targetPath = Path.Combine(_backgroundDirectory, $"background{extension}");
            await using (var source = await file.OpenStreamForReadAsync())
            await using (var target = new FileStream(
                targetPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true))
            {
                await source.CopyToAsync(target);
            }

            foreach (var existing in Directory.EnumerateFiles(_backgroundDirectory, "background.*"))
            {
                if (!string.Equals(existing, targetPath, StringComparison.OrdinalIgnoreCase))
                    File.Delete(existing);
            }

            await SaveBackgroundPathAsync(targetPath);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("设置背景图像失败", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ClearBackgroundImageAsync()
    {
        if (string.IsNullOrWhiteSpace(BackgroundImagePath))
        {
            await _dialogs.ShowInfoAsync("背景图像", "当前没有设置背景图像。");
            return;
        }
        if (!await _dialogs.ConfirmAsync("清除背景图像", "确认清除当前背景图像？", "清除")) return;

        var path = BackgroundImagePath;
        await SaveBackgroundPathAsync(null);
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // The setting is already cleared; a stale cache file is harmless.
        }
    }

    private Task SaveBackgroundPathAsync(string? path) =>
        _controller.SaveSettingsAsync(_controller.Settings with { BackgroundImagePath = path });

    partial void OnBackgroundImageOpacityPercentChanged(double value)
    {
        if (_loading) return;
        _opacitySaveCancellation?.Cancel();
        _opacitySaveCancellation?.Dispose();
        _opacitySaveCancellation = new CancellationTokenSource();
        _ = SaveBackgroundOpacityAsync(value, _opacitySaveCancellation.Token);
    }

    private async Task SaveBackgroundOpacityAsync(double percent, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            await _controller.SaveSettingsAsync(
                _controller.Settings with { BackgroundImageOpacity = Math.Clamp(percent / 100d, 0, 1) },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("保存背景透明度失败", ex.Message);
        }
    }

    private void Load(Core.Configuration.AppSettings settings)
    {
        _loading = true;
        BackgroundImagePath = settings.BackgroundImagePath ?? string.Empty;
        BackgroundImageOpacityPercent = Math.Clamp(settings.BackgroundImageOpacity * 100d, 0, 100);
        _loading = false;
    }
}
