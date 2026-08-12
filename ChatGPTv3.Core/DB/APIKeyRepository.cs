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
        }

        // Upsert models by identity (Id) so a provider can have multiple models
        // with the same name but different settings. New rows (Id=0) are inserted,
        // existing rows are updated in place to keep PurposeBinding references valid.
        var existing = db.Queryable<LLMModelConfig>()
            .Where(x => x.APIKeyId == key.Id)
            .ToList();
        var existingById = existing.ToDictionary(x => x.Id);
        var incomingIds = new HashSet<int>(models.Select(x => x.Id).Where(id => id > 0));

        // Delete models no longer present (by Id, not name)
        var toDelete = existing.Where(x => !incomingIds.Contains(x.Id)).ToList();
        if (toDelete.Count > 0)
        {
            db.Deleteable(toDelete).ExecuteCommand();
        }

        // Insert new, update existing
        var toInsert = new List<LLMModelConfig>();
        foreach (var model in models)
        {
            model.APIKeyId = key.Id;
            if (model.Id > 0 && existingById.ContainsKey(model.Id))
            {
                db.Updateable(model).ExecuteCommand();
            }
            else
            {
                model.Id = 0;
                toInsert.Add(model);
            }
        }

        // Insert one-by-one so each row gets its identity back; the caller
        // (KeyManagementViewModel) then copies Ids back to its UI rows.
        foreach (var model in toInsert)
        {
            model.Id = db.Insertable(model).ExecuteReturnIdentity();
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
        using var db = SQLiteManager.GetInstance();

        // Clear all existing bindings
        db.Deleteable<PurposeBinding>().ExecuteCommand();

        // Insert new bindings
        var rows = new List<PurposeBinding>();
        foreach (var (purpose, items) in bindings)
        {
            var purposeName = purpose.Replace("ApiKeyId", "").Replace("APIKeyId", "");
            foreach (var item in items)
            {
                rows.Add(new PurposeBinding
                {
                    Purpose = purposeName,
                    APIKeyId = item.Id,
                    LLMModelConfigId = item.Model?.Id ?? 0
                });
            }
        }

        if (rows.Count > 0)
        {
            db.Insertable(rows).ExecuteCommand();
        }

        AppConfig.Init();
    }

    public static List<PurposeBinding> GetAllPurposeBindings()
    {
        using var db = SQLiteManager.GetInstance();
        return db.Queryable<PurposeBinding>().ToList();
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
