using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ComicReader.Converters;

/// <summary>
/// Converts a boolean to the inverse Visibility (true -> Collapsed, false -> Visible).
/// </summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolVal)
        {
            return boolVal ? Visibility.Collapsed : Visibility.Visible;
        }

        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility vis)
        {
            return vis != Visibility.Visible;
        }

        return false;
    }
}

