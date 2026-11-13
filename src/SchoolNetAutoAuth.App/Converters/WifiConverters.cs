using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace SchoolNetAutoAuth.App.Converters;

/// <summary>把 0–100 的信号百分比换算成 4 格信号柱；ConverterParameter 是格子序号（0 起）。</summary>
public sealed class SignalBarsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var quality = value switch
        {
            uint u => u,
            int i => (uint)Math.Max(0, i),
            double d => (uint)Math.Max(0, d),
            _ => 0u
        };
        var index = int.TryParse(parameter?.ToString(), out var parsed) ? parsed : 0;
        var bars = quality switch
        {
            >= 75 => 4,
            >= 50 => 3,
            >= 25 => 2,
            > 0 => 1,
            _ => 0
        };
        return index < bars ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>加密方式只有「是否加密」这一个事实，不臆造 WPA2 / WPA3。</summary>
public sealed class WifiSecurityLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? "加密" : "开放";

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>开放网络用警告色，加密网络用次要文本色。</summary>
public sealed class WifiSecurityBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        Resource(value is true ? "AppMutedFgBrush" : "AppWarningBrush");

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();

    private static Brush Resource(string key) =>
        Application.Current.Resources.TryGetValue(key, out object? value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Microsoft.UI.Colors.Gray);
}
