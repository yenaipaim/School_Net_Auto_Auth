using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolNetAutoAuth.App.Services;
using SchoolNetAutoAuth.App.Views;
using SchoolNetAutoAuth.Core.Configuration;

namespace SchoolNetAutoAuth.App.ViewModels;

public sealed record AppearanceOption(string Value, string Label, string? ColorHex = null);

public partial class GeneralSettingsViewModel : ViewModelBase
{
    private readonly AppController _controller;
    private readonly DialogService _dialogs;
    private readonly FilePickerService _pickers;
    private readonly UpdateService _updates;
    private readonly string _backgroundDirectory;
    private CancellationTokenSource? _opacitySaveCancellation;
    private CancellationTokenSource? _accentSaveCancellation;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeLabel))]
    [NotifyPropertyChangedFor(nameof(ThemeGlyph))]
    public partial string Theme { get; set; } = AppSettings.DarkTheme;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedAccent))]
    public partial string AccentColor { get; set; } = AppSettings.DefaultAccentColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomFontEnabled))]
    public partial string FontSelection { get; set; } = "system";

    [ObservableProperty]
    public partial string CustomFontFamily { get; set; } = string.Empty;

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

    public IReadOnlyList<AppearanceOption> ThemeOptions { get; } =
    [
        new(AppSettings.DarkTheme, "深色"),
        new(AppSettings.LightTheme, "浅色")
    ];

    public string ThemeLabel => Theme == AppSettings.LightTheme ? "浅色" : "深色";

    /// <summary>按钮显示的是「点击后切换到的」主题图标。</summary>
    public string ThemeGlyph => Theme == AppSettings.LightTheme ? "\uE708" : "\uE706";

    /// <summary>强调色预设，取值与 PVE-VDI 的 ACCENT_COLORS 一致。</summary>
    public IReadOnlyList<AppearanceOption> AccentOptions { get; } =
    [
        new("#0078D4", "Windows 蓝"),
        new("#3B82F6", "信号蓝"),
        new("#0D9488", "青绿"),
        new("#8B5CF6", "紫罗兰"),
        new("#E11D48", "玫红"),
        new("#F26B1D", "工业橙")
    ];

    public AppearanceOption? SelectedAccent
    {
        get => AccentOptions.FirstOrDefault(option =>
            string.Equals(option.Value, AccentColor, StringComparison.OrdinalIgnoreCase));
        set
        {
            if (value is null || _loading) return;
            AccentColor = value.Value;
            _ = SaveAppearanceAsync(theme: null, accent: value.Value, font: null);
        }
    }

    public IReadOnlyList<AppearanceOption> FontOptions { get; } =
    [
        new("system", "系统字体"),
        new("mono", "等宽字体"),
        new("custom", "自定义")
    ];

    public bool IsCustomFontEnabled => FontSelection == "custom";

    [RelayCommand]
    private void SelectAccent(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        AccentColor = hex;
        _ = SaveAppearanceAsync(null, hex, null);
    }

    [RelayCommand]
    private void ToggleTheme() =>
        _ = SaveAppearanceAsync(
            Theme == AppSettings.LightTheme ? AppSettings.DarkTheme : AppSettings.LightTheme,
            null, null);

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

    /// <summary>
    /// 手输强调色：只在格式合法时落盘，非法值留在输入框里等用户改，
    /// 避免每敲一个字符就弹一次校验错误。
    /// </summary>
    partial void OnAccentColorChanged(string value)
    {
        if (_loading) return;
        if (!LooksLikeHex(value)) return;
        _accentSaveCancellation?.Cancel();
        _accentSaveCancellation?.Dispose();
        _accentSaveCancellation = new CancellationTokenSource();
        _ = SaveAccentAsync(value, _accentSaveCancellation.Token);
    }

    private static bool LooksLikeHex(string? value) =>
        value is { Length: 4 or 7 } && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);

    private async Task SaveAccentAsync(string hex, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(400, cancellationToken);
            await _controller.SaveSettingsAsync(
                _controller.Settings with { AccentColor = hex },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("保存强调色失败", ex.Message);
        }
    }

    partial void OnBackgroundImageOpacityPercentChanged(double value)
    {
        if (_loading) return;
        _opacitySaveCancellation?.Cancel();
        _opacitySaveCancellation?.Dispose();
        _opacitySaveCancellation = new CancellationTokenSource();
        _ = SaveBackgroundOpacityAsync(value, _opacitySaveCancellation.Token);
    }

    partial void OnFontSelectionChanged(string value)
    {
        if (_loading) return;
        _ = SaveAppearanceAsync(null, null, value == "custom" ? CustomFontFamily : ResolveFamily(value));
    }

    partial void OnCustomFontFamilyChanged(string value)
    {
        if (_loading) return;
        if (FontSelection == "custom")
            _ = SaveAppearanceAsync(null, null, value.Trim());
    }

    private static string ResolveFamily(string selection) => selection switch
    {
        "mono" => "Cascadia Code, Consolas",
        _ => ""
    };

    private async Task SaveAppearanceAsync(string? theme, string? accent, string? font)
    {
        var settings = _controller.Settings with
        {
            Theme = theme ?? Theme,
            AccentColor = accent ?? AccentColor,
            InterfaceFontFamily = font is null
                ? InterfaceFontFamilyOrNull()
                : (string.IsNullOrWhiteSpace(font) ? null : font.Trim())
        };
        try
        {
            await _controller.SaveSettingsAsync(settings);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowErrorAsync("保存外观设置失败", ex.Message);
        }
    }

    private string? InterfaceFontFamilyOrNull() =>
        string.IsNullOrWhiteSpace(CustomFontFamily) ? null : CustomFontFamily.Trim();

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
            await _dialogs.ShowErrorAsync("保存背景不透明度失败", ex.Message);
        }
    }

    private void Load(AppSettings settings)
    {
        _loading = true;
        BackgroundImagePath = settings.BackgroundImagePath ?? string.Empty;
        BackgroundImageOpacityPercent = Math.Clamp(settings.BackgroundImageOpacity * 100d, 0, 100);
        Theme = settings.Theme is AppSettings.LightTheme ? AppSettings.LightTheme : AppSettings.DarkTheme;
        AccentColor = settings.AccentColor;
        if (!string.IsNullOrWhiteSpace(settings.InterfaceFontFamily))
        {
            FontSelection = "custom";
            CustomFontFamily = settings.InterfaceFontFamily;
        }
        else
        {
            FontSelection = "system";
            CustomFontFamily = string.Empty;
        }
        _loading = false;
        OnPropertyChanged(nameof(SelectedAccent));
        OnPropertyChanged(nameof(ThemeLabel));
        OnPropertyChanged(nameof(IsCustomFontEnabled));
    }
}
