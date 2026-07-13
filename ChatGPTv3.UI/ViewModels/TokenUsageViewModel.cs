using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Win32;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;

namespace ChatGPTv3.UI.ViewModels;

public partial class TokenUsageSummaryCard : ObservableObject
{
    public string Title { get; init; } = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;
}

public partial class TokenUsageBreakdownItem : ObservableObject
{
    public string Label { get; init; } = string.Empty;

    public int RecordCount { get; init; }

    public int PromptTokens { get; init; }

    public int CachedPromptTokens { get; init; }

    public int CompletionTokens { get; init; }

    public int TotalTokens { get; init; }

    public decimal EstimatedCost { get; init; }

    public string CostText => EstimatedCost.ToString("F4");
}

public partial class TokenUsageRecordItem : ObservableObject
{
    public DateTime Time { get; init; }

    public string EndPoint { get; init; } = string.Empty;

    public string Purpose { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string APIKeyHint { get; init; } = string.Empty;

    public int PromptTokens { get; init; }

    public int CachedPromptTokens { get; init; }

    public int CompletionTokens { get; init; }

    public int TotalTokens { get; init; }

    public decimal EstimatedCost { get; init; }

    public string CostText => EstimatedCost.ToString("F4");
}

public partial class TokenUsageFilterItem : ObservableObject
{
    public string Name { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _checked;
}

public partial class TokenUsageViewModel : ViewModelBase
{
    public ObservableCollection<TokenUsageFilterItem> Services { get; } = [];

    public ObservableCollection<TokenUsageFilterItem> Models { get; } = [];

    public ObservableCollection<TokenUsageFilterItem> Purposes { get; } = [];

    public ObservableCollection<TokenUsageFilterItem> ApiKeys { get; } = [];

    public ObservableCollection<TokenUsageSummaryCard> SummaryCards { get; } = [];

    public ObservableCollection<TokenUsageRecordItem> Records { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ServiceSelectionText))]
    private int _checkedServiceCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModelSelectionText))]
    private int _checkedModelCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PurposeSelectionText))]
    private int _checkedPurposeCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApiKeySelectionText))]
    private int _checkedApiKeyCount;

    [ObservableProperty]
    private DateTime? _startDate = DateTime.Today.AddDays(-7);

    [ObservableProperty]
    private DateTime? _endDate = DateTime.Today;

    [ObservableProperty]
    private IEnumerable<ISeries> _trendSeries = [];

    [ObservableProperty]
    private IEnumerable<ISeries> _purposePieSeries = [];

    [ObservableProperty]
    private IEnumerable<ISeries> _modelPieSeries = [];

    [ObservableProperty]
    private Axis[] _trendXAxes = [];

    [ObservableProperty]
    private Axis[] _trendYAxes = [];

    [ObservableProperty]
    private bool _hasData;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyPropertyChangedFor(nameof(CanGoPrevious))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(PageInfoText))]
    [NotifyPropertyChangedFor(nameof(DisplayPageIndex))]
    private int _pageIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyPropertyChangedFor(nameof(PageInfoText))]
    private int _totalRecordCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyPropertyChangedFor(nameof(PageInfoText))]
    private int _pageSize = 20;

    public List<int> PageSizeOptions { get; } = [20, 50, 100, 200];

    [ObservableProperty]
    private bool _isFilterExpanded = true;

    [ObservableProperty]
    private bool _isSummaryExpanded = true;

    [ObservableProperty]
    private bool _isPieChartExpanded = true;

    [ObservableProperty]
    private bool _isTrendChartExpanded = true;

    [ObservableProperty]
    private bool _isDetailsExpanded;

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalRecordCount / PageSize);

    public int DisplayPageIndex
    {
        get => PageIndex + 1;
        set
        {
            var newIdx = value - 1;
            if (newIdx >= 0 && newIdx != PageIndex && newIdx < TotalPages)
            {
                PageIndex = newIdx;
                _ = LoadAsync(includeFilters: false);
            }
        }
    }

    public bool CanGoPrevious => PageIndex > 0;

    public bool CanGoNext => PageIndex < TotalPages - 1;

    public string PageInfoText => TotalRecordCount <= 0
        ? "无数据"
        : $"第 {PageIndex + 1}/{TotalPages} 页，共 {TotalRecordCount} 条";

    public bool HasTrendData => TrendSeries.Any();

    public bool HasPurposePieData => PurposePieSeries.Any();

    public bool HasModelPieData => ModelPieSeries.Any();

    public SolidColorPaint ChartTextPaint { get; } = new(SKColors.White);

    public SolidColorPaint ChartSubtleTextPaint { get; } = new(new SKColor(180, 188, 204));

    public SolidColorPaint ChartGridPaint { get; } = new(new SKColor(90, 96, 115)) { StrokeThickness = 1 };

    public string ServiceSelectionText => GetSelectionText(CheckedServiceCount, Services.Count);

    public string ModelSelectionText => GetSelectionText(CheckedModelCount, Models.Count);

    public string PurposeSelectionText => GetSelectionText(CheckedPurposeCount, Purposes.Count);

    public string ApiKeySelectionText => GetSelectionText(CheckedApiKeyCount, ApiKeys.Count);

    public TokenUsageViewModel()
    {
        _isFilterExpanded = ConfigManager.GetConfig("TokenStats_FilterExpanded", true);
        _isSummaryExpanded = ConfigManager.GetConfig("TokenStats_SummaryExpanded", true);
        _isPieChartExpanded = ConfigManager.GetConfig("TokenStats_PieChartExpanded", true);
        _isTrendChartExpanded = ConfigManager.GetConfig("TokenStats_TrendChartExpanded", true);
        _isDetailsExpanded = ConfigManager.GetConfig("TokenStats_DetailsExpanded", false);
        _pageSize = ConfigManager.GetConfig("TokenStats_PageSize", 50);

        SummaryCards.Add(new TokenUsageSummaryCard { Title = "调用次数", Value = "0" });
        SummaryCards.Add(new TokenUsageSummaryCard { Title = "输入 Token", Value = "0" });
        SummaryCards.Add(new TokenUsageSummaryCard { Title = "输出 Token", Value = "0" });
        SummaryCards.Add(new TokenUsageSummaryCard { Title = "缓存 Token", Value = "0" });
        SummaryCards.Add(new TokenUsageSummaryCard { Title = "总 Token", Value = "0" });
        SummaryCards.Add(new TokenUsageSummaryCard { Title = "缓存率", Value = "0.00%" });
        SummaryCards.Add(new TokenUsageSummaryCard { Title = "总花费", Value = "¥0.0000" });

        TokenUsage.OnInserted += HandleUsageInserted;
        _ = LoadAsync(includeFilters: true);
    }

    partial void OnPageSizeChanged(int value)
    {
        ConfigManager.SetConfig("TokenStats_PageSize", value);
        PageIndex = 0;
        _ = LoadAsync(includeFilters: false);
    }

    partial void OnIsFilterExpandedChanged(bool value)
        => ConfigManager.SetConfig("TokenStats_FilterExpanded", value);

    partial void OnIsSummaryExpandedChanged(bool value)
        => ConfigManager.SetConfig("TokenStats_SummaryExpanded", value);

    partial void OnIsPieChartExpandedChanged(bool value)
        => ConfigManager.SetConfig("TokenStats_PieChartExpanded", value);

    partial void OnIsTrendChartExpandedChanged(bool value)
        => ConfigManager.SetConfig("TokenStats_TrendChartExpanded", value);

    partial void OnIsDetailsExpandedChanged(bool value)
        => ConfigManager.SetConfig("TokenStats_DetailsExpanded", value);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        PageIndex = 0;
        await LoadAsync(includeFilters: false);
    }

    [RelayCommand]
    private async Task ResetFiltersAsync()
    {
        StartDate = DateTime.Today;
        EndDate = DateTime.Today;
        SetAll(Services, true);
        SetAll(Models, true);
        SetAll(Purposes, true);
        SetAll(ApiKeys, true);
        UpdateCheckedCounts();
        PageIndex = 0;
        await LoadAsync(includeFilters: false);
    }

    [RelayCommand]
    private async Task ApplyTodayAsync()
    {
        StartDate = DateTime.Today;
        EndDate = DateTime.Today;
        PageIndex = 0;
        await LoadAsync(includeFilters: false);
    }

    [RelayCommand]
    private async Task ApplyYesterdayAsync()
    {
        StartDate = DateTime.Today.AddDays(-1);
        EndDate = DateTime.Today.AddDays(-1);
        PageIndex = 0;
        await LoadAsync(includeFilters: false);
    }

    [RelayCommand]
    private async Task ApplyLast7DaysAsync()
    {
        StartDate = DateTime.Today.AddDays(-6);
        EndDate = DateTime.Today;
        PageIndex = 0;
        await LoadAsync(includeFilters: false);
    }

    [RelayCommand]
    private async Task ApplyLast30DaysAsync()
    {
        StartDate = DateTime.Today.AddDays(-29);
        EndDate = DateTime.Today;
        PageIndex = 0;
        await LoadAsync(includeFilters: false);
    }

    [RelayCommand]
    private void SelectAllServices() => SetSelectionAndRefresh(Services, true);

    [RelayCommand]
    private void ClearServices() => SetSelectionAndRefresh(Services, false);

    [RelayCommand]
    private void SelectAllModels() => SetSelectionAndRefresh(Models, true);

    [RelayCommand]
    private void ClearModels() => SetSelectionAndRefresh(Models, false);

    [RelayCommand]
    private void SelectAllPurposes() => SetSelectionAndRefresh(Purposes, true);

    [RelayCommand]
    private void ClearPurposes() => SetSelectionAndRefresh(Purposes, false);

    [RelayCommand]
    private void SelectAllApiKeys() => SetSelectionAndRefresh(ApiKeys, true);

    [RelayCommand]
    private void ClearApiKeys() => SetSelectionAndRefresh(ApiKeys, false);

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (StartDate == null || EndDate == null)
        {
            Growl.Warning("请选择日期范围");
            return;
        }

        IsLoading = true;

        try
        {
            var result = await Task.Run(() =>
            {
                var filters = TokenUsage.GetFilterOptions();
                var query = new TokenUsageQuery
                {
                    Start = StartDate.Value.Date,
                    End = EndDate.Value.Date.AddDays(1).AddTicks(-1),
                    EndPoints = GetSelectedValues(Services, filters.EndPoints),
                    Models = GetSelectedValues(Models, filters.Models),
                    Purposes = GetSelectedValues(Purposes, filters.Purposes),
                    APIKeyHints = GetSelectedValues(ApiKeys, filters.ApiKeyHints),
                    Skip = 0,
                    Take = int.MaxValue
                };
                return TokenUsage.QueryReport(query);
            });

            var allRecords = result.Records;
            if (allRecords.Count == 0)
            {
                Growl.Info("当前没有可导出的数据");
                return;
            }

            var dialog = new SaveFileDialog
            {
                AddExtension = true,
                Filter = "逗号分隔文件|*.csv|所有文件|*.*",
                FileName = $"token-usage-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            using var writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8);
            writer.WriteLine("时间,服务商,模型,用途,APIKey,输入Token,缓存Token,输出Token,总Token,估算成本");
            foreach (var item in allRecords)
            {
                writer.WriteLine(string.Join(",",
                    EscapeCsv(item.Time.ToString("yyyy-MM-dd HH:mm:ss")),
                    EscapeCsv(item.EndPoint),
                    EscapeCsv(item.Model),
                    EscapeCsv(item.Purpose),
                    EscapeCsv(item.APIKeyHint),
                    item.PromptTokens.ToString(CultureInfo.InvariantCulture),
                    item.CachedPromptTokens.ToString(CultureInfo.InvariantCulture),
                    item.CompletionTokens.ToString(CultureInfo.InvariantCulture),
                    item.TotalTokens.ToString(CultureInfo.InvariantCulture),
                    item.EstimatedCost.ToString(CultureInfo.InvariantCulture)));
            }
        }
        catch (Exception ex)
        {
            Growl.Error($"导出失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadAsync(bool includeFilters)
    {
        if (StartDate == null || EndDate == null)
        {
            Growl.Warning("请选择开始和结束日期");
            return;
        }

        var start = StartDate.Value.Date;
        var end = EndDate.Value.Date.AddDays(1).AddTicks(-1);
        if (end < start)
        {
            Growl.Warning("结束日期不能早于开始日期");
            return;
        }

        try
        {
            IsLoading = true;

            var result = await Task.Run(() =>
            {
                var filters = TokenUsage.GetFilterOptions();
                var query = new TokenUsageQuery
                {
                    Start = start,
                    End = end,
                    EndPoints = GetSelectedValues(Services, filters.EndPoints),
                    Models = GetSelectedValues(Models, filters.Models),
                    Purposes = GetSelectedValues(Purposes, filters.Purposes),
                    APIKeyHints = GetSelectedValues(ApiKeys, filters.ApiKeyHints),
                    Skip = PageIndex * PageSize,
                    Take = PageSize
                };
                var report = TokenUsage.QueryReport(query);
                return (report, filters);
            });

            if (includeFilters)
            {
                ApplyFilterOptions(result.filters);
            }
            else
            {
                RefreshOptionsKeepingSelection(Services, result.filters.EndPoints, nameof(CheckedServiceCount));
            }

            if (!includeFilters)
            {
                RefreshOptionsKeepingSelection(Models, result.filters.Models, nameof(CheckedModelCount));
                RefreshOptionsKeepingSelection(Purposes, result.filters.Purposes, nameof(CheckedPurposeCount));
                RefreshOptionsKeepingSelection(ApiKeys, result.filters.ApiKeyHints, nameof(CheckedApiKeyCount));
            }

            ApplyReport(result.report);
            UpdateCheckedCounts();
        }
        catch (Exception ex)
        {
            Growl.Error($"加载 Token 统计失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilterOptions(TokenUsageFilterOptions filters)
    {
        ReplaceFilters(Services, filters.EndPoints);
        ReplaceFilters(Models, filters.Models);
        ReplaceFilters(Purposes, filters.Purposes);
        ReplaceFilters(ApiKeys, filters.ApiKeyHints);
    }

    private void RefreshOptionsKeepingSelection(ObservableCollection<TokenUsageFilterItem> target, IEnumerable<string> values, string _)
    {
        var selected = target.Where(x => x.Checked).Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var hadItems = target.Count > 0;
        target.Clear();
        foreach (var value in values)
        {
            var item = new TokenUsageFilterItem
            {
                Name = value,
                Checked = hadItems ? selected.Contains(value) : true
            };
            item.PropertyChanged += FilterItem_PropertyChanged;
            target.Add(item);
        }
    }

    private void ReplaceFilters(ObservableCollection<TokenUsageFilterItem> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            var item = new TokenUsageFilterItem
            {
                Name = value,
                Checked = true
            };
            item.PropertyChanged += FilterItem_PropertyChanged;
            target.Add(item);
        }
    }

    private void ApplyReport(TokenUsageReport report)
    {
        SummaryCards[0].Value = report.Summary.CallCount.ToString("N0");
        SummaryCards[1].Value = report.Summary.PromptTokens.ToString("N0");
        SummaryCards[2].Value = report.Summary.CompletionTokens.ToString("N0");
        SummaryCards[3].Value = report.Summary.CachedPromptTokens.ToString("N0");
        SummaryCards[4].Value = report.Summary.TotalTokens.ToString("N0");
        SummaryCards[5].Value = report.Summary.CacheRate.ToString("P2");
        SummaryCards[6].Value = $"¥{report.Summary.EstimatedCost:F4}";

        TrendXAxes =
        [
            new Axis
            {
                Labels = report.Trend.Select(x => x.Label).ToArray(),
                LabelsRotation = report.Trend.Count > 12 ? 25 : 0,
                LabelsPaint = ChartSubtleTextPaint,
                SeparatorsPaint = ChartGridPaint
            }
        ];
        TrendYAxes =
        [
            new Axis
            {
                LabelsPaint = ChartSubtleTextPaint,
                SeparatorsPaint = ChartGridPaint
            }
        ];

        TrendSeries = report.Trend.Count == 0
            ? []
            :
            [
                new StackedColumnSeries<double>
                {
                    Name = "输入",
                    Values = report.Trend.Select(x => (double)x.PromptTokens).ToArray()
                },
                new StackedColumnSeries<double>
                {
                    Name = "缓存",
                    Values = report.Trend.Select(x => (double)x.CachedPromptTokens).ToArray()
                },
                new StackedColumnSeries<double>
                {
                    Name = "输出",
                    Values = report.Trend.Select(x => (double)x.CompletionTokens).ToArray()
                }
            ];

        PurposePieSeries = report.PurposeBreakdown
            .Where(x => x.TotalTokens > 0)
            .Take(10)
            .Select(x => new PieSeries<double>
            {
                Name = x.Label,
                Values = [x.TotalTokens],
                DataLabelsSize = 12,
                DataLabelsPaint = ChartTextPaint,
                DataLabelsFormatter = point => x.Label
            })
            .Cast<ISeries>()
            .ToArray();

        ModelPieSeries = report.ModelBreakdown
            .Where(x => x.TotalTokens > 0)
            .Take(10)
            .Select(x => new PieSeries<double>
            {
                Name = x.Label,
                Values = [x.TotalTokens],
                DataLabelsSize = 12,
                DataLabelsPaint = ChartTextPaint,
                DataLabelsFormatter = point => x.Label
            })
            .Cast<ISeries>()
            .ToArray();

        ReplaceCollection(Records, report.Records.Select(x => new TokenUsageRecordItem
        {
            Time = x.Time,
            EndPoint = x.EndPoint,
            Purpose = x.Purpose,
            Model = x.Model,
            APIKeyHint = x.APIKeyHint,
            PromptTokens = x.PromptTokens,
            CachedPromptTokens = x.CachedPromptTokens,
            CompletionTokens = x.CompletionTokens,
            TotalTokens = x.TotalTokens,
            EstimatedCost = x.EstimatedCost
        }));

        HasData = report.Summary.RecordCount > 0;
        TotalRecordCount = report.TotalDetailCount;
        OnPropertyChanged(nameof(HasTrendData));
        OnPropertyChanged(nameof(HasPurposePieData));
        OnPropertyChanged(nameof(HasModelPieData));
    }

    private void SetSelectionAndRefresh(ObservableCollection<TokenUsageFilterItem> items, bool isChecked)
    {
        SetAll(items, isChecked);
        UpdateCheckedCounts();
        PageIndex = 0;
        _ = LoadAsync(includeFilters: false);
    }

    private static void SetAll(IEnumerable<TokenUsageFilterItem> items, bool isChecked)
    {
        foreach (var item in items)
        {
            item.Checked = isChecked;
        }
    }

    private void UpdateCheckedCounts()
    {
        CheckedServiceCount = Services.Count(x => x.Checked);
        CheckedModelCount = Models.Count(x => x.Checked);
        CheckedPurposeCount = Purposes.Count(x => x.Checked);
        CheckedApiKeyCount = ApiKeys.Count(x => x.Checked);
    }

    private static List<string>? GetSelectedValues(ObservableCollection<TokenUsageFilterItem> current, List<string> fallbackAll)
    {
        if (current.Count == 0)
        {
            return null;
        }

        var selected = current.Where(x => x.Checked).Select(x => x.Name).ToList();
        return selected.Count == current.Count ? null : selected;
    }

    private static string GetSelectionText(int checkedCount, int totalCount)
    {
        if (totalCount == 0)
        {
            return "暂无数据";
        }

        if (checkedCount <= 0)
        {
            return "未选择";
        }

        if (checkedCount >= totalCount)
        {
            return $"全部 ({totalCount})";
        }

        return $"已选 {checkedCount}/{totalCount}";
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private void FilterItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TokenUsageFilterItem.Checked))
        {
            return;
        }

        UpdateCheckedCounts();
    }

    private volatile bool _needsRefresh;
    private volatile bool _isVisible;

    public void NotifyVisibilityChanged(bool isVisible)
    {
        _isVisible = isVisible;
        if (isVisible && _needsRefresh)
        {
            _needsRefresh = false;
            PageIndex = 0;
            _ = LoadAsync(includeFilters: false);
        }
    }

    private void HandleUsageInserted(TokenUsage usage)
    {
        _needsRefresh = true;
        if (_isVisible)
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_needsRefresh)
                {
                    _needsRefresh = false;
                    PageIndex = 0;
                    _ = LoadAsync(includeFilters: false);
                }
            }));
        }
    }

    private static string EscapeCsv(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}