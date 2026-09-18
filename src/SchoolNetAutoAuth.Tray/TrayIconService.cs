using System.Drawing;
using Forms = System.Windows.Forms;

namespace SchoolNetAutoAuth.Tray;

public sealed class TrayIconService : IDisposable
{
    private Forms.NotifyIcon? _icon;
    private Forms.ToolStripMenuItem? _statusItem;

    public event EventHandler? AuthenticateRequested;
    public event EventHandler? ShowRequested;
    public event EventHandler? ExitRequested;

    public void Initialize(string iconPath)
    {
        if (_icon is not null) return;
        _statusItem = new Forms.ToolStripMenuItem("当前状态：正在初始化") { Enabled = false };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("立即认证", null, (_, _) => AuthenticateRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("打开主界面", null, (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(_statusItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));
        _icon = new Forms.NotifyIcon
        {
            Icon = File.Exists(iconPath) ? new Icon(iconPath) : SystemIcons.Application,
            Text = "校园网自动认证",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateStatus(string status)
    {
        if (_icon is null || _statusItem is null) return;
        _statusItem.Text = $"当前状态：{status}";
        var tip = $"校园网自动认证 - {status}";
        _icon.Text = tip[..Math.Min(63, tip.Length)];
    }

    public void Dispose()
    {
        if (_icon is null) return;
        _icon.Visible = false;
        _icon.Dispose();
        _icon = null;
    }
}
