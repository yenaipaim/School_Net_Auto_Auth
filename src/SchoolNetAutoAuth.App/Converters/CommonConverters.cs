using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace SchoolNetAutoAuth.App.Converters;

/// <summary>true → Visible，false → Collapsed。</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility.Visible;
}

/// <summary>bool 取反，用于「正在忙时禁用」这类反向开关。</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => value is not true;
}
