using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatGPTv3.OpenAIClient;

/// <summary>
/// Represents a chat message in the OpenAI-compatible conversation format.
/// Supports plain text, multi-modal content parts, and tool calls.
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// The role of the message author. One of: system, user, assistant, tool.
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Plain text content. Used when Parts is null/empty.
    /// Mutually exclusive with Parts for typical usage.
    /// </summary>
    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Content { get; set; }

    /// <summary>
    /// Multi-modal content parts (text + image_url).
    /// When set, content is serialized as an array of content parts.
    /// </summary>
    [JsonIgnore]
    public List<ContentPart>? Parts { get; set; }

    /// <summary>
    /// Optional name for the message author.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    /// <summary>
    /// Tool call ID (required when role is "tool").
    /// </summary>
    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; set; }

    /// <summary>
    /// Tool calls made by the assistant (required when role is "assistant" and tool calls were made).
    /// </summary>
    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ToolCallRequest>? ToolCalls { get; set; }

    /// <summary>
    /// Creates a simple text message.
    /// </summary>
    public static ChatMessage System(string content) => new() { Role = "system", Content = content };
    public static ChatMessage User(string content) => new() { Role = "user", Content = content };
    public static ChatMessage Assistant(string content) => new() { Role = "assistant", Content = content };
    public static ChatMessage Tool(string toolCallId, string content) => new() { Role = "tool", ToolCallId = toolCallId, Content = content };

    /// <summary>
    /// Creates a user message with multi-modal content (text + images).
    /// </summary>
    public static ChatMessage UserWithParts(List<ContentPart> parts) => new() { Role = "user", Parts = parts };

    /// <summary>
    /// Creates an assistant message with tool calls.
    /// </summary>
    public static ChatMessage AssistantWithToolCalls(List<ToolCallRequest> toolCalls) =>
        new() { Role = "assistant", ToolCalls = toolCalls, Content = null };

    /// <summary>
    /// Converts to the JSON-serializable format expected by OpenAI API.
    /// </summary>
    public object GetSerializableContent()
    {
        if (Parts != null && Parts.Count > 0)
        {
            return Parts.Select(p => p.ToSerializable()).ToList();
        }
        return Content ?? string.Empty;
    }
}
