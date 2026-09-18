using Microsoft.UI.Xaml.Controls;
using SchoolNetAutoAuth.App.ViewModels;

namespace SchoolNetAutoAuth.App.Views;

public sealed partial class RecordingPage : Page
{
    public RecordingPage()
    {
        InitializeComponent();
        DataContext = App.GetService<RecordingViewModel>();
    }
}
