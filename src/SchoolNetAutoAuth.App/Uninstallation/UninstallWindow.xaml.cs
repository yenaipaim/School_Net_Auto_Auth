using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace SchoolNetAutoAuth.App.Uninstallation;

public sealed partial class UninstallWindow : Window
{
    private readonly TaskCompletionSource<bool?> _completion = new();

    public UninstallWindow()
    {
        InitializeComponent();
        var appWindow = AppWindow;
        appWindow.Title = "卸载校园网自动认证";
        appWindow.Resize(new SizeInt32(560, 330));
        appWindow.Closing += (_, _) => _completion.TrySetResult(null);
    }

    public Task<bool?> Completion => _completion.Task;

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        _completion.TrySetResult(DeleteUserDataBox.IsChecked == true);
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _completion.TrySetResult(null);
        Close();
    }
}
