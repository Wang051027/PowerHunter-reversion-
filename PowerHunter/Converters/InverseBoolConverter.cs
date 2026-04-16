using System.Globalization;

namespace PowerHunter.Converters;

/// <summary>
/// Inverts a boolean value. When ConverterParameter is provided:
/// - "bg": returns card-bg/Transparent based on inverted bool (theme-aware)
/// - "text": returns primary/secondary text color based on inverted bool (theme-aware)
/// - "rotation": returns 180/0 based on inverted bool (for chevron animation)
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolValue = value is true;
        var inverted = !boolValue;
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

        var param = parameter as string;
        return param switch
        {
            "bg" => inverted
                ? (isDark ? Color.FromArgb("#1F2937") : (Application.Current?.Resources["White"] as Color ?? Colors.White))
                : Colors.Transparent,
            "text" => inverted
                ? (isDark ? (Application.Current?.Resources["Gray50"] as Color ?? Colors.White)
                          : (Application.Current?.Resources["Gray900"] as Color ?? Colors.Black))
                : (isDark ? (Application.Current?.Resources["Gray400"] as Color ?? Colors.Gray)
                          : (Application.Current?.Resources["Gray500"] as Color ?? Colors.Gray)),
            "rotation" => boolValue ? 180.0 : 0.0,
            _ => inverted,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        throw new NotSupportedException();
    }
}
