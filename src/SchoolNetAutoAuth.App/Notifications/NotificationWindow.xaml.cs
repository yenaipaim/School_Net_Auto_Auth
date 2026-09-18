using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace SchoolNetAutoAuth.App.Notifications;

public sealed partial class NotificationWindow : Window
{
    private readonly DispatcherTimer _timer;
    private readonly AppWindow _appWindow;

    public NotificationWindow(string title, string message, TimeSpan duration)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        _appWindow = AppWindow;
        _appWindow.IsShownInSwitchers = false;
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(false, false);
        }
        _timer = new DispatcherTimer { Interval = duration };
        _timer.Tick += (_, _) => Close();
        Closed += (_, _) => _timer.Stop();
    }

    public void ShowInactive()
    {
        var width = MessageText.Text.Length > 100 ? 440 : 380;
        var lineCount = Math.Max(1, (MessageText.Text.Length + 44) / 44);
        var height = Math.Clamp(128 + lineCount * 22, 150, 360);
        _appWindow.Resize(new SizeInt32(width, height));
        var area = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        var position = NotificationPlacement.Calculate(
            new DesktopRect(area.X, area.Y, area.Width, area.Height),
            new DesktopSize(width, height),
            16);
        _appWindow.Move(new PointInt32((int)position.X, (int)position.Y));
        _appWindow.Show(false);
        _timer.Start();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
