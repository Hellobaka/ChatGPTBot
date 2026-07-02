using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Api;

public static class EmbeddingService
{
    // TODO: 添加图片Embedding获取支持
    public static async Task<float[]?> GetEmbeddingsAsync(
        string text, CancellationToken ct = default)
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

        // Key's model takes priority, then config, then fallback
        var model = key.Model?.Name
            ?? (string.IsNullOrEmpty(AppConfig.EmbeddingModelName) ? null : AppConfig.EmbeddingModelName)
            ?? "text-embedding-ada-002";

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
            var result = await client.GetEmbeddingsAsync(text, model, ct);

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