using Microsoft.UI.Xaml.Controls;
using SchoolNetAutoAuth.App.ViewModels;

namespace SchoolNetAutoAuth.App.Views;

public sealed partial class GeneralSettingsPage : Page
{
    public GeneralSettingsPage()
    {
        InitializeComponent();
        DataContext = App.GetService<GeneralSettingsViewModel>();
    }
}
