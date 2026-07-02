using SqlSugar;

namespace ChatGPTv3.Core.DB;

/// <summary>
/// Token usage tracking record.
/// </summary>
[SugarTable("Usage")]
public class TokenUsage
{
    public static event Action<TokenUsage>? OnInserted;

    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// The API endpoint URL used.
    /// </summary>
    public string EndPoint { get; set; } = string.Empty;

    /// <summary>
    /// The model name used.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// The purpose of this API call (e.g., "聊天", "记忆提取").
    /// </summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// The API key used (masked or truncated for privacy).
    /// </summary>
    public string APIKeyHint { get; set; } = string.Empty;

    /// <summary>
    /// Prompt tokens consumed.
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    /// Cached prompt tokens (cache hits, not billed at full price).
    /// </summary>
    public int CachedPromptTokens { get; set; }

    /// <summary>
    /// Completion tokens consumed.
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// Total tokens consumed.
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// Timestamp of the API call.
    /// </summary>
    public DateTime Time { get; set; } = DateTime.Now;

    // ── CRUD ─────────────────────────────────────────────

    public static void Insert(TokenUsage usage)
    {
        using var db = SQLiteManager.GetInstance();
        db.Insertable(usage).ExecuteCommand();
        OnInserted?.Invoke(usage);
    }

    public static List<TokenUsage> GetRange(DateTime start, DateTime end)
    {
        using var db = SQLiteManager.GetInstance();
        return db.Queryable<TokenUsage>()
            .Where(u => u.Time >= start && u.Time <= end)
            .OrderByDescending(u => u.Time)
            .Take(100)
            .ToList();
    }

    public static TokenUsageFilterOptions GetFilterOptions()
    {
        using var db = SQLiteManager.GetInstance();
        return new TokenUsageFilterOptions
        {
            EndPoints = db.Queryable<TokenUsage>()
                .Select(u => u.EndPoint)
                .Distinct()
                .ToList()
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .OrderBy(v => v, StringComparer.Ordinal)
                .ToList(),
            Purposes = db.Queryable<TokenUsage>()
                .Select(u => u.Purpose)
                .Distinct()
                .ToList()
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .OrderBy(v => v, StringComparer.Ordinal)
                .ToList(),
            Models = db.Queryable<TokenUsage>()
                .Select(u => u.Model)
                .Distinct()
                .ToList()
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .OrderBy(v => v, StringComparer.Ordinal)
                .ToList(),
            ApiKeyHints = db.Queryable<TokenUsage>()
                .Select(u => u.APIKeyHint)
                .Distinct()
                .ToList()
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .OrderBy(v => v, StringComparer.Ordinal)
                .ToList()
        };
    }

    public static TokenUsageReport QueryReport(TokenUsageQuery query)
    {
        using var db = SQLiteManager.GetInstance();

        var start = query.Start;
        var end = query.End;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        var usageQuery = db.Queryable<TokenUsage>()
            .Where(u => u.Time >= start && u.Time <= end);

        if (query.EndPoints is { Count: > 0 })
        {
            usageQuery = usageQuery.Where(u => query.EndPoints.Contains(u.EndPoint));
        }

        if (query.Purposes is { Count: > 0 })
        {
            usageQuery = usageQuery.Where(u => query.Purposes.Contains(u.Purpose));
        }

        if (query.Models is { Count: > 0 })
        {
            usageQuery = usageQuery.Where(u => query.Models.Contains(u.Model));
        }

        if (query.APIKeyHints is { Count: > 0 })
        {
            usageQuery = usageQuery.Where(u => query.APIKeyHints.Contains(u.APIKeyHint));
        }

        var records = usageQuery
            .OrderByDescending(u => u.Time)
            .ToList();

        var pricingMap = db.Queryable<LLMModelConfig>()
            .ToList()
            .Where(m => !string.IsNullOrWhiteSpace(m.Name))
            .GroupBy(m => m.Name)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var detailedRecords = records
            .Select(r => ToDetail(r, pricingMap))
            .ToList();

        var summary = new TokenUsageSummary
        {
            RecordCount = detailedRecords.Count,
            CallCount = detailedRecords.Count,
            PromptTokens = detailedRecords.Sum(r => r.PromptTokens),
            CachedPromptTokens = detailedRecords.Sum(r => r.CachedPromptTokens),
            CompletionTokens = detailedRecords.Sum(r => r.CompletionTokens),
            TotalTokens = detailedRecords.Sum(r => r.TotalTokens),
            EstimatedCost = detailedRecords.Sum(r => r.EstimatedCost),
            CacheRate = detailedRecords.Sum(r => r.PromptTokens) <= 0
                ? 0d
                : (double)detailedRecords.Sum(r => r.CachedPromptTokens) / detailedRecords.Sum(r => r.PromptTokens)
        };

        var purposeBreakdown = detailedRecords
            .GroupBy(r => r.Purpose)
            .Select(g => new TokenUsageBreakdown
            {
                Label = string.IsNullOrWhiteSpace(g.Key) ? "(未分类)" : g.Key,
                RecordCount = g.Count(),
                PromptTokens = g.Sum(r => r.PromptTokens),
                CachedPromptTokens = g.Sum(r => r.CachedPromptTokens),
                CompletionTokens = g.Sum(r => r.CompletionTokens),
                TotalTokens = g.Sum(r => r.TotalTokens),
                EstimatedCost = g.Sum(r => r.EstimatedCost)
            })
            .OrderByDescending(g => g.TotalTokens)
            .ThenBy(g => g.Label, StringComparer.Ordinal)
            .ToList();

        var modelBreakdown = detailedRecords
            .GroupBy(r => r.Model)
            .Select(g => new TokenUsageBreakdown
            {
                Label = string.IsNullOrWhiteSpace(g.Key) ? "(未分类)" : g.Key,
                RecordCount = g.Count(),
                PromptTokens = g.Sum(r => r.PromptTokens),
                CachedPromptTokens = g.Sum(r => r.CachedPromptTokens),
                CompletionTokens = g.Sum(r => r.CompletionTokens),
                TotalTokens = g.Sum(r => r.TotalTokens),
                EstimatedCost = g.Sum(r => r.EstimatedCost)
            })
            .OrderByDescending(g => g.TotalTokens)
            .ThenBy(g => g.Label, StringComparer.Ordinal)
            .ToList();

        var serviceBreakdown = detailedRecords
            .GroupBy(r => r.EndPoint)
            .Select(g => new TokenUsageBreakdown
            {
                Label = string.IsNullOrWhiteSpace(g.Key) ? "(未分类)" : g.Key,
                RecordCount = g.Count(),
                PromptTokens = g.Sum(r => r.PromptTokens),
                CachedPromptTokens = g.Sum(r => r.CachedPromptTokens),
                CompletionTokens = g.Sum(r => r.CompletionTokens),
                TotalTokens = g.Sum(r => r.TotalTokens),
                EstimatedCost = g.Sum(r => r.EstimatedCost)
            })
            .OrderByDescending(g => g.TotalTokens)
            .ThenBy(g => g.Label, StringComparer.Ordinal)
            .ToList();

        string bucketFormat = start.Date == end.Date ? "yyyy-MM-dd HH:00" : "yyyy-MM-dd";
        TimeSpan bucketSpan = start.Date == end.Date ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);
        var trend = detailedRecords
            .GroupBy(r => BucketTime(r.Time, bucketSpan))
            .OrderBy(g => g.Key)
            .Select(g => new TokenUsageTrendPoint
            {
                Bucket = g.Key,
                Label = bucketSpan == TimeSpan.FromHours(1) ? g.Key.ToString("HH:mm") : g.Key.ToString("MM-dd"),
                PromptTokens = g.Sum(r => r.PromptTokens),
                CachedPromptTokens = g.Sum(r => r.CachedPromptTokens),
                CompletionTokens = g.Sum(r => r.CompletionTokens),
                TotalTokens = g.Sum(r => r.TotalTokens),
                RecordCount = g.Count(),
                EstimatedCost = g.Sum(r => r.EstimatedCost)
            })
            .ToList();

        return new TokenUsageReport
        {
            Summary = summary,
            PurposeBreakdown = purposeBreakdown,
            ModelBreakdown = modelBreakdown,
            ServiceBreakdown = serviceBreakdown,
            Trend = trend,
            Records = detailedRecords.Take(Math.Max(1, query.DetailLimit)).ToList()
        };
    }

    private static TokenUsageDetail ToDetail(TokenUsage usage, IReadOnlyDictionary<string, LLMModelConfig> pricingMap)
    {
        pricingMap.TryGetValue(usage.Model, out var pricing);
        return new TokenUsageDetail
        {
            Id = usage.Id,
            Time = usage.Time,
            EndPoint = usage.EndPoint,
            Model = usage.Model,
            Purpose = usage.Purpose,
            APIKeyHint = usage.APIKeyHint,
            PromptTokens = usage.PromptTokens,
            CachedPromptTokens = usage.CachedPromptTokens,
            CompletionTokens = usage.CompletionTokens,
            TotalTokens = usage.TotalTokens,
            EstimatedCost = ComputeCost(usage, pricing)
        };
    }

    private static decimal ComputeCost(TokenUsage usage, LLMModelConfig? pricing)
    {
        if (pricing == null)
        {
            return 0m;
        }

        int billableInput = Math.Max(0, usage.PromptTokens - usage.CachedPromptTokens);
        return ((billableInput * pricing.InputPricePer1M)
              + (usage.CachedPromptTokens * pricing.CachePricePer1M)
              + (usage.CompletionTokens * pricing.OutputPricePer1M)) / 1_000_000m;
    }

    private static DateTime BucketTime(DateTime value, TimeSpan bucket)
    {
        if (bucket == TimeSpan.FromHours(1))
        {
            return new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, value.Kind);
        }

        return value.Date;
    }

    internal static void NotifyInserted(TokenUsage usage) => OnInserted?.Invoke(usage);
}

public class TokenUsageQuery
{
    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public List<string> EndPoints { get; set; } = [];

    public List<string> Purposes { get; set; } = [];

    public List<string> Models { get; set; } = [];

    public List<string> APIKeyHints { get; set; } = [];

    public int DetailLimit { get; set; } = 200;
}

public class TokenUsageSummary
{
    public int RecordCount { get; set; }

    public int CallCount { get; set; }

    public int PromptTokens { get; set; }

    public int CachedPromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int TotalTokens { get; set; }

    public decimal EstimatedCost { get; set; }

    public double CacheRate { get; set; }
}

public class TokenUsageBreakdown
{
    public string Label { get; set; } = string.Empty;

    public int RecordCount { get; set; }

    public int PromptTokens { get; set; }

    public int CachedPromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int TotalTokens { get; set; }

    public decimal EstimatedCost { get; set; }
}

public class TokenUsageDetail
{
    public int Id { get; set; }

    public DateTime Time { get; set; }

    public string EndPoint { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string APIKeyHint { get; set; } = string.Empty;

    public int PromptTokens { get; set; }

    public int CachedPromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int TotalTokens { get; set; }

    public decimal EstimatedCost { get; set; }
}

public class TokenUsageFilterOptions
{
    public List<string> EndPoints { get; set; } = [];

    public List<string> Purposes { get; set; } = [];

    public List<string> Models { get; set; } = [];

    public List<string> ApiKeyHints { get; set; } = [];
}

public class TokenUsageTrendPoint
{
    public DateTime Bucket { get; set; }

    public string Label { get; set; } = string.Empty;

    public int PromptTokens { get; set; }

    public int CachedPromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int TotalTokens { get; set; }

    public int RecordCount { get; set; }

    public decimal EstimatedCost { get; set; }
}

public class TokenUsageReport
{
    public TokenUsageSummary Summary { get; set; } = new();

    public List<TokenUsageBreakdown> PurposeBreakdown { get; set; } = [];

    public List<TokenUsageBreakdown> ModelBreakdown { get; set; } = [];

    public List<TokenUsageBreakdown> ServiceBreakdown { get; set; } = [];

    public List<TokenUsageTrendPoint> Trend { get; set; } = [];

    public List<TokenUsageDetail> Records { get; set; } = [];
}