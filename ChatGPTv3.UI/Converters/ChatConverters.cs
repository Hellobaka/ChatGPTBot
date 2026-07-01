using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ChatGPTv3.UI.Converters;

public static class ChatConverters
{
    public static readonly IValueConverter Alignment = new AlignmentConverter();
    public static readonly IValueConverter BubbleColor = new BubbleColorConverter();
    public static readonly IValueConverter TextColor = new TextColorConverter();
    public static readonly IValueConverter BoolToVisibility =
        new System.Windows.Controls.BooleanToVisibilityConverter();
}

public class AlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BubbleColorConverter : IValueConverter
{
    private static readonly SolidColorBrush SelfBrush = new(Color.FromRgb(137, 180, 250));
    private static readonly SolidColorBrush BotBrush = new(Color.FromRgb(49, 50, 68));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? SelfBrush : BotBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Expanded=true → 0px (icon left-aligned), Expanded=false → 1* (icon centered).</summary>
public class SidebarColumnConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}

/// <summary>Converts numeric types (int, float, ushort, double) to double for NumericUpDown.</summary>
public class NumericToDoubleConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double d ? d : System.Convert.ToDouble(value);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value; // double stored as-is; ConfigEntry.Save handles double case
}

/// <summary>true → Visible, false/else → Collapsed (regular BoolToVisibility).</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

/// <summary>true → Collapsed, false/else → Visible (inverted).</summary>
public class InvertBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Collapsed;
}

public class TextColorConverter : IValueConverter
{
    private static readonly SolidColorBrush SelfBrush = new(Color.FromRgb(17, 17, 27));
    private static readonly SolidColorBrush BotBrush = new(Color.FromRgb(205, 214, 244));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? SelfBrush : BotBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
