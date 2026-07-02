using ChatGPTv3.UI.ViewModels;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Themes;
using System.Windows.Controls;

namespace ChatGPTv3.UI.Views;

public partial class TokenUsageView : UserControl
{
    public TokenUsageView()
    {
        InitializeComponent();
        DataContext = new TokenUsageViewModel();
        LiveCharts.Configure(config =>
        {
            config.AddDefaultTheme(requestedTheme: LvcThemeKind.Dark);
        });
    }
}