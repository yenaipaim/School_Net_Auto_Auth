using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolNetAutoAuth.App.Services;
using SchoolNetAutoAuth.Core.Configuration;
using SchoolNetAutoAuth.Infrastructure.Configuration;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Storage;
using Windows.UI;

namespace SchoolNetAutoAuth.App.ViewModels;

public partial class RecordingViewModel : ViewModelBase
{
    private readonly AppController _controller;
    private readonly DialogService _dialogs;
    private readonly FilePickerService _pickers;
    private readonly RecordingConfigurationSerializer _serializer;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(RecordAgainCommand))]
    public partial bool IsRecording { get; set; }
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    public partial RecordedClickStep? SelectedStep { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressBrush))]
    public partial string ProgressText { get; set; } = "准备就绪";

    public RecordingViewModel(AppController controller, DialogService dialogs, FilePickerService pickers, RecordingConfigurationSerializer serializer)
    {
        _controller = controller;
        _dialogs = dialogs;
        _pickers = pickers;
        _serializer = serializer;
        _controller.SettingsChanged += Controller_SettingsChanged;
        _controller.RecordingProgressChanged += Controller_RecordingProgressChanged;
        _controller.RecordingFailed += Controller_RecordingFailed;
        RefreshSteps(controller.Settings);
    }

    public ObservableCollection<RecordedClickStep> Steps { get; } = [];

    /// <summary>步骤计数读数，随集合变化刷新。</summary>
    public string StepCountText => $"{Steps.Count} 步";

    /// <summary>没有录制步骤时显示叠层空态提示。</summary>
    public bool IsEmpty => Steps.Count == 0;

    /// <summary>进度指示灯颜色：录制中取品牌色，失败取 destructive，其余取 muted。</summary>
    public SolidColorBrush ProgressBrush => new(RecordingAppearance(
        IsRecording ? "AppBrandBrush"
            : ProgressText == "录制失败" ? "AppDestructiveBrush"
            : "AppMutedFgBrush"));

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        var sequence = _controller.Settings.RecordedSequence;
        if (sequence is null) return;
        var file = await _pickers.PickSaveFileAsync("校园网认证录制.json");
        if (file is null) return;
        await FileIO.WriteTextAsync(file, _serializer.Serialize(sequence));
        ProgressText = "录制配置已导出";
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var file = await _pickers.PickOpenFileAsync();
        if (file is null) return;
        try
        {
            var sequence = _serializer.Deserialize(await FileIO.ReadTextAsync(file));
            if (_controller.Settings.RecordedSequence is not null && !await _dialogs.ConfirmAsync("覆盖录制", "当前已有录制配置，是否覆盖？", "覆盖")) return;
            await _controller.ImportRecordingAsync(sequence);
            ProgressText = "录制配置已导入";
        }
        catch (Exception ex) { await _dialogs.ShowErrorAsync("导入失败", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (IsRecording) return;
        try
        {
            await _controller.StartRecordingAsync();
            IsRecording = true;
            ProgressText = "录制中";
        }
        catch (Exception ex) { await _dialogs.ShowErrorAsync("无法开始录制", ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync()
    {
        if (!IsRecording) return;
        try
        {
            var sequence = await _controller.StopRecordingAsync();
            ProgressText = $"录制完成，共 {sequence.Clicks.Count} 个步骤";
        }
        catch (Exception ex) { await _dialogs.ShowErrorAsync("录制失败", ex.Message); }
        finally { IsRecording = false; }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedStep is null) return;
        await _controller.DeleteRecordedStepAsync(SelectedStep);
        SelectedStep = null;
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        if (!await _dialogs.ConfirmAsync("清除录制", "确认清除当前录制流程？", "清除")) return;
        await _controller.ClearRecordingAsync();
        ProgressText = "录制已清除";
    }

    [RelayCommand(CanExecute = nameof(CanRecordAgain))]
    private async Task RecordAgainAsync()
    {
        if (!await _dialogs.ConfirmAsync("重新录制", "现有步骤将被清除。继续吗？", "重新录制")) return;
        await _controller.ClearRecordingAsync();
        await StartAsync();
    }

    private static Color RecordingAppearance(string key) =>
        Application.Current.Resources.TryGetValue(key, out object? value) && value is SolidColorBrush brush
            ? brush.Color
            : Colors.Gray;

    private void Controller_SettingsChanged(object? sender, AppSettings settings) =>
        Dispatch(() => RefreshSteps(settings));

    private void Controller_RecordingProgressChanged(object? sender, string message) =>
        Dispatch(() => ProgressText = message);

    private void Controller_RecordingFailed(object? sender, string message) => Dispatch(() =>
    {
        IsRecording = false;
        ProgressText = "录制失败";
        _ = _dialogs.ShowErrorAsync("录制失败", message);
    });

    private bool CanStart() => !IsRecording;
    private bool CanStop() => IsRecording;
    private bool CanDeleteSelected() => SelectedStep is not null;
    private bool CanRecordAgain() => !IsRecording;
    private bool CanExport() => !IsRecording && _controller.Settings.RecordedSequence is not null;

    private void RefreshSteps(AppSettings settings)
    {
        Steps.Clear();
        foreach (var step in settings.RecordedSequence?.Clicks.OrderBy(step => step.Order) ?? Enumerable.Empty<RecordedClickStep>())
            Steps.Add(step);
        OnPropertyChanged(nameof(StepCountText));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
