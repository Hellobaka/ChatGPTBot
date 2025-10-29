using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using me.cqp.luohuaming.ChatGPT.UI.Model;
using Microsoft.SqlServer.Server;
using Microsoft.Win32;
using ModernWpf;
using PropertyChanged;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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

        public bool Bar_InputTokenChecked { get; set; }

        public bool Bar_InputCachedTokenChecked { get; set; }

        public bool Bar_OutputTokenChecked { get; set; }

        public IEnumerable<ISeries> BarCollection { get; set; } = [];

        public int CheckedModelCount => Models.Count(item => item.Checked);

        public int CheckedPurposeCount => Purposes.Count(item => item.Checked);

        public int CheckedServiceCount => Services.Count(item => item.Checked);

        public DateTime FilterEndDate { get; set; }

        public List<Usage> FilterResult { get; set; } = [];

        public DateTime FilterStartDate { get; set; }

        public long CallCount { get; set; }

        public long InputTokenCount { get; set; }

        public long CachedTokenCount { get; set; }

        public long OutputTokenCount { get; set; }

        public decimal PredictConsume { get; set; }

        public ObservableCollection<CheckableItem> Models { get; set; } = [];

        public bool PageLoaded { get; set; }

        public bool Pie_ModelChecked { get; set; }

        public IEnumerable<ISeries> Pie_ModelCollection { get; set; } = [];

        public bool Pie_PurposeChecked { get; set; }

        public IEnumerable<ISeries> Pie_PurposeCollection { get; set; } = [];

        public bool Pie_ServiceChecked { get; set; }

        public IEnumerable<ISeries> Pie_ServiceCollection { get; set; } = [];

        public ObservableCollection<CheckableItem> Purposes { get; set; } = [];

        public ObservableCollection<CheckableItem> Services { get; set; } = [];

        public long TotalTokenCount { get; set; }

        public bool UnitCountChecked { get; set; }

        public bool UnitTokenChecked { get; set; }

        public bool UnitConsumeChecked { get; set; }

        [AlsoNotifyFor(nameof(Bar_LegendPosition))]
        public bool Bar_ShowLegend { get; set; }

        public LegendPosition Bar_LegendPosition => Bar_ShowLegend ? LegendPosition.Right : LegendPosition.Hidden;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void AnimateTextChange(TextBlock textBlock, long newValue)
        {
            AnimateTextChange(textBlock, (decimal)newValue);
        }

        private void AnimateTextChange(TextBlock textBlock, decimal newValue)
        {
            var binding = textBlock.GetBindingExpression(TextBlock.TextProperty);
            var propertyName = binding?.ParentBinding?.Path?.Path;
            var property = GetType().GetProperty(propertyName);
            var fadeOutAnimation = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.2)));
            fadeOutAnimation.Completed += (s, a) =>
            {
                if (property?.PropertyType == typeof(decimal))
                {
                    property.SetValue(this, newValue);
                }
                else if (property?.PropertyType == typeof(long))
                {
                    property.SetValue(this, (long)newValue);
                }
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
            if (propertyName.StartsWith("Unit") || propertyName.StartsWith("Bar_"))
            {
                DoFilter();
            }
        }

        private async void DoFilter()
        {
            await LoadFilterGroup(false);

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
            var rawData = FilterResult.GroupBy(x => x.Time.ToString(format))
                .Select(x =>
                    new TimeGroupData
                    {
                        GroupName = x.Key,
                        Group = x,
                        Format = format,
                    });

            if (dayMode)
            {
                TimeDetailChart.XAxes = [new DateTimeAxis(TimeSpan.FromHours(1), date => date.ToString("HH:mm"))];
            }
            else
            {
                TimeDetailChart.XAxes = [new DateTimeAxis(TimeSpan.FromDays(1), date => date.ToString("yyyy-MM-dd"))];
            }
            BarCollection = [];
            if (UnitCountChecked)
            {
                BuildSimpleColumns(rawData, x => x.Count());
            }
            else if (UnitTokenChecked)
            {
                BuildTokenColumns(rawData);
            }
            else
            {
                BuildSimpleColumns(rawData, x => (double)x.Sum(x => x.PredictConsume));
            }

            OnPropertyChanged(nameof(BarCollection));
            ChangeBarChartColor();
        }

        private void BuildSimpleColumns(IEnumerable<TimeGroupData> rawData, Func<IGrouping<string, Usage>, double> func)
        {
            List<DateTimePoint> overview = [];
            Dictionary<string, List<DateTimePoint>> model = [];
            Dictionary<string, List<DateTimePoint>> purpose = [];
            Dictionary<string, List<DateTimePoint>> service = [];
            foreach (var item in rawData)
            {
                DateTime pointDate = DateTime.TryParseExact(item.GroupName, item.Format, null, DateTimeStyles.None, out DateTime d) ? d : new();
                overview.Add(new() { DateTime = pointDate, Value = func(item.Group) });

                foreach (var g in item.Group.GroupBy(x => x.ModelName))
                {
                    if (!model.ContainsKey(g.Key))
                    {
                        model.Add(g.Key, []);
                    }
                    model[g.Key].Add(new() { DateTime = pointDate, Value = func(g) });
                }

                foreach (var g in item.Group.GroupBy(x => x.Purpose))
                {
                    if (!purpose.ContainsKey(g.Key))
                    {
                        purpose.Add(g.Key, []);
                    }
                    purpose[g.Key].Add(new() { DateTime = pointDate, Value = func(g) });
                }

                foreach (var g in item.Group.GroupBy(x => x.Endpoint))
                {
                    if (!service.ContainsKey(g.Key))
                    {
                        service.Add(g.Key, []);
                    }
                    service[g.Key].Add(new() { DateTime = pointDate, Value = func(g) });
                }
            }
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
        }

        private void BuildTokenColumns(IEnumerable<TimeGroupData> rawData)
        {
            void AddDataToBarCollection(string[] keys,
                                        Dictionary<string, List<DateTimePoint>> input,
                                        Dictionary<string, List<DateTimePoint>> inputCached,
                                        Dictionary<string, List<DateTimePoint>> output)
            {
                foreach (var item in keys)
                {
                    if (Bar_InputTokenChecked)
                    {
                        BarCollection = [new StackedColumnSeries<DateTimePoint>
                        {
                            Values = input[item],
                            Name = item + " - 输入",
                        }, ..BarCollection];
                    }
                    if (Bar_InputCachedTokenChecked)
                    {
                        BarCollection = [new StackedColumnSeries<DateTimePoint>
                        {
                            Values = inputCached[item],
                            Name = item + " - 输入缓存",
                        }, ..BarCollection];
                    }
                    if (Bar_OutputTokenChecked)
                    {
                        BarCollection = [new StackedColumnSeries<DateTimePoint>
                        {
                            Values = output[item],
                            Name = item + " - 输出",
                        }, ..BarCollection];
                    }
                }
            }
            List<DateTimePoint> overview_Input = [];
            List<DateTimePoint> overview_InputCache = [];
            List<DateTimePoint> overview_Output = [];
            Dictionary<string, List<DateTimePoint>> model_Input = [];
            Dictionary<string, List<DateTimePoint>> purpose_Input = [];
            Dictionary<string, List<DateTimePoint>> service_Input = [];
            Dictionary<string, List<DateTimePoint>> model_InputCache = [];
            Dictionary<string, List<DateTimePoint>> purpose_InputCache = [];
            Dictionary<string, List<DateTimePoint>> service_InputCache = [];
            Dictionary<string, List<DateTimePoint>> model_Output = [];
            Dictionary<string, List<DateTimePoint>> purpose_Output = [];
            Dictionary<string, List<DateTimePoint>> service_Output = [];
            foreach (var item in rawData)
            {
                DateTime pointDate = DateTime.TryParseExact(item.GroupName, item.Format, null, DateTimeStyles.None, out DateTime d) ? d : new();
                overview_Input.Add(new() { DateTime = pointDate, Value = item.Group.Sum(x => x.InputToken) });
                overview_InputCache.Add(new() { DateTime = pointDate, Value = item.Group.Sum(x => x.InputCacheToken) });
                overview_Output.Add(new() { DateTime = pointDate, Value = item.Group.Sum(x => x.OutputToken) });

                foreach (var g in item.Group.GroupBy(x => x.ModelName))
                {
                    if (!model_Input.ContainsKey(g.Key))
                    {
                        model_Input.Add(g.Key, []);
                        model_InputCache.Add(g.Key, []);
                        model_Output.Add(g.Key, []);
                    }
                    model_Input[g.Key].Add(new() { DateTime = pointDate, Value = g.Sum(x => x.InputToken) });
                    model_InputCache[g.Key].Add(new() { DateTime = pointDate, Value = g.Sum(x => x.InputCacheToken) });
                    model_Output[g.Key].Add(new() { DateTime = pointDate, Value = g.Sum(x => x.OutputToken) });
                }
                ;

                foreach (var g in item.Group.GroupBy(x => x.Purpose))
                {
                    if (!purpose_Input.ContainsKey(g.Key))
                    {
                        purpose_Input.Add(g.Key, []);
                        purpose_InputCache.Add(g.Key, []);
                        purpose_Output.Add(g.Key, []);
                    }
                    purpose_Input[g.Key].Add(new() { DateTime = pointDate, Value = g.Sum(x => x.InputToken) });
                    purpose_InputCache[g.Key].Add(new() { DateTime = pointDate, Value = g.Sum(x => x.InputCacheToken) });
                    purpose_Output[g.Key].Add(new() { DateTime = pointDate, Value = g.Sum(x => x.OutputToken) });
                }
                ;

                foreach (var g in item.Group.GroupBy(x => x.Endpoint))
                {
                    if (!service_Input.ContainsKey(g.Key))
                    {
                        service_Input.Add(g.Key, []);
                        service_InputCache.Add(g.Key, []);
                        service_Output.Add(g.Key, []);
                    }
                    service_Input[g.Key].Add(new() { DateTime = pointDate, Value = g.Sum(x => x.InputToken) });
                    service_InputCache[g.Key].Add(new() { DateTime = pointDate, Value = g.Sum(x => x.InputCacheToken) });
                    service_Output[g.Key].Add(new() { DateTime = pointDate, Value = g.Sum(x => x.OutputToken) });
                }
                ;
            }

            if (Bar_OverviewChecked)
            {
                AddDataToBarCollection(["总览"], new() { { "总览", overview_Input } }, new() { { "总览", overview_InputCache } }, new() { { "总览", overview_Output } });
            }
            else if (Bar_PurposeChecked)
            {
                AddDataToBarCollection(purpose_Input.Keys.ToArray(), purpose_Input, purpose_InputCache, purpose_Output);
            }
            else if (Bar_ModelChecked)
            {
                AddDataToBarCollection(model_Input.Keys.ToArray(), model_Input, model_InputCache, model_Output);
            }
            else if (Bar_ServiceChecked)
            {
                AddDataToBarCollection(service_Input.Keys.ToArray(), service_Input, service_InputCache, service_Output);
            }
        }

        private void DrawPieChart()
        {
            decimal[] GetPieChartValue(TimeGroupData data)
            {
                if (UnitCountChecked)
                {
                    return [data.Group.Count()];
                }
                if (UnitTokenChecked)
                {
                    return [data.Group.Sum(x => x.TotalToken)];
                }
                return [data.Group.Sum(x => x.PredictConsume)];
            }
            SKColor color = ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Dark ? SKColors.White : SKColors.Black;
            var paint = new SolidColorPaint { Color = color, SKTypeface = SKTypeface.FromFamilyName("微软雅黑") }; ;

            Pie_ModelCollection = [];
            foreach (var model in FilterResult.GroupBy(x => x.ModelName).Select(x => new TimeGroupData { GroupName = x.Key, Group = x, }))
            {
                var series = new PieSeries<decimal>
                {
                    Values = GetPieChartValue(model),
                    Name = model.GroupName,
                    Stroke = null,
                    DataLabelsSize = 14,
                    DataLabelsPaint = paint,
                    DataLabelsPosition = PolarLabelsPosition.Middle,
                    DataLabelsFormatter = o => model.GroupName,
                };
                Pie_ModelCollection = [series, .. Pie_ModelCollection];
            }

            Pie_PurposeCollection = [];
            foreach (var model in FilterResult.GroupBy(x => x.Purpose).Select(x => new TimeGroupData { GroupName = x.Key, Group = x, }))
            {
                var series = new PieSeries<decimal>
                {
                    Values = GetPieChartValue(model),
                    Name = model.GroupName,
                    Stroke = null,
                    DataLabelsSize = 14,
                    DataLabelsPaint = paint,
                    DataLabelsPosition = PolarLabelsPosition.Middle,
                    DataLabelsFormatter = o => model.GroupName,
                };
                Pie_PurposeCollection = [series, .. Pie_PurposeCollection];
            }

            Pie_ServiceCollection = [];
            foreach (var model in FilterResult.GroupBy(x => x.Endpoint).Select(x => new TimeGroupData { GroupName = x.Key, Group = x }))
            {
                var series = new PieSeries<decimal>
                {
                    Values = GetPieChartValue(model),
                    Name = model.GroupName,
                    Stroke = null,
                    DataLabelsSize = 14,
                    DataLabelsPaint = paint,
                    DataLabelsPosition = PolarLabelsPosition.Middle,
                    DataLabelsFormatter = o => model.GroupName,
                };
                Pie_ServiceCollection = [series, .. Pie_ServiceCollection];
            }

            OnPropertyChanged(nameof(Pie_ModelCollection));
            OnPropertyChanged(nameof(Pie_PurposeCollection));
            OnPropertyChanged(nameof(Pie_ServiceCollection));
        }

        private void LoadChartPreference()
        {
            Pie_ModelChecked = ConfigHelper.GetConfig("Pie_ModelChecked", false);
            Pie_PurposeChecked = ConfigHelper.GetConfig("Pie_PurposeChecked", false);
            Pie_ServiceChecked = ConfigHelper.GetConfig("Pie_ServiceChecked", false);
            Bar_ModelChecked = ConfigHelper.GetConfig("Bar_ModelChecked", false);
            Bar_OverviewChecked = ConfigHelper.GetConfig("Bar_OverviewChecked", false);
            Bar_PurposeChecked = ConfigHelper.GetConfig("Bar_PurposeChecked", false);
            Bar_ServiceChecked = ConfigHelper.GetConfig("Bar_ServiceChecked", false);
            UnitCountChecked = ConfigHelper.GetConfig("UnitCountChecked", false);
            UnitTokenChecked = ConfigHelper.GetConfig("UnitTokenChecked", false);
            UnitConsumeChecked = ConfigHelper.GetConfig("UnitConsumeChecked", false);
            Bar_ShowLegend = ConfigHelper.GetConfig("Bar_ShowLegend", true);
            Bar_InputTokenChecked = ConfigHelper.GetConfig("Bar_InputTokenChecked", true);
            Bar_InputCachedTokenChecked = ConfigHelper.GetConfig("Bar_InputCachedTokenChecked", true);
            Bar_OutputTokenChecked = ConfigHelper.GetConfig("Bar_OutputTokenChecked", true);

            if (!UnitTokenChecked && !UnitCountChecked && !UnitConsumeChecked)
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

            if (!Bar_InputTokenChecked
                && !Bar_InputCachedTokenChecked
                && !Bar_OutputTokenChecked)
            {
                Bar_InputTokenChecked = true;
                Bar_OutputTokenChecked = true;
            }
            TimeDetailChart.TooltipTextPaint = new SolidColorPaint { Color = SKColors.Black, SKTypeface = SKTypeface.FromFamilyName("微软雅黑") };

            PieModelChart.TooltipTextPaint = new SolidColorPaint { Color = SKColors.Black, SKTypeface = SKTypeface.FromFamilyName("微软雅黑") };
            PiePurposeChart.TooltipTextPaint = new SolidColorPaint { Color = SKColors.Black, SKTypeface = SKTypeface.FromFamilyName("微软雅黑") };
            PieServiceChart.TooltipTextPaint = new SolidColorPaint { Color = SKColors.Black, SKTypeface = SKTypeface.FromFamilyName("微软雅黑") };
        }

        private async Task LoadFilterGroup(bool newChecked)
        {
            var (services, models, purposes) = await Task.Run(Usage.GetGroups);
            var notContain = Services.Where(x => !services.Any(o => o == x.Name)).ToList();
            foreach (var item in notContain)
            {
                Services.Remove(item);
            }
            notContain = Models.Where(x => !models.Any(o => o == x.Name)).ToList();
            foreach (var item in notContain)
            {
                Models.Remove(item);
            }
            notContain = Purposes.Where(x => !purposes.Any(o => o == x.Name)).ToList();
            foreach (var item in notContain)
            {
                Purposes.Remove(item);
            }
            foreach (var group in services)
            {
                if (Services.Any(x => x.Name == group))
                {
                    continue;
                }
                var item = new CheckableItem()
                {
                    Name = group,
                    Checked = newChecked
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
            foreach (var group in purposes)
            {
                if (Purposes.Any(x => x.Name == group))
                {
                    continue;
                }
                var item = new CheckableItem()
                {
                    Name = group,
                    Checked = newChecked
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
                if (Models.Any(x => x.Name == group))
                {
                    continue;
                }
                var item = new CheckableItem()
                {
                    Name = group,
                    Checked = newChecked
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

            await LoadFilterGroup(true);
            LoadChartPreference();
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
            OnPropertyChanged(nameof(Bar_LegendPosition));
            OnPropertyChanged(nameof(Bar_InputCachedTokenChecked));
            OnPropertyChanged(nameof(Bar_InputTokenChecked));
            OnPropertyChanged(nameof(Bar_OutputTokenChecked));
            OnPropertyChanged(nameof(UnitCountChecked));
            OnPropertyChanged(nameof(UnitTokenChecked));
            OnPropertyChanged(nameof(UnitConsumeChecked));
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
            long inputTokenCount = 0;
            long outputTokenCount = 0;
            long totalTokenCount = 0;
            long cachedTokenCount = 0;
            decimal predictConsume = 0;

            Dictionary<string, int> rpm = [];
            foreach (var item in FilterResult)
            {
                callCount++;
                inputTokenCount += item.InputToken;
                outputTokenCount += item.OutputToken;
                totalTokenCount += item.TotalToken == 0 ? item.InputToken + item.OutputToken : item.TotalToken;
                cachedTokenCount += item.InputCacheToken;
                predictConsume += item.PredictConsume;
            }

            AnimateTextChange(CallCountDisplay, callCount);
            AnimateTextChange(InputTokenDisplay, inputTokenCount);
            AnimateTextChange(OutputTokenDisplay, outputTokenCount);
            AnimateTextChange(TotalTokenDisplay, totalTokenCount);
            AnimateTextChange(CachedTokenDisplay, cachedTokenCount);
            AnimateTextChange(PredictConsumeDisplay, predictConsume);
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