using ChatGPTv3.Core.DB;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ChatGPTv3.UI.Converters;

public static class ChatConverters
{
    public static readonly IValueConverter CapabilityToText = new CapabilityToTextConverter();

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

public class CapabilityToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ModelCapability caps)
        {
            return "(无)";
        }

        var parts = new List<string>();
        if ((caps & ModelCapability.Chat) != 0)
        {
            parts.Add("Chat");
        }
        if ((caps & ModelCapability.Image) != 0)
        {
            parts.Add("Image");
        }
        if ((caps & ModelCapability.Embedding) != 0)
        {
            parts.Add("Emb");
        }
        if ((caps & ModelCapability.Rerank) != 0)
        {
            parts.Add("Rerank");
        }

        return parts.Count switch
        {
            0 => "(无)",
            1 => parts[0],
            _ => $"{parts.Count} 项功能"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Converts 0-based page index to 1-based for HandyControl Pagination and back.</summary>
public class PageIndexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i ? i + 1 : 1;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i ? Math.Max(0, i - 1) : 0;
}