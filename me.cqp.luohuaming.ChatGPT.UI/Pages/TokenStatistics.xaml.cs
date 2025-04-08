using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace me.cqp.luohuaming.ChatGPT.UI.Pages
{
    /// <summary>
    /// UserControl1.xaml 的交互逻辑
    /// </summary>
    public partial class TokenStatistics : Page
    {
        public TokenStatistics()
        {
            InitializeComponent();
            DataContext = this;
        }

        public IEnumerable<ISeries> Pie_ModelCollection { get; set; } =
        [
            new PieSeries<int>
            {
                Values = [80],
                Name = "deepseek-chat",
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsSize = 14,
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                DataLabelsFormatter = point => "deepseek-chat"
            },
            new PieSeries<int>
            {
                Values = [20],
                Name = "gpt-4o-mini" ,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsSize = 14,
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                DataLabelsFormatter = point => "gpt-4o-mini"
            },
            new PieSeries<int>
            {
                Values = [47],
                Name = "gpt-4o" ,
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsSize = 14,
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                DataLabelsFormatter = point => "gpt-4o"
            },
            new PieSeries<int>
            {
                Values = [117],
                Name = "deepseek-v3-0214",
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsSize = 14,
                DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                DataLabelsFormatter = point => "deepseek-v3-0214"
            },
        ];

        private void UpdateGridLayout()
        {
            Column1.Width = Visibility.Visible == Visibility.Collapsed ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            Column2.Width = Visibility.Visible == Visibility.Collapsed ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            Column3.Width = Visibility.Visible == Visibility.Collapsed ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateGridLayout();
        }
    }
}
