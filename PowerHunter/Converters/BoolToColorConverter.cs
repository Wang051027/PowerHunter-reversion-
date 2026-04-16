using System.Globalization;

namespace PowerHunter.Converters;

/// <summary>
/// Converts a boolean to one of two configurable colors.
/// Supports dark mode via optional DarkTrueColor/DarkFalseColor properties.
/// </summary>
public sealed class BoolToColorConverter : IValueConverter
{
    public Color TrueColor { get; set; } = Colors.White;
    public Color FalseColor { get; set; } = Colors.Transparent;
    public Color? DarkTrueColor { get; set; }
    public Color? DarkFalseColor { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var trueResult = isDark && DarkTrueColor is not null ? DarkTrueColor : TrueColor;
        var falseResult = isDark && DarkFalseColor is not null ? DarkFalseColor : FalseColor;
        return value is true ? trueResult : falseResult;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
