using ChatGPTv3.Core.Config;
using SqlSugar;

namespace ChatGPTv3.Core.DB;

public static class APIKeyRepository
{
    public static List<APIKey> GetAllWithModels()
    {
        using var db = SQLiteManager.GetInstance();
        return db.Queryable<APIKey>()
            .Includes(x => x.AvailableModels)
            .OrderBy(x => x.Id)
            .ToList();
    }

    public static APIKey Save(APIKey key)
    {
        using var db = SQLiteManager.GetInstance();

        var models = key.AvailableModels ?? [];
        key.AvailableModels = null;

        if (key.Id == 0)
        {
            key.Id = db.Insertable(key).ExecuteReturnIdentity();
        }
        else
        {
            db.Updateable(key).ExecuteCommand();
            db.Deleteable<LLMModelConfig>().Where(x => x.APIKeyId == key.Id).ExecuteCommand();
        }

        foreach (var model in models)
        {
            model.Id = 0;
            model.APIKeyId = key.Id;
        }

        if (models.Count > 0)
        {
            db.Insertable(models).ExecuteCommand();
        }

        key.AvailableModels = models;
        return key;
    }

    public static void Delete(int id)
    {
        using var db = SQLiteManager.GetInstance();
        db.Deleteable<LLMModelConfig>().Where(x => x.APIKeyId == id).ExecuteCommand();
        db.Deleteable<APIKey>().Where(x => x.Id == id).ExecuteCommand();
    }

    public static string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length <= 8)
        {
            return "***";
        }

        return key[..4] + "***" + key[^4..];
    }

    public static List<ModelSpendSummary> GetModelSpendSummaries(APIKey key)
    {
        using var db = SQLiteManager.GetInstance();

        var masked = MaskKey(key.Key);
        var usages = db.Queryable<TokenUsage>()
            .Where(x => x.EndPoint == key.EndPoint && x.APIKeyHint == masked)
            .ToList();

        var pricingMap = (key.AvailableModels ?? [])
            .GroupBy(x => x.Name)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        return usages
            .GroupBy(x => x.Model)
            .Select(g =>
            {
                pricingMap.TryGetValue(g.Key, out var model);
                var prompt = g.Sum(x => x.PromptTokens);
                var cached = g.Sum(x => x.CachedPromptTokens);
                var completion = g.Sum(x => x.CompletionTokens);
                var cost = model == null
                    ? 0m
                    : ((Math.Max(0, prompt - cached) * model.InputPricePer1M)
                       + (cached * model.CachePricePer1M)
                       + (completion * model.OutputPricePer1M)) / 1_000_000m;

                return new ModelSpendSummary
                {
                    ModelName = g.Key,
                    CallCount = g.Count(),
                    PromptTokens = prompt,
                    CachedPromptTokens = cached,
                    CompletionTokens = completion,
                    TotalTokens = g.Sum(x => x.TotalTokens),
                    EstimatedConsume = cost
                };
            })
            .OrderByDescending(x => x.TotalTokens)
            .ToList();
    }

    public static void SavePurposeBindings(Dictionary<string, List<APIKeyPurpose>> bindings)
    {
        ConfigManager.SetConfig("ChatAPIKeyId", bindings["ChatAPIKeyId"]);
        ConfigManager.SetConfig("ReplyAPIKeyId", bindings["ReplyAPIKeyId"]);
        ConfigManager.SetConfig("SplitterApiKeyId", bindings["SplitterApiKeyId"]);
        ConfigManager.SetConfig("ImageDescriberApiKeyId", bindings["ImageDescriberApiKeyId"]);
        ConfigManager.SetConfig("EmbeddingApiKeyId", bindings["EmbeddingApiKeyId"]);
        ConfigManager.SetConfig("RerankApiKeyId", bindings["RerankApiKeyId"]);
        ConfigManager.SetConfig("SummarizerApiKeyId", bindings["SummarizerApiKeyId"]);
        ConfigManager.SetConfig("DiaryAPIKeyId", bindings["DiaryAPIKeyId"]);
        AppConfig.Init();
    }
}

public class ModelSpendSummary
{
    public string ModelName { get; set; } = string.Empty;

    public int CallCount { get; set; }

    public int PromptTokens { get; set; }

    public int CachedPromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int TotalTokens { get; set; }

    public decimal EstimatedConsume { get; set; }
}