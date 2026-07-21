using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;
using System.Text;
using System.Text.Json;

namespace ChatGPTv3.Core.Api;

/// <summary>
/// Rerank API client. Supports Alibaba Cloud (Bailian) format.
/// Re-ranks search results for memory/knowledge relevance.
/// </summary>
public static class RerankService
{
    private static readonly HttpClient _http = new();

    public static async Task<List<(int index, float score)>> RerankAsync(
        string query,
        List<string> documents,
        CancellationToken ct = default)
    {
        if (!AppConfig.EnableRerank || documents.Count == 0)
        {
            return [];
        }

        try
        {
            var keyPurpose = AppConfig.RerankApiKeyId
                .OrderBy(_ => Guid.NewGuid())
                .FirstOrDefault();

            if (keyPurpose?.Key == null || keyPurpose.Model == null)
            {
                CommonHelper.LogError?.Invoke("Rerank", "No valid Rerank API key configured");
                return [];
            }

            var endpoint = keyPurpose.Key.EndPoint;
            var model = keyPurpose.Model.Name;
            var useTencentSign = keyPurpose.Key.UseTencentSign;

            var payload = new
            {
                Query = query,
                Docs = documents,
                Model = model
            };

            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            // Add TC3 signature headers (Tencent Cloud only)
            if (useTencentSign)
            {
                var headers = TencentSign.BuildHeaders("lkeap", "lkeap.tencentcloudapi.com",
                    "ap-guangzhou", "RunRerank", "2024-05-22", json);
                foreach (var (k, v) in headers)
                {
                    request.Headers.TryAddWithoutValidation(k, v);
                }
            }

            using var cts = new CancellationTokenSource(AppConfig.RerankTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);
            var response = await _http.SendAsync(request, linked.Token);

            if (!response.IsSuccessStatusCode)
            {
                CommonHelper.LogError?.Invoke("Rerank", $"HTTP {(int)response.StatusCode}");
                return [];
            }

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            // Parse results — try Alibaba format first, then Tencent
            var scored = new List<(int index, float score)>();
            JsonElement? responseEl = null;

            if (doc.RootElement.TryGetProperty("output", out var outputEl)
                && outputEl.TryGetProperty("results", out var alibabaResults))
            {
                // Alibaba Bailian format: [{index, relevance_score, document}]
                foreach (var item in alibabaResults.EnumerateArray())
                {
                    var index = item.TryGetProperty("index", out var idxEl) ? idxEl.GetInt32() : scored.Count;
                    var score = item.TryGetProperty("relevance_score", out var scoreEl)
                        ? scoreEl.GetSingle()
                        : 0f;
                    scored.Add((index, score));
                }
            }
            else if (doc.RootElement.TryGetProperty("Response", out var respEl))
            {
                responseEl = respEl;
                // Tencent Cloud format: {ScoreList: [score, ...]}
                // Scores are in the same order as input documents (index = position)
                if (respEl.TryGetProperty("ScoreList", out var scoreList))
                {
                    int idx = 0;
                    foreach (var scoreEl in scoreList.EnumerateArray())
                    {
                        scored.Add((idx++, scoreEl.GetSingle()));
                    }
                }
            }
            else
            {
                CommonHelper.LogError?.Invoke("Rerank", "无法解析 Rerank 响应格式");
                return [];
            }

            // Track token usage
            TokenUsageInfo? usage = null;

            // Alibaba format: usage.total_tokens (top-level)
            if (doc.RootElement.TryGetProperty("usage", out var usageEl)
                && usageEl.TryGetProperty("total_tokens", out var ttEl))
            {
                usage = new TokenUsageInfo
                {
                    PromptTokens = ttEl.GetInt32(),
                    CompletionTokens = 0,
                    TotalTokens = ttEl.GetInt32()
                };
            }
            // Tencent format: Response.Usage.TotalTokens
            else if (responseEl.HasValue
                     && responseEl.Value.TryGetProperty("Usage", out var usageEl2)
                     && usageEl2.TryGetProperty("TotalTokens", out var ttEl2))
            {
                usage = new TokenUsageInfo
                {
                    PromptTokens = ttEl2.GetInt32(),
                    CompletionTokens = 0,
                    TotalTokens = ttEl2.GetInt32()
                };
            }

            if (usage != null)
            {
                UsageTracker.TrackUsage(endpoint, model, "Rerank", usage, keyPurpose.Key.Key);
            }

            return scored.OrderByDescending(x => x.score).ToList();
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("Rerank", ex.Message);
            return [];
        }
    }
}