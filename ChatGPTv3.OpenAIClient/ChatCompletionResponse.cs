using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatGPTv3.OpenAIClient;

/// <summary>
/// Full response from the OpenAI-compatible chat completions endpoint (non-streaming).
/// Aligned with OpenAI API spec.
/// </summary>
public class ChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Always "chat.completion" for non-streaming responses.
    /// </summary>
    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("system_fingerprint")]
    public string? SystemFingerprint { get; set; }

    [JsonPropertyName("service_tier")]
    public string? ServiceTier { get; set; }

    [JsonPropertyName("choices")]
    public List<Choice> Choices { get; set; } = [];

    [JsonPropertyName("usage")]
    public TokenUsageInfo? Usage { get; set; }

    /// <summary>True when blocked by provider content filter.</summary>
    public bool IsContentFiltered() =>
        Choices.FirstOrDefault()?.FinishReason == "content_filter";

    /// <summary>
    /// Extracts the text content from the first choice, if any.
    /// </summary>
    public string? GetFirstChoiceText()
    {
        return Choices.FirstOrDefault()?.Message?.GetTextContent();
    }

    /// <summary>
    /// Extracts tool calls from the first choice, if any.
    /// </summary>
    public List<ToolCallRequest>? GetFirstChoiceToolCalls()
    {
        return Choices.FirstOrDefault()?.Message?.ToolCalls;
    }
}

/// <summary>
/// A single choice within a chat completion response.
/// </summary>
public class Choice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public ResponseMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

/// <summary>
/// The full message returned in a non-streaming choice.
/// </summary>
public class ResponseMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public object? Content { get; set; }

    /// <summary>
    /// If the model refused to answer, this contains the refusal explanation.
    /// </summary>
    [JsonPropertyName("refusal")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Refusal { get; set; }

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ToolCallRequest>? ToolCalls { get; set; }

    /// <summary>
    /// Gets the text content as a string, regardless of whether Content is a string,
    /// a JsonElement, or an array of content parts.
    /// </summary>
    public string? GetTextContent()
    {
        if (Content == null)
        {
            return null;
        }

        // Plain string content
        if (Content is string s)
        {
            return s;
        }

        if (Content is JsonElement je)
        {
            // String value
            if (je.ValueKind == JsonValueKind.String)
            {
                return je.GetString();
            }

            // Array of content parts (multi-modal response)
            if (je.ValueKind == JsonValueKind.Array)
            {
                var texts = new List<string>();
                foreach (var part in je.EnumerateArray())
                {
                    if (part.TryGetProperty("type", out var type))
                    {
                        if (type.GetString() == "text" &&
                            part.TryGetProperty("text", out var text))
                        {
                            texts.Add(text.GetString() ?? "");
                        }
                    }
                }
                return string.Join("", texts);
            }
        }

        return Content.ToString();
    }
}

/// <summary>
/// Delta content for a single streaming choice.
/// </summary>
public class DeltaContent
{
    /// <summary>
    /// The role (only present in the first streaming chunk). Usually "assistant".
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    /// <summary>
    /// The text content delta. Null when the model is making a tool call.
    /// Accumulate all deltas to reconstruct the full response.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>
    /// DeepSeek R1 reasoning content. OpenAI o-series models emit reasoning inline.
    /// </summary>
    [JsonPropertyName("reasoning_content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReasoningContent { get; set; }

    /// <summary>
    /// If the model refuses to answer in a streaming chunk.
    /// </summary>
    [JsonPropertyName("refusal")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Refusal { get; set; }

    /// <summary>
    /// Incremental tool calls. May arrive across multiple SSE events.
    /// Merged by SseResponseParser before being presented to callers.
    /// </summary>
    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ToolCallRequest>? ToolCalls { get; set; }

    /// <summary>
    /// Audio data for TTS-capable models (GPT-4o-audio).
    /// Contains base64-encoded audio chunk in the specified format.
    /// </summary>
    [JsonPropertyName("audio")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AudioDelta? Audio { get; set; }
}

/// <summary>
/// Audio data in a streaming delta (from TTS-capable models).
/// </summary>
public class AudioDelta
{
    /// <summary>Base64-encoded audio data chunk.</summary>
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    /// <summary>Unique identifier for this audio response.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Timestamp of this audio chunk.</summary>
    [JsonPropertyName("expires_at")]
    public long? ExpiresAt { get; set; }

    /// <summary>Transcription of the audio content (if available).</summary>
    [JsonPropertyName("transcript")]
    public string? Transcript { get; set; }

    /// <summary>Decodes the base64 audio data to bytes.</summary>
    public byte[]? GetAudioBytes()
    {
        return string.IsNullOrEmpty(Data) ? null : Convert.FromBase64String(Data);
    }
}