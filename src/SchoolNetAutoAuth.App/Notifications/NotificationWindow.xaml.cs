using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using SchoolNetAutoAuth.App.Services;
using Windows.Foundation;
using Windows.Graphics;

namespace SchoolNetAutoAuth.App.Notifications;

public sealed partial class NotificationWindow : Window
{
    /// <summary>卡片宽度固定，避免长文本把窗口撑宽。</summary>
    private const double CardWidthDip = 360;

    /// <summary>高度由内容决定，但限制上下限，避免过短或铺满屏幕。</summary>
    private const double MinHeightDip = 96;
    private const double MaxHeightDip = 300;

    private const int ScreenMarginDip = 16;

    private readonly DispatcherTimer _timer;
    private readonly AppWindow _appWindow;
    private double _scale = 1.0;
    private bool _fitApplied;

    public NotificationWindow(string title, string message, TimeSpan duration, bool success)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        AccentBar.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(NotificationAccent(success));

        // 独立窗口不会自动继承主窗口的主题，这里显式对齐一次。
        Root.RequestedTheme = ThemeService.IsLight ? ElementTheme.Light : ElementTheme.Dark;
        Root.FontFamily = ThemeService.ResolveFontFamily(null);

        _appWindow = AppWindow;
        _appWindow.IsShownInSwitchers = false;
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(false, false);
        }

        _timer = new DispatcherTimer { Interval = duration };
        _timer.Tick += (_, _) => Close();
        Closed += (_, _) => _timer.Stop();
    }

    private static Windows.UI.Color NotificationAccent(bool success) =>
        Resolve(success ? "AppSuccessBrush" : "AppWarningBrush");

    private static Windows.UI.Color Resolve(string key) =>
        Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out object? value)
        && value is Microsoft.UI.Xaml.Media.SolidColorBrush brush
            ? brush.Color
            : Microsoft.UI.Colors.Gray;

    public void ShowInactive()
    {
        // 先按最小高度显示，等 XAML 完成布局后再按真实内容高度调整；
        // 未加载时 Measure 拿不到有效 DesiredSize。
        _scale = ReadScale();
        ResizeAndAnchor(MinHeightDip);
        _appWindow.Show(false);
        Root.Loaded += (_, _) => DispatcherQueue.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, FitToContent);
        _timer.Start();
    }

    /// <summary>按内容真实高度重排一次，超出上限时由内部滚动承担。</summary>
    private void FitToContent()
    {
        if (_fitApplied) return;
        _fitApplied = true;

        var heightDip = MeasureContentHeight();
        ResizeAndAnchor(heightDip);
    }

    private double MeasureContentHeight()
    {
        Root.Measure(new Size(CardWidthDip, double.PositiveInfinity));
        var desired = Root.DesiredSize.Height;
        if (double.IsNaN(desired) || desired <= 0)
        {
            // 退回到按行数估算，中文字宽按每行 18 个字计。
            var perLine = 18;
            var lines = Math.Max(1, (MessageText.Text.Length + perLine - 1) / perLine);
            desired = 34 + lines * 18;
        }

        return Math.Clamp(desired, MinHeightDip, MaxHeightDip);
    }

    private void ResizeAndAnchor(double heightDip)
    {
        var width = (int)Math.Round(CardWidthDip * _scale);
        var height = (int)Math.Round(heightDip * _scale);
        _appWindow.Resize(new SizeInt32(width, height));

        var area = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest)?.WorkArea;
        if (area is null) return;

        var workArea = area.Value;
        var margin = (int)Math.Round(ScreenMarginDip * _scale);
        var frame = ReadVisibleFrame(width, height);
        var frameWidth = frame.Right - frame.Left;
        var frameHeight = frame.Bottom - frame.Top;
        var position = _appWindow.Position;
        // 右下角对齐，并减去窗口不可见边框的偏移，避免贴边被裁。
        _appWindow.Move(new PointInt32(
            workArea.X + workArea.Width - frameWidth - margin - (frame.Left - position.X),
            workArea.Y + workArea.Height - frameHeight - margin - (frame.Top - position.Y)));
    }

    /// <summary>
    /// 窗口 Show 之前 XamlRoot 还是 null，拿不到 RasterizationScale，
    /// 只能按 HWND 查当前显示器的 DPI；否则会把 DIP 当像素用，整体缩水。
    /// </summary>
    private double ReadScale()
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var dpi = NativeMethods.GetDpiForWindow(hwnd);
            if (dpi > 0) return dpi / 96.0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
        {
            // 退回 1.0。
        }

        var fallback = Root.XamlRoot?.RasterizationScale ?? 0;
        return fallback > 0 ? fallback : 1.0;
    }

    /// <summary>DWM 可见边框；窗口外框含不可见缩进，直接用它对齐会贴边偏移。</summary>
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

        var position = _appWindow.Position;
        return new NativeMethods.Rect
        {
            Left = position.X,
            Top = position.Y,
            Right = position.X + widthPixels,
            Bottom = position.Y + heightPixels
        };
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static class NativeMethods
    {
        internal const int DwmwaExtendedFrameBounds = 9;

        [DllImport("user32.dll")]
        internal static extern uint GetDpiForWindow(IntPtr hwnd);

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
}