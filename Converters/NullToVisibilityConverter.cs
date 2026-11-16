using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ComicReader.Converters;

/// <summary>
/// Converts null or empty strings/objects to Collapsed, non-null to Visible (or inverted if parameter is "Inverse").
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNull = value switch
        {
            null => true,
            string s => string.IsNullOrWhiteSpace(s),
            _ => false
        };

        bool invert = parameter is string paramStr && string.Equals(paramStr, "Inverse", StringComparison.OrdinalIgnoreCase);

        if (invert)
        {
            return isNull ? Visibility.Visible : Visibility.Collapsed;
        }

        return isNull ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

