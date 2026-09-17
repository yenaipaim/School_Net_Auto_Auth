using Forms = System.Windows.Forms;

namespace SchoolNetAutoAuth.Uninstaller;

internal sealed class UninstallForm : Forms.Form
{
    private readonly Forms.CheckBox _deleteUserData;

    public bool DeleteUserData => _deleteUserData.Checked;

    public UninstallForm()
    {
        Text = "卸载校园网自动认证";
        Width = 480;
        Height = 240;
        FormBorderStyle = Forms.FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = Forms.FormStartPosition.CenterScreen;
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);

        var title = new Forms.Label { Text = "确定要卸载校园网自动认证吗？", AutoSize = true, Font = new System.Drawing.Font("Microsoft YaHei UI", 13, System.Drawing.FontStyle.Bold), Left = 24, Top = 24 };
        var detail = new Forms.Label { Text = "程序文件、开机启动项和快捷方式将被删除。", AutoSize = true, Left = 26, Top = 68 };
        _deleteUserData = new Forms.CheckBox { Text = "同时删除账号凭据、录制配置和 Edge 专用登录状态", AutoSize = true, Left = 26, Top = 104, Checked = false };
        var uninstall = new Forms.Button { Text = "卸载", DialogResult = Forms.DialogResult.OK, Width = 92, Height = 32, Left = 258, Top = 148 };
        var cancel = new Forms.Button { Text = "取消", DialogResult = Forms.DialogResult.Cancel, Width = 92, Height = 32, Left = 358, Top = 148 };
        Controls.AddRange([title, detail, _deleteUserData, uninstall, cancel]);
        AcceptButton = uninstall;
        CancelButton = cancel;
    }
}
