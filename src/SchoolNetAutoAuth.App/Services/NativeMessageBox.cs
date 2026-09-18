using System.Runtime.InteropServices;

namespace SchoolNetAutoAuth.App.Services;

public static class NativeMessageBox
{
    public static void ShowInfo(string message) => Show(message, 0x40);
    public static void ShowError(string message) => Show(message, 0x10);

    private static void Show(string message, uint icon) =>
        MessageBox(IntPtr.Zero, message, "校园网自动认证", icon);

    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr window, string text, string caption, uint type);
}
