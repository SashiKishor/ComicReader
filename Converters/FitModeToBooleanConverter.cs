using ComicReader.Models;
using System.Globalization;
using System.Windows.Data;

namespace ComicReader.Converters;

/// <summary>
/// Converts PageFitMode enum to boolean for RadioButton / ToggleButton bindings.
/// </summary>
public sealed class FitModeToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PageFitMode currentMode && parameter is string targetModeStr)
        {
            if (Enum.TryParse<PageFitMode>(targetModeStr, true, out var targetMode))
            {
                return currentMode == targetMode;
            }
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is string targetModeStr)
        {
            if (Enum.TryParse<PageFitMode>(targetModeStr, true, out var targetMode))
            {
                return targetMode;
            }
        }

        return Binding.DoNothing;
    }
}

