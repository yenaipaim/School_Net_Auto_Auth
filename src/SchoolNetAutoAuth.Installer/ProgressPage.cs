using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace SchoolNetAutoAuth.Installer;

internal sealed class ProgressPage : InstallerPage
{
    private readonly ThinProgressBar _bar;
    private readonly Label _percent;
    private readonly Label _elapsed;
    private readonly StageList _stages;
    private readonly FlatButton _cancel;
    private readonly System.Windows.Forms.Timer _clock = new() { Interval = 1000 };
    private readonly Stopwatch _stopwatch = new();

    internal event EventHandler? CancelRequested;

    internal ProgressPage()
    {
        var title = MakeText("正在安装", 13, InstallerTheme.Fg);
        Place(this, title, ContentLeft, InstallerMetrics.Px(56), InstallerMetrics.Px(160), InstallerMetrics.Px(20));

        _elapsed = MakeText("已用时 0 秒", 11, InstallerTheme.MutedFg, mono: true);
        _elapsed.TextAlign = ContentAlignment.TopRight;
        Place(this, _elapsed, ContentLeft + ContentWidth - InstallerMetrics.Px(160), InstallerMetrics.Px(56),
            InstallerMetrics.Px(160), InstallerMetrics.Px(16));

        _bar = new ThinProgressBar { Height = InstallerMetrics.Px(4) };
        Place(this, _bar, ContentLeft, InstallerMetrics.Px(84), ContentWidth, InstallerMetrics.Px(4));

        _percent = MakeText("0%", 26, InstallerTheme.Fg, mono: true, style: FontStyle.Bold);
        Place(this, _percent, ContentLeft, InstallerMetrics.Px(100), ContentWidth, InstallerMetrics.Px(36));

        _stages = new StageList { BackColor = InstallerTheme.Bg };
        Place(this, _stages, ContentLeft, InstallerMetrics.Px(152), ContentWidth,
            InstallerMetrics.Px(24) * StageList.Count);

        _cancel = SecondaryButton("停止");
        Place(this, _cancel, ContentLeft + ContentWidth - _cancel.Width, BottomButtonTop,
            _cancel.Width, _cancel.Height);
        _cancel.Click += (_, _) =>
        {
            MarkRollingBack();
            CancelRequested?.Invoke(this, EventArgs.Empty);
        };

        _clock.Tick += (_, _) => UpdateElapsed();
    }

    internal void Begin()
    {
        _bar.Value = 0;
        _percent.Text = "0%";
        _stages.Reset();
        _cancel.Text = "停止";
        _cancel.Enabled = true;
        _stopwatch.Restart();
        _clock.Start();
        UpdateElapsed();
    }

    internal void Report(InstallProgress progress)
    {
        _bar.Value = progress.Percent;
        _percent.Text = progress.Percent + "%";

        var activeIndex = (int)progress.Stage;
        for (var index = 0; index < StageList.Count; index++)
        {
            var state = progress.Percent >= 100 || index < activeIndex
                ? StageState.Done
                : index == activeIndex ? StageState.Active : StageState.Pending;
            var detail = index == (int)InstallStage.Extract && progress.TotalItems > 0 && state != StageState.Done
                ? $"{progress.CompletedItems} / {progress.TotalItems}"
                : null;
            _stages.SetState(index, state, detail);
        }
    }

    internal void MarkComplete()
    {
        _bar.Value = 100;
        _percent.Text = "100%";
        _stages.CompleteAll();
        _clock.Stop();
        UpdateElapsed();
        _cancel.Enabled = false;
    }

    /// <summary>点「停止」或关窗后立即把按钮变成回滚提示并禁用。</summary>
    internal void MarkRollingBack()
    {
        _cancel.Text = "正在回滚…";
        _cancel.Enabled = false;
    }

    internal void Stop()
    {
        _clock.Stop();
        _stopwatch.Stop();
    }

    private void UpdateElapsed()
    {
        var seconds = (int)_stopwatch.Elapsed.TotalSeconds;
        _elapsed.Text = seconds < 60
            ? $"已用时 {seconds} 秒"
            : $"已用时 {seconds / 60} 分 {seconds % 60} 秒";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _clock.Dispose();
        base.Dispose(disposing);
    }
}
