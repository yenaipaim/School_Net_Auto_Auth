using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SchoolNetAutoAuth.App.Services;

public sealed class FilePickerService
{
    public async Task<StorageFile?> PickOpenFileAsync(
        string description = "配置文件",
        params string[] extensions)
    {
        var picker = new FileOpenPicker();
        foreach (var extension in NormalizeExtensions(extensions))
            picker.FileTypeFilter.Add(extension);
        Initialize(picker);
        return await picker.PickSingleFileAsync();
    }

    public async Task<StorageFile?> PickSaveFileAsync(
        string suggestedName,
        string description = "配置文件",
        params string[] extensions)
    {
        var picker = new FileSavePicker { SuggestedFileName = suggestedName, SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeChoices.Add(description, NormalizeExtensions(extensions).ToArray());
        Initialize(picker);
        return await picker.PickSaveFileAsync();
    }

    private static IEnumerable<string> NormalizeExtensions(IReadOnlyCollection<string> extensions) =>
        extensions.Count == 0 ? [".json"] : extensions;

    private void Initialize(object picker)
    {
        var hwnd = WindowNative.GetWindowHandle((Application.Current as App)?.MainWindow
            ?? throw new InvalidOperationException("主窗口尚未初始化。"));
        InitializeWithWindow.Initialize(picker, hwnd);
    }
}
