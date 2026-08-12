using System.Text.Json;

namespace ChatGPTv3.ResponsesClient;

/// <summary>
/// Exception thrown when the OpenAI Responses API returns an error response.
/// </summary>
public class ResponsesApiException : Exception
{
    /// <summary>The HTTP status code of the error response.</summary>
    public int StatusCode { get; }

    /// <summary>The error type from the API response body (e.g., "invalid_request_error").</summary>
    public string? ErrorType { get; }

    /// <summary>The error code from the API response body.</summary>
    public string? ErrorCode { get; }

    public ResponsesApiException(int statusCode, string message, string? errorType = null, string? errorCode = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorType = errorType;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Creates a ResponsesApiException from an HTTP response.
    /// Error bodies look like: {"error":{"code":"...","message":"...","param":null,"type":"..."}}.
    /// </summary>
    public static async Task<ResponsesApiException> FromResponseAsync(HttpResponseMessage response)
    {
        var statusCode = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync();
        string? errorType = null;
        string? errorCode = null;
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

                if (error.TryGetProperty("code", out var c) && c.ValueKind != JsonValueKind.Null)
                {
                    errorCode = c.GetString();
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

        return new ResponsesApiException(statusCode, message, errorType, errorCode);
    }
}
