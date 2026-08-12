using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatGPTv3.ResponsesClient;

/// <summary>
/// A lightweight, zero-external-SDK OpenAI Responses API HTTP/SSE client,
/// styled after ChatGPTv3.OpenAIClient.OpenAiChatClient.
/// </summary>
public class ResponsesChatClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ResponsesChatClientOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _baseUrl;
    private bool _disposed;

    public ResponsesChatClient(ResponsesChatClientOptions options)
    {
        options.Validate();
        _options = options;

        _baseUrl = options.BaseUrl.TrimEnd('/');
        if (_baseUrl.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
        {
            _baseUrl = _baseUrl[..^"/responses".Length];
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

    /// <summary>Sends a non-streaming Responses API request.</summary>
    public async Task<ResponsesCreateResponse> CompleteAsync(
        ResponsesCreateRequest request,
        CancellationToken ct = default)
    {
        request.Stream = false;
        var response = await SendRequestAsync(request, HttpCompletionOption.ResponseContentRead, ct)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await ResponsesApiException.FromResponseAsync(response).ConfigureAwait(false);
        }

        var result = JsonSerializer.Deserialize<ResponsesCreateResponse>(body, _jsonOptions);
        return result ?? throw new ResponsesApiException(0, "Failed to deserialize response: " + body);
    }

    /// <summary>
    /// Sends a streaming Responses API request and returns an async enumerable of updates.
    /// Usage arrives in the response.completed event (there is no stream_options.include_usage
    /// equivalent in the Responses protocol).
    /// </summary>
    public async IAsyncEnumerable<ResponsesStreamingUpdate> StreamAsync(
        ResponsesCreateRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        request.Stream = true;
        var response = await SendRequestAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw await ResponsesApiException.FromResponseAsync(response).ConfigureAwait(false);
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await foreach (var update in ResponsesSseResponseParser.ParseStreamAsync(stream, ct).ConfigureAwait(false))
        {
            yield return update;
        }
    }

    // ─── Private Helpers ─────────────────────────────────────────

    private async Task<HttpResponseMessage> SendRequestAsync(
        ResponsesCreateRequest request,
        HttpCompletionOption completionOption,
        CancellationToken ct)
    {
        var url = $"{_baseUrl}/responses";
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
