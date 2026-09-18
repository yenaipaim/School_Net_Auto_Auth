using Microsoft.UI.Xaml.Controls;
using SchoolNetAutoAuth.App.ViewModels;

namespace SchoolNetAutoAuth.App.Views;

public sealed partial class NetworkSettingsPage : Page
{
    public NetworkSettingsPage()
    {
        InitializeComponent();
        DataContext = App.GetService<NetworkSettingsViewModel>();
    }
}
