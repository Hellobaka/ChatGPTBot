namespace ChatGPTv3.AnthropicClient;

/// <summary>
/// Configuration options for the AnthropicChatClient (Messages API).
/// </summary>
public class AnthropicChatClientOptions
{
    /// <summary>
    /// The base URL of the Anthropic API endpoint.
    /// If the URL ends with "/v1/messages" or "/messages", it is stripped automatically.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.anthropic.com";

    /// <summary>The Anthropic API key (sent as the x-api-key header).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The default model name. Can be overridden per-request via AnthropicChatRequest.Model.
    /// </summary>
    public string DefaultModel { get; set; } = "claude-sonnet-4-5";

    /// <summary>The Anthropic API version header (anthropic-version).</summary>
    public string AnthropicVersion { get; set; } = "2023-06-01";

    /// <summary>HTTP timeout in milliseconds.</summary>
    public int TimeoutMs { get; set; } = 30000;

    /// <summary>Custom headers to include with every request.</summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = [];

    /// <summary>Validates that required options are set.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            throw new ArgumentException("BaseUrl is required");
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new ArgumentException("ApiKey is required");
        }
    }
}
