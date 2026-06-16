namespace ChatGPTv3.HttpSse;

/// <summary>
/// Configuration options for the OpenAiChatClient.
/// </summary>
public class OpenAiChatClientOptions
{
    /// <summary>
    /// The base URL of the OpenAI-compatible API endpoint.
    /// If the URL ends with "/chat/completions", it will be stripped automatically.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>
    /// The API key for authentication (Bearer token).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The default model name to use for chat completions.
    /// Can be overridden per-request via ChatCompletionRequest.Model.
    /// </summary>
    public string DefaultModel { get; set; } = "gpt-4o";

    /// <summary>
    /// HTTP timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Custom headers to include with every request.
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = [];

    /// <summary>
    /// Validates that required options are set.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
            throw new ArgumentException("BaseUrl is required");
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new ArgumentException("ApiKey is required");
    }
}
