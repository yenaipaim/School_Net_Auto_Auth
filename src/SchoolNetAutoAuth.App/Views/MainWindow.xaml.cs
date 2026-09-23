using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using SchoolNetAutoAuth.App.Services;
using SchoolNetAutoAuth.App.ViewModels;
using Windows.Graphics;

namespace SchoolNetAutoAuth.App.Views;

public sealed partial class MainWindow : Window
{
    private readonly AppController _controller;
    private SizeInt32? _sizeBeforeSchedule;
    private SchedulePage? _schedulePage;
    private bool _scheduleMode;

    public MainWindow(MainViewModel viewModel, WindowService windows, AppController controller)
    {
        InitializeComponent();
        _controller = controller;
        Root.DataContext = viewModel;
        windows.Attach(this);
        AppWindow.Title = "校园网自动认证";
        var iconPath = Path.Combine(AppContext.BaseDirectory, "App.ico");
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);
        AppWindow.Resize(new SizeInt32(980, 680));
        ApplyBackgroundImage(_controller.Settings.BackgroundImagePath, _controller.Settings.BackgroundImageOpacity);
        _controller.SettingsChanged += (_, settings) =>
            Root.DispatcherQueue.TryEnqueue(() =>
                ApplyBackgroundImage(settings.BackgroundImagePath, settings.BackgroundImageOpacity));
        Navigation.SelectedItem = StatusNavigationItem;
        if (ContentFrame.CurrentSourcePageType != typeof(StatusPage))
            ContentFrame.Navigate(typeof(StatusPage));
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag) return;
        var enteringSchedule = string.Equals(tag, "schedule", StringComparison.Ordinal);
        if (enteringSchedule && !_scheduleMode) EnterScheduleMode();
        else if (!enteringSchedule && _scheduleMode) LeaveScheduleMode();

        var page = tag switch
        {
            "schedule" => typeof(SchedulePage),
            "recording" => typeof(RecordingPage),
            "network" => typeof(NetworkSettingsPage),
            "advanced" => typeof(AdvancedSettingsPage),
            "general" => typeof(GeneralSettingsPage),
            _ => typeof(StatusPage)
        };
        if (ContentFrame.CurrentSourcePageType != page)
            ContentFrame.Navigate(page);
        if (enteringSchedule) AttachSchedulePage();
    }

    private void EnterScheduleMode()
    {
        _scheduleMode = true;
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
            _sizeBeforeSchedule = AppWindow.Size;
    }

    private void LeaveScheduleMode()
    {
        DetachSchedulePage();
        _scheduleMode = false;
        if (_sizeBeforeSchedule is { } size
            && AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Restored })
        {
            ResizeToWorkArea(size);
        }
        _sizeBeforeSchedule = null;
    }

    private void AttachSchedulePage()
    {
        DetachSchedulePage();
        _schedulePage = ContentFrame.Content as SchedulePage;
        if (_schedulePage is null) return;
        ResizeToWorkArea(_schedulePage.PreferredWindowSize);
    }

    private void DetachSchedulePage()
    {
        _schedulePage = null;
    }

    private void ResizeToWorkArea(SizeInt32 requestedSize)
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        if (displayArea is null) return;
        var workArea = displayArea.WorkArea;
        var minimumWidth = Math.Min(720, workArea.Width);
        var minimumHeight = Math.Min(560, workArea.Height);
        AppWindow.Resize(new SizeInt32(
            Math.Clamp(requestedSize.Width, minimumWidth, workArea.Width),
            Math.Clamp(requestedSize.Height, minimumHeight, workArea.Height)));
    }

    private void ApplyBackgroundImage(string? path, double opacity)
    {
        BackgroundImage.Source = null;
        BackgroundImage.Opacity = Math.Clamp(opacity, 0, 1);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try
        {
            BackgroundImage.Source = new BitmapImage(new Uri(path, UriKind.Absolute));
        }
        catch
        {
            BackgroundImage.Source = null;
        }
    }
}
