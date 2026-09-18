using CommunityToolkit.Mvvm.Input;
using SchoolNetAutoAuth.App.Services;

namespace SchoolNetAutoAuth.App.ViewModels;

public partial class MainViewModel(DialogService dialogs) : ViewModelBase
{
    public string Title => "校园网自动认证";

    [RelayCommand]
    private Task ShowAboutAsync() => dialogs.ShowAboutAsync();
}
