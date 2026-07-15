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
        return await InsertWithIdAsync(text, collectionName, Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Insert a point with a custom ID (e.g. MD5 hash for idempotent upserts).
    /// </summary>
    public async Task<bool> InsertWithIdAsync(string text, string collectionName, string pointId)
    {
        return await InsertWithPayloadAsync(text, collectionName, pointId, null);
    }

    /// <summary>
    /// Insert a point with a custom ID and optional source metadata in payload.
    /// </summary>
    public async Task<bool> InsertWithSourceAsync(string text, string collectionName, string source)
    {
        var pointId = Guid.NewGuid().ToString("N");
        var payload = new Dictionary<string, object?>
        {
            ["source"] = source,
            ["timestamp"] = DateTime.Now.ToString("O")
        };
        return await UpsertPointAsync(collectionName, pointId, text, payload);
    }

    private async Task<bool> InsertWithPayloadAsync(string text, string collectionName, string pointId, Dictionary<string, object?>? extraPayload)
    {
        var payload = new Dictionary<string, object?>
        {
            ["text"] = text,
            ["timestamp"] = DateTime.Now.ToString("O")
        };
        if (extraPayload != null)
        {
            foreach (var kv in extraPayload)
            {
                payload[kv.Key] = kv.Value;
            }
        }
        return await UpsertPointAsync(collectionName, pointId, text, payload);
    }

    private async Task<bool> UpsertPointAsync(string collectionName, string pointId, string text, Dictionary<string, object?> payload)
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

            payload["text"] = text;

            var request = new
            {
                points = new[]
                {
                    new
                    {
                        id = pointId,
                        vector = embedding,
                        payload
                    }
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
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

    // ── Scroll / Count / Delete ─────────────────────────────

    /// <summary>
    /// Scroll through points with optional payload filter and pagination.
    /// </summary>
    public async Task<List<(string id, string text, string source, DateTime time)>> ScrollAsync(
        string collectionName,
        string? sourceFilter = null,
        int limit = 50,
        int offset = 0)
    {
        try
        {
            object? filter = null;
            if (!string.IsNullOrEmpty(sourceFilter))
            {
                filter = new
                {
                    must = new[]
                    {
                        new { key = "source", match = new { value = sourceFilter } }
                    }
                };
            }

            var request = new { filter, limit, offset, with_payload = true, with_vector = false };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var r = await _http.PostAsync($"{_baseUrl}/collections/{collectionName}/points/scroll", content);
            var body = await r.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);
            var results = new List<(string, string, string, DateTime)>();
            var points = doc.RootElement.GetProperty("result").GetProperty("points");
            foreach (var point in points.EnumerateArray())
            {
                var id = point.GetProperty("id").GetString()!;
                var payload = point.GetProperty("payload");
                var text = payload.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                var source = payload.TryGetProperty("source", out var s) ? s.GetString() ?? "" : "";
                var time = payload.TryGetProperty("timestamp", out var ts)
                    && DateTime.TryParse(ts.GetString(), out var t2) ? t2 : DateTime.MinValue;
                results.Add((id, text, source, time));
            }
            return results;
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("Qdrant", $"Scroll: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Count points grouped by source field.
    /// </summary>
    public async Task<Dictionary<string, int>> CountBySourceAsync(string collectionName)
    {
        try
        {
            // Qdrant doesn't support GROUP BY, so we scroll all and count client-side
            var all = await ScrollAsync(collectionName, limit: 10000, offset: 0);
            return all.GroupBy(p => p.source)
                      .ToDictionary(g => g.Key, g => g.Count());
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("Qdrant", $"CountBySource: {ex.Message}");
            return new Dictionary<string, int>();
        }
    }

    /// <summary>
    /// Delete a single point by ID.
    /// </summary>
    public async Task<bool> DeleteByIdAsync(string collectionName, string pointId)
    {
        return await DeletePointsAsync(collectionName, new[] { pointId });
    }

    /// <summary>
    /// Delete all points matching a source filter.
    /// </summary>
    public async Task<bool> DeleteBySourceAsync(string collectionName, string source)
    {
        try
        {
            var filter = new
            {
                must = new[]
                {
                    new { key = "source", match = new { value = source } }
                }
            };

            var request = new { filter };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var r = await _http.PostAsync($"{_baseUrl}/collections/{collectionName}/points/delete", content);
            return r.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("Qdrant", $"DeleteBySource: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> DeletePointsAsync(string collectionName, IEnumerable<string> pointIds)
    {
        try
        {
            var request = new
            {
                points = pointIds.ToArray()
            };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var r = await _http.PostAsync($"{_baseUrl}/collections/{collectionName}/points/delete", content);
            return r.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("Qdrant", $"DeletePoints: {ex.Message}");
            return false;
        }
    }

    // ── Embedding ────────────────────────────────────────

    private static async Task<float[]?> GetEmbeddingAsync(string text)
    {
        // Use EmbeddingService — may call OpenAI or configured embedding API
        return await Api.EmbeddingService.GetEmbeddingsAsync(text, AppConfig.MemoryDimensions);
    }
}