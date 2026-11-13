using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace SchoolNetAutoAuth.App.Services;

public sealed class WindowService
{
    private Window? _window;

    public bool IsExiting { get; set; }

    /// <summary>请求把主窗口导航到指定页面标签（status / network / recording ...）。</summary>
    public event Action<string>? NavigationRequested;

    public void Navigate(string tag) => NavigationRequested?.Invoke(tag);
    public XamlRoot? XamlRoot => (_window?.Content as FrameworkElement)?.XamlRoot;

    public void Attach(Window window)
    {
        _window = window;
        AppWindow.Closing += AppWindow_Closing;
    }

    public AppWindow AppWindow
    {
        get
        {
            if (_window is null) throw new InvalidOperationException("主窗口尚未初始化。");
            return _window.AppWindow;
        }
    }

    public void Show()
    {
        _window?.Activate();
        AppWindow.Show();
    }

    public void Hide() => AppWindow.Hide();

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (IsExiting) return;
        args.Cancel = true;
        sender.Hide();
    }
}
