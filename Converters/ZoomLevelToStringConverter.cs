using System.Globalization;
using System.Windows.Data;

namespace ComicReader.Converters;

/// <summary>
/// Converts double zoom factor (e.g., 1.0, 1.25) to human-friendly display percentage text (e.g., "100% (Default)", "125%").
/// </summary>
public sealed class ZoomLevelToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double zoom)
        {
            if (Math.Abs(zoom - 1.0) < 0.001)
            {
                return "100% (Default)";
            }

            return $"{zoom:P0}";
        }

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
