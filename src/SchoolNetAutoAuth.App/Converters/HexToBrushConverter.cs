using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace SchoolNetAutoAuth.App.Converters;

/// <summary>
/// #RRGGBB / #RGB 字符串 → 画刷。用于强调色预览方块，
/// 让 XAML 不必为每个预设色再声明一份资源。
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string text) return new SolidColorBrush(Colors.Gray);
        var trimmed = text.Trim();
        if (trimmed.Length == 4 && trimmed[0] == '#')
        {
            trimmed = $"#{trimmed[1]}{trimmed[1]}{trimmed[2]}{trimmed[2]}{trimmed[3]}{trimmed[3]}";
        }

        if (trimmed.Length != 7 || trimmed[0] != '#') return new SolidColorBrush(Colors.Gray);
        try
        {
            return new SolidColorBrush(Color.FromArgb(
                0xFF,
                System.Convert.ToByte(trimmed.Substring(1, 2), 16),
                System.Convert.ToByte(trimmed.Substring(3, 2), 16),
                System.Convert.ToByte(trimmed.Substring(5, 2), 16)));
        }
        catch (FormatException)
        {
            return new SolidColorBrush(Colors.Gray);
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
