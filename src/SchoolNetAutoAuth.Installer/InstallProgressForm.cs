using System.Drawing;
using System.Windows.Forms;

namespace SchoolNetAutoAuth.Installer;

internal sealed class InstallProgressForm : Form
{
    private readonly Label _status = new() { AutoSize = true, Dock = DockStyle.Fill, Text = "正在准备安装...", TextAlign = ContentAlignment.MiddleLeft };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100, Style = ProgressBarStyle.Continuous };
    private readonly Button _close = new() { AutoSize = true, Enabled = false, Text = "关闭" };

    public InstallProgressForm()
    {
        Text = "校园网自动认证";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(520, 150);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), RowCount = 4, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.Controls.Add(new Label { Text = "正在安装校园网自动认证", Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        layout.Controls.Add(_status, 0, 1);
        layout.Controls.Add(_progress, 0, 2);
        layout.Controls.Add(_close, 0, 3);
        _close.Anchor = AnchorStyles.Right;
        _close.Click += (_, _) => Close();
        Controls.Add(layout);
        Shown += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        try
        {
            await Task.Run(() => Program.Install(Report));
            Report(new(100, "安装完成，正在启动程序..."));
            _close.Enabled = true;
            await Task.Delay(700);
            Close();
        }
        catch (Exception ex)
        {
            Report(new(_progress.Value, $"安装失败：{ex.Message}"));
            _close.Enabled = true;
        }
    }

    private void Report(InstallProgress progress)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(() => Report(progress)); return; }
        _progress.Value = Math.Clamp(progress.Percent, 0, 100);
        _status.Text = progress.Message;
    }
}

internal readonly record struct InstallProgress(int Percent, string Message);
