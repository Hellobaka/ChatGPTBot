using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;

namespace ChatGPTv3.UI.Converters;

public static class ChatConverters
{
    public static readonly IValueConverter Alignment =
        new FuncValueConverter<bool, HorizontalAlignment>(isSelf =>
            isSelf ? HorizontalAlignment.Right : HorizontalAlignment.Left);

    public static readonly IValueConverter BubbleColor =
        new FuncValueConverter<bool, IBrush>(isSelf =>
            isSelf
                ? new SolidColorBrush(Color.FromRgb(137, 180, 250))
                : new SolidColorBrush(Color.FromRgb(49, 50, 68)));

    public static readonly IValueConverter TextColor =
        new FuncValueConverter<bool, IBrush>(isSelf =>
            isSelf
                ? new SolidColorBrush(Color.FromRgb(17, 17, 27))
                : new SolidColorBrush(Color.FromRgb(205, 214, 244)));
}
