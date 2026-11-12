using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace SchoolNetAutoAuth.App.Uninstallation;

public sealed partial class UninstallWindow : Window
{
    /// <summary>目标尺寸用 DIP 表达，实际摆放时按显示器缩放换算成物理像素。</summary>
    private const double TargetWidthDip = 620;
    private const double TargetHeightDip = 470;

    private readonly TaskCompletionSource<bool?> _completion = new();
    private bool _layoutApplied;

    public UninstallWindow()
    {
        InitializeComponent();
        // 卸载流程不会创建主窗口，这里自己对齐一次主题，避免深色令牌配浅色原生控件。
        Root.RequestedTheme = SchoolNetAutoAuth.App.Services.ThemeService.IsLight
            ? Microsoft.UI.Xaml.ElementTheme.Light
            : Microsoft.UI.Xaml.ElementTheme.Dark;
        Root.FontFamily = SchoolNetAutoAuth.App.Services.ThemeService.ResolveFontFamily(null);
        var appWindow = AppWindow;
        appWindow.Title = "卸载校园网自动认证";
        var iconPath = Path.Combine(AppContext.BaseDirectory, "App.ico");
        if (File.Exists(iconPath))
            appWindow.SetIcon(iconPath);
        appWindow.Closing += (_, _) => _completion.TrySetResult(null);

        // 必须等 XAML 加载完（此时才有 XamlRoot 与有效 DPI），并且延后一拍：
        // 在 Activated 里直接 Move 会被 WinUI 随后的默认摆放覆盖。
        Root.Loaded += (_, _) => DispatcherQueue.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, ApplyInitialLayout);
    }

    /// <summary>
    /// 居中并给出足够的内容尺寸。
    /// AppWindow 的尺寸单位是物理像素而 XAML 用 DIP，直接 Resize(600, 420)
    /// 在 125% 缩放下只有 480x336 DIP，会把按钮裁掉。
    /// </summary>
    private void ApplyInitialLayout()
    {
        if (_layoutApplied) return;
        _layoutApplied = true;

        try
        {
            var scale = Root.XamlRoot?.RasterizationScale ?? 0;
            if (scale <= 0) scale = 1.0;

            var width = (int)Math.Round(TargetWidthDip * scale);
            var height = (int)Math.Round(TargetHeightDip * scale);

            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary)?.WorkArea;
            if (area is null)
            {
                AppWindow.Resize(new SizeInt32(width, height));
                return;
            }

            var workArea = area.Value;
            width = Math.Min(width, workArea.Width);
            height = Math.Min(height, workArea.Height);
            AppWindow.Resize(new SizeInt32(width, height));

            // AppWindow 的坐标是窗口外框，可见区域会向内缩进，而且缩进通常不对称
            // （上边框为 0、下边框有几像素）。按"可见边框"居中，视觉上才真的在屏幕中间。
            var frame = ReadVisibleFrame(width, height);
            var frameWidth = frame.Right - frame.Left;
            var frameHeight = frame.Bottom - frame.Top;
            var position = AppWindow.Position;
            var desiredLeft = workArea.X + (workArea.Width - frameWidth) / 2;
            var desiredTop = workArea.Y + (workArea.Height - frameHeight) / 2;
            AppWindow.Move(new PointInt32(
                desiredLeft - (frame.Left - position.X),
                desiredTop - (frame.Top - position.Y)));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            // 摆放失败不影响卸载流程，窗口保持系统默认位置。
        }
    }

    /// <summary>读取 DWM 报告的可见边框；取不到时按传入尺寸假设无缩进。</summary>
    private NativeMethods.Rect ReadVisibleFrame(int widthPixels, int heightPixels)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (NativeMethods.DwmGetWindowAttribute(hwnd, NativeMethods.DwmwaExtendedFrameBounds,
                    out var frame, Marshal.SizeOf<NativeMethods.Rect>()) == 0)
                return frame;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
        {
            // 退回到无缩进假设。
        }

        var position = AppWindow.Position;
        return new NativeMethods.Rect
        {
            Left = position.X,
            Top = position.Y,
            Right = position.X + widthPixels,
            Bottom = position.Y + heightPixels
        };
    }

    private static class NativeMethods
    {
        internal const int DwmwaExtendedFrameBounds = 9;

        [DllImport("dwmapi.dll")]
        internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out Rect value, int size);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }

    public Task<bool?> Completion => _completion.Task;

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        _completion.TrySetResult(DeleteUserDataBox.IsChecked == true);
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _completion.TrySetResult(null);
        Close();
    }
}