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

            // Update API key total consumption
            var keyHint = MaskKey(apiKey);
            db.Updateable<APIKey>()
                .SetColumns(it => it.TotalTokens == it.TotalTokens + usage.TotalTokens)
                .Where(it => it.Key != null && it.Key.Contains(keyHint))
                .ExecuteCommand();
        }
        catch (Exception ex)
        {
            // Usage tracking failure should not crash the chat pipeline
            System.Diagnostics.Debug.WriteLine($"UsageTracker error: {ex.Message}");
        }
    }

    private static string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length <= 8)
            return "***";
        return key.Substring(0, 4) + "***" + key.Substring(key.Length - 4);
    }
}
