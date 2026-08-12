using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatGPTv3.AnthropicClient;

/// <summary>
/// A lightweight, zero-external-SDK Anthropic Messages API HTTP/SSE client,
/// styled after ChatGPTv3.OpenAIClient.OpenAiChatClient.
/// </summary>
public class AnthropicChatClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AnthropicChatClientOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _baseUrl;
    private bool _disposed;

    public AnthropicChatClient(AnthropicChatClientOptions options)
    {
        options.Validate();
        _options = options;

        _baseUrl = options.BaseUrl.TrimEnd('/');
        if (_baseUrl.EndsWith("/v1/messages", StringComparison.OrdinalIgnoreCase))
        {
            _baseUrl = _baseUrl[..^"/v1/messages".Length];
        }
        else if (_baseUrl.EndsWith("/messages", StringComparison.OrdinalIgnoreCase))
        {
            _baseUrl = _baseUrl[..^"/messages".Length];
        }

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(options.TimeoutMs)
        };
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", options.ApiKey);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", options.AnthropicVersion);
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

    /// <summary>Sends a non-streaming Messages API request.</summary>
    public async Task<AnthropicChatResponse> CompleteAsync(
        AnthropicChatRequest request,
        CancellationToken ct = default)
    {
        request.Stream = false;
        var response = await SendRequestAsync(request, HttpCompletionOption.ResponseContentRead, ct)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await AnthropicApiException.FromResponseAsync(response).ConfigureAwait(false);
        }

        var result = JsonSerializer.Deserialize<AnthropicChatResponse>(body, _jsonOptions);
        return result ?? throw new AnthropicApiException(0, "Failed to deserialize response: " + body);
    }

    /// <summary>
    /// Sends a streaming Messages API request and returns an async enumerable of updates.
    /// </summary>
    public async IAsyncEnumerable<AnthropicStreamingUpdate> StreamAsync(
        AnthropicChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        request.Stream = true;
        var response = await SendRequestAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await AnthropicApiException.FromResponseAsync(response).ConfigureAwait(false);
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await foreach (var update in AnthropicSseResponseParser.ParseStreamAsync(stream, ct).ConfigureAwait(false))
        {
            yield return update;
        }
    }

    // ─── Private Helpers ─────────────────────────────────────────

    private async Task<HttpResponseMessage> SendRequestAsync(
        AnthropicChatRequest request,
        HttpCompletionOption completionOption,
        CancellationToken ct)
    {
        var url = $"{_baseUrl}/v1/messages";
        if (string.IsNullOrEmpty(request.Model))
        {
            request.Model = _options.DefaultModel;
        }

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        return await _httpClient.SendAsync(httpRequest, completionOption, ct).ConfigureAwait(false);
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
