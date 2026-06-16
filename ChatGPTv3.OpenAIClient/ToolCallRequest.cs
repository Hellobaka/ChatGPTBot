using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatGPTv3.HttpSse;

/// <summary>
/// Represents a tool call request from the assistant (in streaming or non-streaming responses).
/// </summary>
public class ToolCallRequest
{
    /// <summary>
    /// Unique identifier for this tool call (matches the tool result's tool_call_id).
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Type discriminator. Always "function" for OpenAI-compatible APIs.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    /// <summary>
    /// The function call details.
    /// </summary>
    [JsonPropertyName("function")]
    public FunctionCall Function { get; set; } = new();

    /// <summary>
    /// The index of this tool call within the choice's tool_calls array.
    /// Used during streaming SSE incremental merging by SseResponseParser.
    /// Not serialized in API communication.
    /// </summary>
    [JsonIgnore]
    public int Index { get; set; }
}

/// <summary>
/// Function call details within a tool call request.
/// </summary>
public class FunctionCall
{
    /// <summary>
    /// The name of the function to call.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The JSON-encoded arguments string for the function call.
    /// </summary>
    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// Parse the arguments string into a JsonDocument.
    /// Returns null if parsing fails.
    /// </summary>
    public JsonDocument? ParseArguments()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Arguments)) return null;
            return JsonDocument.Parse(Arguments);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Tool call result from the function invocation, to be sent back to the API.
/// </summary>
public class ToolCallResult
{
    /// <summary>
    /// The tool call ID this result corresponds to.
    /// </summary>
    public string ToolCallId { get; set; } = string.Empty;

    /// <summary>
    /// The function name (for logging/tracking).
    /// </summary>
    public string FunctionName { get; set; } = string.Empty;

    /// <summary>
    /// The result content (as string).
    /// </summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>
    /// Whether the tool call was successful.
    /// </summary>
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// Creates a ChatMessage with role "tool" from this result.
    /// </summary>
    public ChatMessage ToChatMessage()
    {
        return new ChatMessage
        {
            Role = "tool",
            ToolCallId = ToolCallId,
            Content = IsSuccess ? Result : $"Error: {Result}"
        };
    }
}
