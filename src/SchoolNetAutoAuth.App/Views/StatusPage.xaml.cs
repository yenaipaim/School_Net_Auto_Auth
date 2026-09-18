using Microsoft.UI.Xaml.Controls;
using SchoolNetAutoAuth.App.ViewModels;

namespace SchoolNetAutoAuth.App.Views;

public sealed partial class StatusPage : Page
{
    public StatusPage()
    {
        InitializeComponent();
        DataContext = App.GetService<StatusViewModel>();
    }
}
