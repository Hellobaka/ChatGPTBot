using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using me.cqp.luohuaming.ChatGPT.UI.Model;
using Microsoft.Win32;
using ModernWpf;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace me.cqp.luohuaming.ChatGPT.UI.Pages
{
    /// <summary>
    /// UserControl1.xaml 的交互逻辑
    /// </summary>
    public partial class TokenStatistics : Page, INotifyPropertyChanged
    {
        public TokenStatistics()
        {
            InitializeComponent();
            DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public bool Bar_ModelChecked { get; set; }

        public bool Bar_OverviewChecked { get; set; }

        public bool Bar_PurposeChecked { get; set; }

        public bool Bar_ServiceChecked { get; set; }

        public IEnumerable<ISeries> BarCollection { get; set; } = [];

        public int CallCount { get; set; }

        public int CheckedModelCount => Models.Count(item => item.Checked);

        public int CheckedPurposeCount => Purposes.Count(item => item.Checked);

        public int CheckedServiceCount => Services.Count(item => item.Checked);

        public DateTime FilterEndDate { get; set; }

        public List<Usage> FilterResult { get; set; } = [];

        public DateTime FilterStartDate { get; set; }

        public int InputTokenCount { get; set; }

        public int MaxRPM { get; set; }

        public ObservableCollection<CheckableItem> Models { get; set; } = [];

        public int OutputTokenCount { get; set; }

        public bool PageLoaded { get; set; }

        public bool Pie_ModelChecked { get; set; }

        public IEnumerable<ISeries> Pie_ModelCollection { get; set; } = [];

        public bool Pie_PurposeChecked { get; set; }

        public IEnumerable<ISeries> Pie_PurposeCollection { get; set; } = [];

        public bool Pie_ServiceChecked { get; set; }

        public IEnumerable<ISeries> Pie_ServiceCollection { get; set; } = [];

        public ObservableCollection<CheckableItem> Purposes { get; set; } = [];

        public ObservableCollection<CheckableItem> Services { get; set; } = [];

        public int TotalTokenCount { get; set; }

        public bool UnitCountChecked { get; set; }

        public bool UnitTokenChecked { get; set; }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void AnimateTextChange(TextBlock textBlock, int newValue)
        {
            var binding = textBlock.GetBindingExpression(TextBlock.TextProperty);
            var propertyName = binding?.ParentBinding?.Path?.Path;
            var property = GetType().GetProperty(propertyName);
            var fadeOutAnimation = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.2)));
            fadeOutAnimation.Completed += (s, a) =>
            {
                property.SetValue(this, newValue);
                OnPropertyChanged(propertyName);

                var fadeInAnimation = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.2)));
                textBlock.BeginAnimation(TextBlock.OpacityProperty, fadeInAnimation);
            };

            textBlock.BeginAnimation(TextBlock.OpacityProperty, fadeOutAnimation);
        }

        private void BarChartVisible_Checked(object sender, RoutedEventArgs e)
        {
            BarChart.Visibility = Visibility.Collapsed;
        }

        private void BarChartVisible_Unchecked(object sender, RoutedEventArgs e)
        {
            BarChart.Visibility = Visibility.Visible;
        }

        private void ChangeBarChartColor()
        {
            SKColor color = ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Dark ? SKColors.White : SKColors.Black;
            TimeDetailChart.LegendTextPaint = new SolidColorPaint { Color = color, SKTypeface = SKTypeface.FromFamilyName("微软雅黑") };
            foreach (var item in TimeDetailChart.XAxes)
            {
                item.LabelsPaint = new SolidColorPaint { Color = color, SKTypeface = SKTypeface.FromFamilyName("微软雅黑") };
            }
            foreach (var item in TimeDetailChart.YAxes)
            {
                item.LabelsPaint = new SolidColorPaint { Color = color, SKTypeface = SKTypeface.FromFamilyName("微软雅黑") };
            }
        }

        private void ChangeFilterDay_Clicked(object sender, RoutedEventArgs e)
        {
            var filterDay = (sender as Button).Tag.ToString();
            if (int.TryParse(filterDay, out int day))
            {
                FilterStartDate = DateTime.Now.AddDays(-1 * day).Date;
            }
            else
            {
                MainWindow.ShowError("所选筛选时间无效");
                return;
            }
            if (day == 1)
            {
                // 昨天筛选只能到昨天
                FilterEndDate = FilterStartDate;
            }
            else
            {
                FilterEndDate = DateTime.Now.Date;
            }
            TriggerFilter();
            DoFilter();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox checkbox = sender as CheckBox;
            var binding = checkbox.GetBindingExpression(CheckBox.IsCheckedProperty);
            var propertyName = binding?.ParentBinding?.Path?.Path;
            OnPropertyChanged(propertyName);
            UpdateGridLayout();
            ConfigHelper.SetConfig(propertyName, (bool)GetType().GetProperty(propertyName).GetValue(this));
        }

        private void DoFilter()
        {
            var searchResult = Usage.GetRangeUsageDetail(FilterStartDate, FilterEndDate);
            FilterResult = searchResult.Where(x => Services.Any(o => o.Checked && o.Name == x.Endpoint)
                && (Models.Count <= 0 || Models.Any(o => o.Checked && o.Name == x.ModelName))
                && (Purposes.Count <= 0 || Purposes.Any(o => o.Checked && o.Name == x.Purpose))).ToList();
            DrawPieChart();
            DrawBarChart();
            UpdateTokenCount();
        }

        private void DrawBarChart()
        {
            BarCollection = [];
            bool dayMode = FilterEndDate.Date == FilterStartDate.Date;
            List<DateTimePoint> overview = [];
            Dictionary<string, List<DateTimePoint>> model = [];
            Dictionary<string, List<DateTimePoint>> purpose = [];
            Dictionary<string, List<DateTimePoint>> service = [];
            string format = dayMode ? "yyyy-MM-dd HH:00" : "yyyy-MM-dd";
            foreach (var item in FilterResult.GroupBy(x => x.Time.ToString(format))
                .Select(x => new { Name = x.Key, Group = x, Count = x.Count(), TotalToken = x.Sum(x => x.InputToken + x.OutputToken) }))
            {
                DateTime pointDate = DateTime.TryParseExact(item.Name, format, null, System.Globalization.DateTimeStyles.None, out DateTime d) ? d : new();
                overview.Add(new() { DateTime = pointDate, Value = UnitCountChecked ? item.Count : item.TotalToken });

                item.Group.GroupBy(x => x.ModelName).ToList().ForEach(x =>
                {
                    if (!model.ContainsKey(x.Key))
                    {
                        model.Add(x.Key, []);
                    }
                    model[x.Key].Add(new() { DateTime = pointDate, Value = UnitCountChecked ? x.Count() : x.Sum(x => x.InputToken + x.OutputToken) });
                });

                item.Group.GroupBy(x => x.Purpose).ToList().ForEach(x =>
                {
                    if (!purpose.ContainsKey(x.Key))
                    {
                        purpose.Add(x.Key, []);
                    }
                    purpose[x.Key].Add(new() { DateTime = pointDate, Value = UnitCountChecked ? x.Count() : x.Sum(x => x.InputToken + x.OutputToken) });
                });

                item.Group.GroupBy(x => x.Endpoint).ToList().ForEach(x =>
                {
                    if (!service.ContainsKey(x.Key))
                    {
                        service.Add(x.Key, []);
                    }
                    service[x.Key].Add(new() { DateTime = pointDate, Value = UnitCountChecked ? x.Count() : x.Sum(x => x.InputToken + x.OutputToken) });
                });
            }

            if (dayMode)
            {
                TimeDetailChart.XAxes = [new DateTimeAxis(TimeSpan.FromHours(1), date => date.ToString("HH:mm"))];
            }
            else
            {
                TimeDetailChart.XAxes = [new DateTimeAxis(TimeSpan.FromDays(1), date => date.ToString("yyyy-MM-dd"))];
            }
            BarCollection = [];

            if (Bar_OverviewChecked)
            {
                BarCollection = [new ColumnSeries<DateTimePoint>
                {
                    Values = overview,
                    Name = "总览",
                }];
            }
            else if (Bar_PurposeChecked)
            {
                foreach (var item in purpose)
                {
                    BarCollection = [new ColumnSeries<DateTimePoint>
                    {
                        Values = item.Value,
                        Name = item.Key,
                    }, .. BarCollection];
                }
            }
            else if (Bar_ModelChecked)
            {
                foreach (var item in model)
                {
                    BarCollection = [new ColumnSeries<DateTimePoint>
                    {
                        Values = item.Value,
                        Name = item.Key,
                    }, .. BarCollection];
                }
            }
            else if (Bar_ServiceChecked)
            {
                foreach (var item in service)
                {
                    BarCollection = [new ColumnSeries<DateTimePoint>
                    {
                        Values = item.Value,
                        Name = item.Key,
                    }, .. BarCollection];
                }
            }

            OnPropertyChanged(nameof(BarCollection));
            ChangeBarChartColor();
        }

        private void DrawPieChart()
        {
            SKColor color = ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Dark ? SKColors.White : SKColors.Black;
            var paint = new SolidColorPaint { Color = color, SKTypeface = SKTypeface.FromFamilyName("微软雅黑") }; ;
           
            Pie_ModelCollection = [];
            foreach (var model in FilterResult.GroupBy(x => x.ModelName).Select(x => new { Name = x.Key, Group = x, Count = x.Count() }))
            {
                var series = new PieSeries<int>
                {
                    Values = [UnitCountChecked ? model.Count : model.Group.Sum(x => x.InputToken + x.OutputToken)],
                    Name = model.Name,
                    Stroke = null,
                    DataLabelsSize = 14,
                    DataLabelsPaint = paint,
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = o => model.Name,
                };
                Pie_ModelCollection = [series, .. Pie_ModelCollection];
            }

            Pie_PurposeCollection = [];
            foreach (var model in FilterResult.GroupBy(x => x.Purpose).Select(x => new { Name = x.Key, Group = x, Count = x.Count() }))
            {
                var series = new PieSeries<int>
                {
                    Values = [UnitCountChecked ? model.Count : model.Group.Sum(x => x.InputToken + x.OutputToken)],
                    Name = model.Name,
                    Stroke = null,
                    DataLabelsSize = 14,
                    DataLabelsPaint = paint,
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = o => model.Name,
                };
                Pie_PurposeCollection = [series, .. Pie_PurposeCollection];
            }

            Pie_ServiceCollection = [];
            foreach (var model in FilterResult.GroupBy(x => x.Endpoint).Select(x => new { Name = x.Key, Group = x, Count = x.Count() }))
            {
                var series = new PieSeries<int>
                {
                    Values = [UnitCountChecked ? model.Count : model.Group.Sum(x => x.InputToken + x.OutputToken)],
                    Name = model.Name,
                    Stroke = null,
                    DataLabelsSize = 14,
                    DataLabelsPaint = paint,
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = o => model.Name,
                };
                Pie_ServiceCollection = [series, .. Pie_ServiceCollection];
            }

            OnPropertyChanged(nameof(Pie_ModelCollection));
            OnPropertyChanged(nameof(Pie_PurposeCollection));
            OnPropertyChanged(nameof(Pie_ServiceCollection));
        }

        private void LoadChartPerference()
        {
            Pie_ModelChecked = ConfigHelper.GetConfig("Pie_ModelChekced", false);
            Pie_PurposeChecked = ConfigHelper.GetConfig("Pie_PurposeChecked", false);
            Pie_ServiceChecked = ConfigHelper.GetConfig("Pie_ServiceChecked", false);
            Bar_ModelChecked = ConfigHelper.GetConfig("Bar_ModelChecked", false);
            Bar_OverviewChecked = ConfigHelper.GetConfig("Bar_OverviewChecked", false);
            Bar_PurposeChecked = ConfigHelper.GetConfig("Bar_PurposeChecked", false);
            Bar_ServiceChecked = ConfigHelper.GetConfig("Bar_ServiceChecked", false);
            UnitCountChecked = ConfigHelper.GetConfig("UnitCountChecked", false);
            UnitTokenChecked = ConfigHelper.GetConfig("UnitTokenChecked", false);

            if (!UnitTokenChecked && !UnitCountChecked)
            {
                UnitCountChecked = true;
            }

            if (!Bar_ModelChecked
                && !Bar_OverviewChecked
                && !Bar_PurposeChecked
                && !Bar_ServiceChecked)
            {
                Bar_OverviewChecked = true;
            }
            TimeDetailChart.TooltipTextPaint = new SolidColorPaint { Color = SKColors.Black, SKTypeface = SKTypeface.FromFamilyName("微软雅黑") };
            
            PieModelChart.TooltipTextPaint = new SolidColorPaint { Color = SKColors.Black, SKTypeface = SKTypeface.FromFamilyName("微软雅黑") };
            PiePurposeChart.TooltipTextPaint = new SolidColorPaint { Color = SKColors.Black, SKTypeface = SKTypeface.FromFamilyName("微软雅黑") };
            PieServiceChart.TooltipTextPaint = new SolidColorPaint { Color = SKColors.Black, SKTypeface = SKTypeface.FromFamilyName("微软雅黑") };
        }

        private async Task LoadFilterGroup()
        {
            Services.Clear();
            Models.Clear();
            Purposes.Clear();

            var (services, models, puropses) = await Task.Run(Usage.GetGroups);
            foreach (var group in services)
            {
                var item = new CheckableItem()
                {
                    Name = group,
                    Checked = true
                };
                Services.Add(item);
                item.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(CheckableItem.Checked))
                    {
                        OnPropertyChanged(nameof(CheckedServiceCount));
                    }
                };
            }
            foreach (var group in puropses)
            {
                var item = new CheckableItem()
                {
                    Name = group,
                    Checked = true
                };
                Purposes.Add(item);
                item.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(CheckableItem.Checked))
                    {
                        OnPropertyChanged(nameof(CheckedPurposeCount));
                    }
                };
            }
            foreach (var group in models)
            {
                var item = new CheckableItem()
                {
                    Name = group,
                    Checked = true
                };
                Models.Add(item);
                item.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(CheckableItem.Checked))
                    {
                        OnPropertyChanged(nameof(CheckedModelCount));
                    }
                };
            }
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (PageLoaded)
            {
                return;
            }
            PageLoaded = true;
            FilterStartDate = DateTime.Now.Date;
            FilterEndDate = DateTime.Now.Date;
            ChangeBarChartColor();

            Services.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CheckedServiceCount));
            Models.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CheckedModelCount));
            Purposes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CheckedPurposeCount));
            ThemeManager.Current.ActualApplicationThemeChanged += (_, _) => ChangeBarChartColor();
            Usage.OnUsageInserted += Usage_OnUsageInserted;

            await LoadFilterGroup();
            LoadChartPerference();
            UpdateGridLayout();
            DoFilter();

            TriggerFilter();
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton radioButton = sender as RadioButton;
            var binding = radioButton.GetBindingExpression(RadioButton.IsCheckedProperty);
            var propertyName = binding?.ParentBinding?.Path?.Path;
            OnPropertyChanged(propertyName);
            ConfigHelper.SetConfig(propertyName, (bool)GetType().GetProperty(propertyName).GetValue(this));
            if (propertyName.StartsWith("Unit") || propertyName.StartsWith("Bar_"))
            {
                DoFilter();
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            DoFilter();
        }

        private void TriggerFilter()
        {
            OnPropertyChanged(nameof(FilterStartDate));
            OnPropertyChanged(nameof(FilterEndDate));
            OnPropertyChanged(nameof(Pie_ModelChecked));
            OnPropertyChanged(nameof(Pie_PurposeChecked));
            OnPropertyChanged(nameof(Pie_ServiceChecked));
            OnPropertyChanged(nameof(Bar_ModelChecked));
            OnPropertyChanged(nameof(Bar_OverviewChecked));
            OnPropertyChanged(nameof(Bar_PurposeChecked));
            OnPropertyChanged(nameof(Bar_ServiceChecked));
            OnPropertyChanged(nameof(UnitCountChecked));
            OnPropertyChanged(nameof(UnitTokenChecked));
        }

        private void UpdateGridLayout()
        {
            Column1.Width = !Pie_ModelChecked ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            Column2.Width = !Pie_PurposeChecked ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
            Column3.Width = !Pie_ServiceChecked ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        }

        private void UpdateTokenCount()
        {
            int callCount = 0;
            int inputTokenCount = 0;
            int outputTokenCount = 0;
            int totalTokenCount = 0;
            int maxRPM = 0;

            Dictionary<string, int> rpm = [];
            foreach (var item in FilterResult)
            {
                callCount++;
                inputTokenCount += item.InputToken;
                outputTokenCount += item.OutputToken;
                totalTokenCount = inputTokenCount + outputTokenCount;
                string minuteKey = item.Time.ToString("yyyy-MM-dd HH:mm");
                if (rpm.ContainsKey(minuteKey))
                {
                    rpm[minuteKey]++;
                }
                else
                {
                    rpm.Add(minuteKey, 1);
                }
            }
            maxRPM = rpm.Count > 0 ? rpm.Max(x => x.Value) : 0;

            AnimateTextChange(CallCountDisplay, callCount);
            AnimateTextChange(InputTokenDisplay, inputTokenCount);
            AnimateTextChange(OutputTokenDisplay, outputTokenCount);
            AnimateTextChange(TotalTokenDisplay, totalTokenCount);
            AnimateTextChange(MaxRPMDisplay, maxRPM);
        }

        private void Usage_OnUsageInserted(Usage usage)
        {
            if (usage.Time >= FilterStartDate && usage.Time <= FilterEndDate.AddDays(1)
                && Services.Any(x => x.Name == usage.Endpoint && x.Checked)
                && Models.Any(x => x.Name == usage.ModelName && x.Checked)
                && Purposes.Any(x => x.Name == usage.Purpose && x.Checked))
            {
                FilterResult.Add(usage);
                Dispatcher.BeginInvoke(() =>
                {
                    DrawPieChart();
                    DrawBarChart();
                    UpdateTokenCount();
                });
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.ShowConfirm("确定要导出吗？"))
            {
                SaveFileDialog dialog = new()
                {
                    AddExtension = true,
                    CheckPathExists = true,
                    Filter = "逗号分隔文件|*.csv|所有文件|*.*",
                };
                if (!(dialog.ShowDialog() ?? false))
                {
                    return;
                }
                using FileStream fileStream = new(dialog.FileName, FileMode.Create, FileAccess.Write, FileShare.Write);
                using StreamWriter writer = new(fileStream, Encoding.UTF8);
                writer.WriteLine("服务商,模型,用途,输入Token,输出Token,总计Token,时间");
                foreach (var item in FilterResult)
                {
                    writer.WriteLine($"{item.Endpoint},{item.ModelName},{item.Purpose},{item.InputToken},{item.OutputToken},{item.InputToken + item.OutputToken},{item.Time:G}");
                }

                MainWindow.ShowInfo($"导出了 {FilterResult.Count} 条数据");
            }
        }
    }
}