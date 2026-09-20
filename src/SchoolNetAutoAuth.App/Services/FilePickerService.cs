using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SchoolNetAutoAuth.App.Services;

public sealed class FilePickerService(WindowService windows)
{
    public async Task<StorageFile?> PickOpenFileAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");
        Initialize(picker);
        return await picker.PickSingleFileAsync();
    }

    public async Task<StorageFile?> PickSaveFileAsync(string suggestedName)
    {
        var picker = new FileSavePicker { SuggestedFileName = suggestedName, SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeChoices.Add("录制配置", new[] { ".json" });
        Initialize(picker);
        return await picker.PickSaveFileAsync();
    }

    private void Initialize(object picker)
    {
        var hwnd = WindowNative.GetWindowHandle((Application.Current as App)?.MainWindow
            ?? throw new InvalidOperationException("主窗口尚未初始化。"));
        InitializeWithWindow.Initialize(picker, hwnd);
    }
}
