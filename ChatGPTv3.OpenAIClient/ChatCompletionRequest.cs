using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatGPTv3.OpenAIClient;

/// <summary>
/// Full request body for OpenAI-compatible chat completions endpoint.
/// Aligned with OpenAI API spec as of 2025.
/// </summary>
public class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ChatMessage> Messages { get; set; } = [];

    /// <summary>
    /// The maximum number of tokens that can be generated in the chat completion.
    /// NOTE: max_completion_tokens is the preferred field; max_tokens is deprecated.
    /// </summary>
    [JsonPropertyName("max_completion_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Temperature { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    /// <summary>
    /// Options for streaming responses. Only sent when stream=true.
    /// </summary>
    [JsonPropertyName("stream_options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StreamOptions? StreamOptions { get; set; }

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ToolDefinition>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? ToolChoice { get; set; }

    /// <summary>
    /// Thinking mode control for OpenAI-compatible endpoints: {"type": "enabled"|"disabled"}.
    /// </summary>
    [JsonPropertyName("thinking")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ThinkingConfig? Thinking { get; set; }

    /// <summary>reasoning_effort for OpenAI-compatible endpoints (low / high / max, or a custom value).</summary>
    [JsonPropertyName("reasoning_effort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReasoningEffort { get; set; }

    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseFormat? ResponseFormat { get; set; }

    [JsonPropertyName("top_p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? TopP { get; set; }

    [JsonPropertyName("frequency_penalty")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? FrequencyPenalty { get; set; }

    [JsonPropertyName("presence_penalty")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? PresencePenalty { get; set; }

    [JsonPropertyName("stop")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Stop { get; set; }

    [JsonPropertyName("seed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Seed { get; set; }

    /// <summary>
    /// Output modalities for the model. Use ["text", "audio"] for TTS-capable models (GPT-4o-audio).
    /// Default is ["text"].
    /// </summary>
    [JsonPropertyName("modalities")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Modalities { get; set; }

    /// <summary>
    /// Audio output configuration. Required when modalities includes "audio".
    /// </summary>
    [JsonPropertyName("audio")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AudioConfig? Audio { get; set; }

    /// <summary>
    /// Enables audio output modality for TTS-capable models.
    /// </summary>
    public void EnableAudioOutput(string voice = "alloy", string format = "wav")
    {
        Modalities = ["text", "audio"];
        Audio = new AudioConfig { Voice = voice, Format = format };
    }

    /// <summary>
    /// Enables "auto" tool choice (model decides whether to call tools).
    /// </summary>
    public void EnableAutoToolChoice()
    {
        ToolChoice = "auto";
    }

    /// <summary>Sets thinking mode: enabled or disabled.</summary>
    public void SetThinking(bool enabled)
    {
        Thinking = new ThinkingConfig { Type = enabled ? "enabled" : "disabled" };
    }

    /// <summary>
    /// Disables tool calling for this request.
    /// </summary>
    public void DisableToolChoice()
    {
        ToolChoice = "none";
    }

    /// <summary>
    /// Forces a specific tool to be called.
    /// </summary>
    public void ForceToolChoice(string functionName)
    {
        ToolChoice = new { type = "function", function = new { name = functionName } };
    }

    /// <summary>
    /// Enables JSON mode response format.
    /// </summary>
    public void EnableJsonMode()
    {
        ResponseFormat = new ResponseFormat { Type = "json_object" };
    }

    /// <summary>
    /// Enables streaming usage reporting (recommended).
    /// When enabled, a final chunk with empty choices carries full usage stats.
    /// </summary>
    public void EnableStreamUsage()
    {
        StreamOptions = new StreamOptions { IncludeUsage = true };
    }

    /// <summary>
    /// Serialize messages list for the HTTP request body.
    /// </summary>
    public List<object> SerializeMessages()
    {
        var result = new List<object>();
        foreach (var msg in Messages)
        {
            result.Add(SerializeMessage(msg));
        }

        return result;
    }

    private Dictionary<string, object?> SerializeMessage(ChatMessage msg)
    {
        var dict = new Dictionary<string, object?> { ["role"] = msg.Role };

        if (msg.Parts != null && msg.Parts.Count > 0)
        {
            dict["content"] = msg.Parts.Select(p => p.ToSerializable()).ToList();
        }
        else if (msg.Content is string s)
        {
            dict["content"] = s;
        }
        else if (msg.Content is JsonElement je)
        {
            dict["content"] = je;
        }
        else if (msg.Content != null)
        {
            dict["content"] = msg.Content;
        }

        if (msg.Name != null)
        {
            dict["name"] = msg.Name;
        }

        if (msg.ToolCallId != null)
        {
            dict["tool_call_id"] = msg.ToolCallId;
        }

        if (msg.ToolCalls != null)
        {
            dict["tool_calls"] = msg.ToolCalls;
        }

        return dict;
    }
}

/// <summary>Thinking mode configuration: {"type": "enabled"|"disabled"}.</summary>
public class ThinkingConfig
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "enabled";
}

/// <summary>
/// Response format specification for JSON mode.
/// </summary>
public class ResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "json_object";
}

/// <summary>
/// Streaming options for chat completion requests.
/// </summary>
public class StreamOptions
{
    [JsonPropertyName("include_usage")]
    public bool IncludeUsage { get; set; }
}

/// <summary>
/// Audio output configuration for TTS-capable models (GPT-4o-audio).
/// </summary>
public class AudioConfig
{
    /// <summary>Voice to use: alloy, echo, fable, onyx, nova, shimmer.</summary>
    [JsonPropertyName("voice")]
    public string Voice { get; set; } = "alloy";

    /// <summary>Audio format: wav, mp3, flac, opus, pcm16.</summary>
    [JsonPropertyName("format")]
    public string Format { get; set; } = "wav";
}
