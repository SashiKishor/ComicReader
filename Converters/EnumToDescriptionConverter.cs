using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace ComicReader.Converters;

/// <summary>
/// Converts an enum value to its [Description] attribute string representation for UI controls.
/// Falls back to enum string representation if no description attribute is found.
/// </summary>
public sealed class EnumToDescriptionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var enumType = value.GetType();
        var memberName = value.ToString();
        if (string.IsNullOrEmpty(memberName))
        {
            return string.Empty;
        }

        var fieldInfo = enumType.GetField(memberName);
        if (fieldInfo != null)
        {
            var attribute = fieldInfo.GetCustomAttribute<DescriptionAttribute>();
            if (attribute != null)
            {
                return attribute.Description;
            }
        }

        return memberName;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
