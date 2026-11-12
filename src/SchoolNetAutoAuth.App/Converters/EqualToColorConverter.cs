using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace SchoolNetAutoAuth.App.Converters;

/// <summary>
/// 字符串（忽略大小写）相等 → Visible，否则 Collapsed。
/// 用于强调色预设色块的选中环：绑定当前 AccentColor，参数是预设值。
/// </summary>
public sealed class EqualToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        string.Equals(value?.ToString()?.Trim(), parameter?.ToString()?.Trim(), StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
