using System.Text.Json.Serialization;

namespace ChatGPTv3.OpenAIClient;

/// <summary>
/// Represents a single streaming chat completion chunk (one SSE "data:" event parsed).
/// Aligned with OpenAI API spec: object type is "chat.completion.chunk".
/// </summary>
public class StreamingUpdate
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Always "chat.completion.chunk" for streaming responses.
    /// </summary>
    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("created")]
    public long? Created { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Backend configuration fingerprint. Changes when model config is updated.
    /// </summary>
    [JsonPropertyName("system_fingerprint")]
    public string? SystemFingerprint { get; set; }

    /// <summary>
    /// Service tier that processed the request: auto, default, flex, priority.
    /// </summary>
    [JsonPropertyName("service_tier")]
    public string? ServiceTier { get; set; }

    [JsonPropertyName("choices")]
    public List<StreamingChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public TokenUsageInfo? Usage { get; set; }

    // ── Convenience accessors ────────────────────────────

    /// <summary>
    /// Whether this is a stream_options usage chunk (has usage but empty choices).
    /// </summary>
    public bool IsUsageChunk()
    {
        return Usage != null && (Choices == null || Choices.Count == 0);
    }

    /// <summary>
    /// Extracts the text delta from the first streaming choice.
    /// </summary>
    public string? GetDeltaContent()
    {
        return Choices?.FirstOrDefault()?.Delta?.Content;
    }

    /// <summary>
    /// Extracts reasoning content from the first streaming choice (DeepSeek R1 style).
    /// Note: OpenAI o-series models also emit reasoning in content.
    /// </summary>
    public string? GetReasoningContent()
    {
        return Choices?.FirstOrDefault()?.Delta?.ReasoningContent;
    }

    /// <summary>
    /// Gets the finish reason from the first choice.
    /// </summary>
    public string? GetFinishReason()
    {
        return Choices?.FirstOrDefault()?.FinishReason;
    }

    /// <summary>
    /// Gets tool call deltas from the first streaming choice.
    /// These are MERGED (accumulated) tool calls from SseResponseParser.
    /// </summary>
    public List<ToolCallRequest>? GetToolCalls()
    {
        return Choices?.FirstOrDefault()?.Delta?.ToolCalls;
    }

    /// <summary>
    /// Whether the response was blocked by the provider's content filter.
    /// finish_reason = "content_filter"
    /// </summary>
    public bool IsContentFiltered()
    {
        return GetFinishReason() == "content_filter";
    }

    /// <summary>
    /// Whether this update signals the end of tool call accumulation.
    /// </summary>
    public bool IsToolCallFinish()
    {
        return GetFinishReason() == "tool_calls";
    }

    /// <summary>
    /// Whether this update signals the normal end of streaming.
    /// </summary>
    public bool IsStreamEnd()
    {
        var reason = GetFinishReason();
        return reason == "stop" || reason == "length" || reason == "content_filter";
    }
}

/// <summary>
/// A single streaming choice delta in a chat completion chunk.
/// </summary>
public class StreamingChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("delta")]
    public DeltaContent? Delta { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}