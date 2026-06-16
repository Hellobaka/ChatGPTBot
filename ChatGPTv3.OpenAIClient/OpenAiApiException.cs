namespace ChatGPTv3.OpenAIClient;

/// <summary>
/// Exception thrown when the OpenAI-compatible API returns an error response.
/// </summary>
public class OpenAiApiException : Exception
{
    /// <summary>
    /// The HTTP status code of the error response.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// The error type from the API response body (e.g., "invalid_request_error").
    /// </summary>
    public string? ErrorType { get; }

    /// <summary>
    /// The error code from the API response body (e.g., "context_length_exceeded").
    /// </summary>
    public string? ErrorCode { get; }

    public OpenAiApiException(int statusCode, string message, string? errorType = null, string? errorCode = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorType = errorType;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Creates an OpenAiApiException from an HTTP response.
    /// </summary>
    public static async Task<OpenAiApiException> FromResponseAsync(HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync();
        string? errorType = null;
        string? errorCode = null;
        string message;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("message", out var msg))
                    message = msg.GetString() ?? body;
                else
                    message = body;

                if (error.TryGetProperty("type", out var t))
                    errorType = t.GetString();
                if (error.TryGetProperty("code", out var c))
                    errorCode = c.GetString();
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

        return new OpenAiApiException(statusCode, message, errorType, errorCode);
    }

    /// <summary>
    /// Whether this error is transient (may succeed on retry).
    /// </summary>
    public bool IsTransient()
    {
        return StatusCode >= 500 || StatusCode == 429;
    }

    /// <summary>
    /// Whether this error is due to context length exceeded.
    /// </summary>
    public bool IsContextLengthExceeded()
    {
        return ErrorCode == "context_length_exceeded" ||
               (Message?.Contains("context_length_exceeded") ?? false) ||
               (Message?.Contains("maximum context length") ?? false);
    }
}
