using Microsoft.UI.Xaml.Controls;
using SchoolNetAutoAuth.App.ViewModels;

namespace SchoolNetAutoAuth.App.Views;

public sealed partial class AdvancedSettingsPage : Page
{
    public AdvancedSettingsPage()
    {
        InitializeComponent();
        DataContext = App.GetService<AdvancedSettingsViewModel>();
    }
}
