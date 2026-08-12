using System.Text.Json;

namespace ChatGPTv3.AnthropicClient;

/// <summary>
/// Exception thrown when the Anthropic Messages API returns an error response.
/// </summary>
public class AnthropicApiException : Exception
{
    /// <summary>The HTTP status code of the error response.</summary>
    public int StatusCode { get; }

    /// <summary>The error type from the API response body (e.g., "invalid_request_error").</summary>
    public string? ErrorType { get; }

    public AnthropicApiException(int statusCode, string message, string? errorType = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorType = errorType;
    }

    /// <summary>
    /// Creates an AnthropicApiException from an HTTP response.
    /// Anthropic error bodies look like: {"type":"error","error":{"type":"...","message":"..."}}.
    /// </summary>
    public static async Task<AnthropicApiException> FromResponseAsync(HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync();
        string? errorType = null;
        string message;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                message = error.TryGetProperty("message", out var msg)
                    ? msg.GetString() ?? body
                    : body;
                if (error.TryGetProperty("type", out var t))
                {
                    errorType = t.GetString();
                }
            }
            else
            {
                message = body;
            }
        }
        catch
        {
            message = body;
        }

        return new AnthropicApiException(statusCode, message, errorType);
    }
}
