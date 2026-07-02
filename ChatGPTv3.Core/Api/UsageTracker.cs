using ChatGPTv3.Core.DB;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Api;

/// <summary>
/// Records token usage to the database after each API call.
/// </summary>
public static class UsageTracker
{
    public static void TrackUsage(
        string baseUrl,
        string model,
        string purpose,
        TokenUsageInfo usage,
        string apiKey)
    {
        try
        {
            using var db = SQLiteManager.GetInstance();
            var record = new TokenUsage
            {
                EndPoint = baseUrl,
                Model = model,
                Purpose = purpose,
                APIKeyHint = MaskKey(apiKey),
                PromptTokens = usage.PromptTokens,
                CachedPromptTokens = usage.GetCachedPromptTokens(),
                CompletionTokens = usage.CompletionTokens,
                TotalTokens = usage.TotalTokens,
                Time = DateTime.Now
            };

            db.Insertable(record).ExecuteCommand();
            TokenUsage.NotifyInserted(record);

            // Update API key total tokens + cumulative cost
            var keyHint = MaskKey(apiKey);
            var cost = ComputeCost(db, model, usage);

            db.Updateable<APIKey>()
                .SetColumns(it => new APIKey
                {
                    TotalTokens = it.TotalTokens + usage.TotalTokens,
                    TotalConsume = it.TotalConsume + cost
                })
                .Where(it => it.Key != null && it.Key.Contains(keyHint))
                .ExecuteCommand();
        }
        catch (Exception ex)
        {
            // Usage tracking failure should not crash the chat pipeline
            System.Diagnostics.Debug.WriteLine($"UsageTracker error: {ex.Message}");
        }
    }

    /// <summary>
    /// Computes the RMB cost of a single API call based on the model's pricing.
    /// cost = (prompt - cached) × inputPrice + cached × cachePrice + completion × outputPrice, all per 1M tokens.
    /// </summary>
    private static decimal ComputeCost(SqlSugar.SqlSugarClient db, string model, TokenUsageInfo usage)
    {
        try
        {
            var pricing = db.Queryable<LLMModelConfig>()
                .First(m => m.Name == model);
            if (pricing == null)
            {
                return 0m;
            }

            int cached = usage.GetCachedPromptTokens();
            int billableInput = Math.Max(0, usage.PromptTokens - cached);
            decimal cost = ((billableInput * pricing.InputPricePer1M)
                          + (cached * pricing.CachePricePer1M)
                          + (usage.CompletionTokens * pricing.OutputPricePer1M)) / 1_000_000m;
            return cost;
        }
        catch
        {
            return 0m;
        }
    }

    private static string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length <= 8)
        {
            return "***";
        }

        return key.Substring(0, 4) + "***" + key.Substring(key.Length - 4);
    }
}