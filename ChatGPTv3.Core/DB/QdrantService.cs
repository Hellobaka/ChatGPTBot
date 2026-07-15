using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.Utilities;
using System.Text;
using System.Text.Json;

namespace ChatGPTv3.Core.DB;

/// <summary>
/// Qdrant vector database client for long-term memory and knowledge base.
/// Uses Qdrant REST API (v1.x).
/// </summary>
public class QdrantService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public const string KnowledgeCollectionName = "knowledge_base";
    public const string ImageCollectionName = "picture_descriptions";

    public QdrantService()
    {
        _baseUrl = $"http://{AppConfig.QdrantHost}:{AppConfig.QdrantPort}";
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        if (!string.IsNullOrEmpty(AppConfig.QdrantAPIKey))
        {
            _http.DefaultRequestHeaders.Add("api-key", AppConfig.QdrantAPIKey);
        }
    }

    // ── Collections ──────────────────────────────────────

    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            var r = await _http.GetAsync($"{_baseUrl}/collections");
            return r.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> GetCollectionsAsync()
    {
        try
        {
            var r = await _http.GetStringAsync($"{_baseUrl}/collections");
            using var doc = JsonDocument.Parse(r);
            return doc.RootElement.GetProperty("result").GetProperty("collections")
                .EnumerateArray().Select(c => c.GetProperty("name").GetString()!).ToList();
        }
        catch
        {
            return [];
        }
    }

    public async Task<bool> CreateCollectionAsync(string name)
    {
        try
        {
            var body = JsonSerializer.Serialize(new
            {
                vectors = new { size = AppConfig.MemoryDimensions, distance = "Cosine" }
            });
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var r = await _http.PutAsync($"{_baseUrl}/collections/{name}", content);
            return r.IsSuccessStatusCode || r.StatusCode == System.Net.HttpStatusCode.Conflict;
        }
        catch
        {
            return false;
        }
    }

    // ── Points ───────────────────────────────────────────

    public async Task<bool> InsertAsync(string text, string collectionName)
    {
        return await InsertWithIdAsync(text, collectionName, Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Insert a point with a custom ID (e.g. MD5 hash for idempotent upserts).
    /// </summary>
    public async Task<bool> InsertWithIdAsync(string text, string collectionName, string pointId)
    {
        try
        {
            CommonHelper.LogInfo?.Invoke("Qdrant", $"插入向量: collection={collectionName} id={pointId} text={text[..Math.Min(text.Length, 50)]}...");
            var embedding = await GetEmbeddingAsync(text);
            if (embedding == null)
            {
                CommonHelper.LogWarning?.Invoke("Qdrant", "Embedding 失败");
                return false;
            }

            var payload = new Dictionary<string, object>
            {
                ["points"] = new[]
                {
                    new
                    {
                        id = pointId,
                        vector = embedding,
                        payload = new { text, timestamp = DateTime.Now.ToString("O") }
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            // Use PUT for upsert (same endpoint, id ensures idempotency)
            var r = await _http.PutAsync($"{_baseUrl}/collections/{collectionName}/points", content);
            CommonHelper.DebugLog("Qdrant", $"插入结果: {(r.IsSuccessStatusCode ? "成功" : $"失败 HTTP{(int)r.StatusCode}")}");
            return r.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("Qdrant", $"Insert: {ex.Message}");
            return false;
        }
    }

    public async Task<List<(string id, string text, DateTime time, float score)>> SearchAsync(
        string query, string collectionName, int limit = 5)
    {
        try
        {
            var embedding = await GetEmbeddingAsync(query);
            if (embedding == null)
            {
                return [];
            }

            var body = JsonSerializer.Serialize(new
            {
                vector = embedding,
                limit,
                with_payload = true,
                score_threshold = AppConfig.MinMemorySimilarity
            });

            var content = new StringContent(body, Encoding.UTF8, "application/json");
            CommonHelper.LogInfo?.Invoke("Qdrant", $"搜索向量: collection={collectionName}, query={query[..Math.Min(query.Length, 50)]}...");
            var r = await _http.PostAsync($"{_baseUrl}/collections/{collectionName}/points/search", content);
            var json = await r.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var results = new List<(string, string, DateTime, float)>();
            foreach (var point in doc.RootElement.GetProperty("result").EnumerateArray())
            {
                var id = point.GetProperty("id").GetString()!;
                var score = point.GetProperty("score").GetSingle();
                var payload = point.GetProperty("payload");
                var record = payload.GetProperty("text").GetString() ?? "";
                var time = DateTime.TryParse(payload.GetProperty("timestamp").GetString(), out var t)
                    ? t : DateTime.MinValue;
                results.Add((id, record, time, score));
            }
            return results;
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("Qdrant", $"Search: {ex.Message}");
            return [];
        }
    }

    // ── Embedding ────────────────────────────────────────

    private static async Task<float[]?> GetEmbeddingAsync(string text)
    {
        // Use EmbeddingService — may call OpenAI or configured embedding API
        return await Api.EmbeddingService.GetEmbeddingsAsync(text, AppConfig.MemoryDimensions);
    }
}