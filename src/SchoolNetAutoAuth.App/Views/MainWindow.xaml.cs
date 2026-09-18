using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SchoolNetAutoAuth.App.Services;
using SchoolNetAutoAuth.App.ViewModels;
using Windows.Graphics;

namespace SchoolNetAutoAuth.App.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel, WindowService windows)
    {
        InitializeComponent();
        Root.DataContext = viewModel;
        windows.Attach(this);
        AppWindow.Title = "校园网自动认证";
        var iconPath = Path.Combine(AppContext.BaseDirectory, "App.ico");
        if (File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);
        AppWindow.Resize(new SizeInt32(980, 680));
        Navigation.SelectedItem = Navigation.MenuItems[0];
        ContentFrame.Navigate(typeof(StatusPage));
    }

    private void Navigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag) return;
        var page = tag switch
        {
            "recording" => typeof(RecordingPage),
            "network" => typeof(NetworkSettingsPage),
            "advanced" => typeof(AdvancedSettingsPage),
            _ => typeof(StatusPage)
        };
        if (ContentFrame.CurrentSourcePageType != page)
            ContentFrame.Navigate(page);
    }
}
