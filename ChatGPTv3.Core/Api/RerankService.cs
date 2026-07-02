using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.Utilities;
using System.Text;
using System.Text.Json;

namespace ChatGPTv3.Core.Api;

/// <summary>
/// Tencent Cloud Rerank API client. Re-ranks search results for memory/knowledge relevance.
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

            var endpoint = keyPurpose?.Key?.EndPoint ?? AppConfig.RerankUrl;
            var model = keyPurpose?.Model?.Name ?? AppConfig.RerankModelName;
            var useTencentSign = keyPurpose?.Key?.UseTencentSign ?? AppConfig.EnableTencentSign;

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

            // Add TC3 signature headers
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
            var results = doc.RootElement.GetProperty("Response").GetProperty("Results");

            var scored = new List<(int index, float score)>();
            foreach (var item in results.EnumerateArray())
            {
                scored.Add((item.GetProperty("Index").GetInt32(),
                            item.GetProperty("RelevanceScore").GetSingle()));
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