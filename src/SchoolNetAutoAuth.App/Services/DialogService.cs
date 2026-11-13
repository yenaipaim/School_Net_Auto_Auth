using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SchoolNetAutoAuth.App.Views;

namespace SchoolNetAutoAuth.App.Services;

public sealed class DialogService(AppController controller, WindowService windows)
{
    public Task ShowInfoAsync(string title, string message) => ShowAsync(title, message, "确定");

    public Task ShowErrorAsync(string title, string message) => ShowAsync(title, message, "关闭");

    public async Task<bool> ConfirmAsync(string title, string message, string confirmText = "确定")
    {
        var dialog = CreateDialog(title, message);
        dialog.PrimaryButtonText = confirmText;
        dialog.CloseButtonText = "取消";
        dialog.DefaultButton = ContentDialogButton.Primary;
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>
    /// 首启提示：不做向导，只把「还差哪三步」讲清楚，并把「打开设置」做成主按钮。
    /// 勾选「不再提示」才落盘 FirstRunPromptShown。
    /// </summary>
    public async Task ShowFirstRunAsync()
    {
        var xamlRoot = windows.XamlRoot;
        if (xamlRoot is null) return;

        var checklist = new TextBlock
        {
            Text = "1  选择校园网 Wi-Fi\n2  填写认证入口地址\n3  完成一次认证流程录制",
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 26
        };
        var hint = new TextBlock
        {
            Text = "详细说明请阅读 README。",
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap
        };
        var suppress = new CheckBox { Content = "下次启动不再提示", IsChecked = false };

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock
        {
            Text = "配置完成前，自动认证不会运行。",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(checklist);
        content.Children.Add(hint);
        content.Children.Add(suppress);

        var dialog = new ContentDialog
        {
            Title = "使用前请先完成配置",
            Content = content,
            PrimaryButtonText = "打开设置",
            SecondaryButtonText = "查看教程",
            CloseButtonText = "知道了",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        var result = await dialog.ShowAsync();
        if (suppress.IsChecked == true)
        {
            try { await controller.MarkFirstRunPromptShownAsync(); }
            catch (Exception ex) { await ShowErrorAsync("保存失败", ex.Message); }
        }

        if (result == ContentDialogResult.Primary)
        {
            windows.Navigate("network");
        }
        else if (result == ContentDialogResult.Secondary)
        {
            OpenTutorial();
        }
    }

    private static void OpenTutorial()
    {
        try
        {
            Process.Start(new ProcessStartInfo(AboutInfo.TutorialUri.ToString()) { UseShellExecute = true });
        }
        catch
        {
            // 断网或没有默认浏览器时静默，不阻断首启流程。
        }
    }

    private async Task ShowAsync(string title, string message, string closeText)
    {
        var dialog = CreateDialog(title, message);
        dialog.CloseButtonText = closeText;
        await dialog.ShowAsync();
    }

    private ContentDialog CreateDialog(string title, string message) => new()
    {
        Title = title,
        Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
        XamlRoot = windows.XamlRoot
    };
}
