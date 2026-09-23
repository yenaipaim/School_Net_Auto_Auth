using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SchoolNetAutoAuth.App.Services;

public sealed class DialogService(WindowService windows)
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
