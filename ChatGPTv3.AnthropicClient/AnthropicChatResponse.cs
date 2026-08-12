using System.Text.Json;
using System.Text.Json.Serialization;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.AnthropicClient;

/// <summary>
/// Full response from the Anthropic Messages API (non-streaming).
/// </summary>
public class AnthropicChatResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public List<AnthropicContentBlock> Content { get; set; } = [];

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }

    [JsonPropertyName("stop_sequence")]
    public string? StopSequence { get; set; }

    [JsonPropertyName("usage")]
    public AnthropicUsage? Usage { get; set; }

    /// <summary>Concatenates all text blocks in the response.</summary>
    public string? GetText() =>
        string.Concat(Content.Where(c => c.Type == "text").Select(c => c.Text ?? ""));

    /// <summary>Extracts tool_use blocks as OpenAI-style ToolCallRequest items.</summary>
    public List<ToolCallRequest>? GetToolUse()
    {
        var calls = Content
            .Where(c => c.Type == "tool_use")
            .Select(c => new ToolCallRequest
            {
                Id = c.Id ?? string.Empty,
                Type = "function",
                Function = new FunctionCall
                {
                    Name = c.Name ?? string.Empty,
                    Arguments = c.Input?.GetRawText() ?? "{}"
                }
            })
            .ToList();

        return calls.Count > 0 ? calls : null;
    }
}

/// <summary>
/// Token usage reported by the Anthropic Messages API.
/// </summary>
public class AnthropicUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("cache_creation_input_tokens")]
    public int CacheCreationInputTokens { get; set; }

    [JsonPropertyName("cache_read_input_tokens")]
    public int CacheReadInputTokens { get; set; }

    /// <summary>Converts to the unified TokenUsageInfo used by UsageTracker.</summary>
    public TokenUsageInfo ToTokenUsageInfo() => new()
    {
        PromptTokens = InputTokens,
        CompletionTokens = OutputTokens,
        TotalTokens = InputTokens + OutputTokens,
        PromptTokensDetails = new PromptTokensDetails
        {
            CachedTokens = CacheReadInputTokens
        }
    };
}
