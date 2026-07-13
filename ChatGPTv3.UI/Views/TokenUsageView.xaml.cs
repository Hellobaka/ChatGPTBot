using ChatGPTv3.UI.ViewModels;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ChatGPTv3.UI.Views;

public partial class TokenUsageView : UserControl
{
    public TokenUsageView()
    {
        InitializeComponent();
        DataContext = new TokenUsageViewModel();
        IsVisibleChanged += (s, e) =>
        {
            if (DataContext is TokenUsageViewModel vm)
            {
                vm.NotifyVisibilityChanged(IsVisible);
            }
        };
        LiveCharts.Configure(config =>
        {
            config.AddDefaultTheme(requestedTheme: LvcThemeKind.Dark);
        });
    }

    private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
        eventArg.RoutedEvent = UIElement.MouseWheelEvent;
        eventArg.Source = sender;

        var parent = ((Control)sender).Parent as UIElement;

        // �ڸ����������µ� MouseWheel �¼�
        if (parent != null)
        {
            parent.RaiseEvent(eventArg);
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
            {
                return result;
            }

            var descendant = FindVisualChild<T>(child);
            if (descendant != null)
            {
                return descendant;
            }
        }

        return null;
    }
}