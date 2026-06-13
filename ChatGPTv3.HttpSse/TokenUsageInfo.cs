using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatGPTv3.HttpSse;

/// <summary>
/// Token usage information from chat completion responses.
/// Aligned with OpenAI API spec.
/// </summary>
public class TokenUsageInfo
{
    /// <summary>Number of tokens in the prompt (input).</summary>
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    /// <summary>Number of tokens in the generated completion (output).</summary>
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    /// <summary>Total tokens used (prompt + completion).</summary>
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }

    /// <summary>
    /// Breakdown of prompt tokens (cache hit info from providers like DeepSeek, Anthropic).
    /// Not all providers return this.
    /// </summary>
    [JsonPropertyName("prompt_tokens_details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PromptTokensDetails? PromptTokensDetails { get; set; }

    /// <summary>
    /// Breakdown of completion tokens (reasoning tokens from o-series models).
    /// </summary>
    [JsonPropertyName("completion_tokens_details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CompletionTokensDetails? CompletionTokensDetails { get; set; }

    /// <summary>Number of cached prompt tokens (cache hits, billed at reduced rate).</summary>
    public int GetCachedPromptTokens()
    {
        return PromptTokensDetails?.CachedTokens ?? 0;
    }

    /// <summary>Number of non-cached (newly processed) prompt tokens.</summary>
    public int GetNonCachedPromptTokens()
    {
        return PromptTokens - GetCachedPromptTokens();
    }

    /// <summary>Number of reasoning tokens used by o-series/reasoning models.</summary>
    public int GetReasoningTokens()
    {
        return CompletionTokensDetails?.ReasoningTokens ?? 0;
    }
}

/// <summary>
/// Breakdown of prompt token usage.
/// </summary>
public class PromptTokensDetails
{
    /// <summary>Number of tokens that were cached (cache hits).</summary>
    [JsonPropertyName("cached_tokens")]
    public int CachedTokens { get; set; }

    [JsonPropertyName("audio_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AudioTokens { get; set; }
}

/// <summary>
/// Breakdown of completion token usage.
/// </summary>
public class CompletionTokensDetails
{
    /// <summary>
    /// Tokens consumed by the model for internal reasoning (o-series models).
    /// These are NOT visible in the output content.
    /// </summary>
    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; set; }

    /// <summary>
    /// Tokens from accepted prediction (speculative decoding).
    /// </summary>
    [JsonPropertyName("accepted_prediction_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AcceptedPredictionTokens { get; set; }

    /// <summary>
    /// Tokens from rejected prediction (speculative decoding).
    /// </summary>
    [JsonPropertyName("rejected_prediction_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RejectedPredictionTokens { get; set; }
}
