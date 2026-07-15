using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Api;

public static class EmbeddingService
{
    public static async Task<float[]?> GetEmbeddingsAsync(
        string text,
        int? dimensions = null,
        CancellationToken ct = default)
    {
        // Prefer dedicated embedding key, fall back to chat key
        var keys = AppConfig.EmbeddingApiKeyId;
        if (keys.Count == 0)
        {
            keys = AppConfig.ChatAPIKeyId;
            if (keys.Count == 0)
            {
                CommonHelper.LogWarning?.Invoke("Embedding", "无可用 API Key");
                return null;
            }
        }

        var key = keys.OrderBy(_ => Guid.NewGuid()).FirstOrDefault();
        if (key?.Key == null)
        {
            return null;
        }

        // Model must come from the bound APIKey; no built-in fallback
        if (key.Model == null)
        {
            CommonHelper.LogError?.Invoke("Embedding", "No valid Embedding model configured on key");
            return null;
        }
        var model = key.Model.Name;

        try
        {
            CommonHelper.LogInfo?.Invoke("Embedding",
                $"请求: endpoint={key.Key.EndPoint} model={model} text={text[..Math.Min(text.Length, 30)]}...");

            var options = new OpenAiChatClientOptions
            {
                BaseUrl = key.Key.EndPoint,
                ApiKey = key.Key.Key,
                TimeoutMs = AppConfig.EmbeddingTimeout
            };

            using var client = new OpenAiChatClient(options);
            var result = await client.GetEmbeddingsAsync(text, model, dimensions, ct);

            CommonHelper.LogInfo?.Invoke("Embedding", $"成功: dims={result?.Length}");
            return result;
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("Embedding", ex.Message);
            return null;
        }
    }
}