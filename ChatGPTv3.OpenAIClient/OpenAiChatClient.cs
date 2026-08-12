using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatGPTv3.OpenAIClient;

/// <summary>
/// A lightweight, zero-dependency OpenAI-compatible HTTP/SSE chat client.
/// Replaces the Microsoft.Extensions.AI + OpenAI SDK + Azure.AI.OpenAI stack.
/// </summary>
public class OpenAiChatClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiChatClientOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    // Original base URL (without /chat/completions suffix)
    private readonly string _baseUrl;

    public OpenAiChatClient(OpenAiChatClientOptions options)
    {
        options.Validate();
        _options = options;

        _baseUrl = options.BaseUrl.TrimEnd('/');
        // Strip known API suffix paths for clean URL building
        if (_baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            _baseUrl = _baseUrl[..^"/chat/completions".Length];
        }
        else if (_baseUrl.EndsWith("/embeddings", StringComparison.OrdinalIgnoreCase))
        {
            _baseUrl = _baseUrl[..^"/embeddings".Length];
        }

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(options.TimeoutMs)
        };
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", options.ApiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        foreach (var (key, value) in options.CustomHeaders)
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    // ─── Public API ──────────────────────────────────────────────

    /// <summary>
    /// Sends a non-streaming chat completion request.
    /// </summary>
    public async Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken ct = default)
    {
        request.Stream = false;
        var response = await SendRequestAsync(request, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await OpenAiApiException.FromResponseAsync(response).ConfigureAwait(false);
        }

        var result = JsonSerializer.Deserialize<ChatCompletionResponse>(body, _jsonOptions);
        return result ?? throw new OpenAiApiException(0, "Failed to deserialize response: " + body);
    }

    /// <summary>
    /// Sends a streaming chat completion request. Returns an async enumerable of updates.
    /// Usage chunks (stream_options.include_usage) are included in the stream automatically.
    /// </summary>
    public async IAsyncEnumerable<StreamingUpdate> StreamAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        request.Stream = true;
        // Enable usage tracking in stream (standard OpenAI practice)
        request.EnableStreamUsage();

        var response = await SendRequestAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await OpenAiApiException.FromResponseAsync(response).ConfigureAwait(false);
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await foreach (var update in SseResponseParser.ParseStreamAsync(stream, ct).ConfigureAwait(false))
        {
            yield return update;
        }
    }

    /// <summary>
    /// Gets text embeddings for the given input.
    /// </summary>
    /// <param name="input">The text to embed.</param>
    /// <param name="model">The embedding model name (e.g., "text-embedding-ada-002").</param>
    /// <param name="dimensions">Optional dimensions parameter for the embedding.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Float array of embedding values, plus token usage if available.</returns>
    public async Task<(float[] embedding, TokenUsageInfo? usage)> GetEmbeddingsAsync(
        string input,
        string model,
        int? dimensions = null,
        CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/embeddings";
        object payload = dimensions.HasValue
            ? new { input, model, dimensions = dimensions.Value }
            : new { input, model };

        var content = new StringContent(
            JsonSerializer.Serialize(payload, _jsonOptions),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(url, content, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await OpenAiApiException.FromResponseAsync(response).ConfigureAwait(false);
        }

        using var doc = JsonDocument.Parse(body);
        var embedding = doc.RootElement
            .GetProperty("data")[0]
            .GetProperty("embedding");

        var result = new List<float>();
        foreach (var item in embedding.EnumerateArray())
        {
            result.Add(item.GetSingle());
        }

        TokenUsageInfo? usage = null;
        if (doc.RootElement.TryGetProperty("usage", out var usageEl))
        {
            usage = new TokenUsageInfo
            {
                PromptTokens = usageEl.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0,
                CompletionTokens = 0,
                TotalTokens = usageEl.TryGetProperty("total_tokens", out var tt) ? tt.GetInt32() : 0
            };
        }

        return (result.ToArray(), usage);
    }

    // ─── Private Helpers ─────────────────────────────────────────

    private async Task<HttpResponseMessage> SendRequestAsync(
        ChatCompletionRequest request,
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead,
        CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/chat/completions";
        var model = string.IsNullOrEmpty(request.Model) ? _options.DefaultModel : request.Model;

        // Build the request body with proper message serialization
        var body = BuildRequestBody(request, model);
        var json = JsonSerializer.Serialize(body, _jsonOptions);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        return await _httpClient.SendAsync(httpRequest, completionOption, ct).ConfigureAwait(false);
    }

    private object BuildRequestBody(ChatCompletionRequest request, string model)
    {
        var serializedMessages = request.SerializeMessages();

        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = serializedMessages,
            ["stream"] = request.Stream
        };

        if (request.MaxTokens.HasValue)
        {
            body["max_completion_tokens"] = request.MaxTokens.Value;
        }

        if (request.Temperature.HasValue)
        {
            body["temperature"] = request.Temperature.Value;
        }

        if (request.Tools != null)
        {
            body["tools"] = request.Tools;
        }

        if (request.ToolChoice != null)
        {
            body["tool_choice"] = request.ToolChoice;
        }

        if (request.Thinking != null)
        {
            body["thinking"] = request.Thinking;
        }

        if (!string.IsNullOrEmpty(request.ReasoningEffort))
        {
            body["reasoning_effort"] = request.ReasoningEffort;
        }

        if (request.ResponseFormat != null)
        {
            body["response_format"] = request.ResponseFormat;
        }

        if (request.TopP.HasValue)
        {
            body["top_p"] = request.TopP.Value;
        }

        if (request.FrequencyPenalty.HasValue)
        {
            body["frequency_penalty"] = request.FrequencyPenalty.Value;
        }

        if (request.PresencePenalty.HasValue)
        {
            body["presence_penalty"] = request.PresencePenalty.Value;
        }

        if (request.Stop != null)
        {
            body["stop"] = request.Stop;
        }

        if (request.Seed.HasValue)
        {
            body["seed"] = request.Seed.Value;
        }

        if (request.StreamOptions != null)
        {
            body["stream_options"] = request.StreamOptions;
        }

        return body;
    }

    // ─── IDisposable ─────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _httpClient.Dispose();
    }
}
