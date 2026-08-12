using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatGPTv3.AnthropicClient;

/// <summary>
/// Request body for the Anthropic Messages API (POST /v1/messages).
/// </summary>
public class AnthropicChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<AnthropicMessage> Messages { get; set; } = [];

    /// <summary>Required by the API — the maximum number of tokens to generate.</summary>
    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 1024;

    [JsonPropertyName("system")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? System { get; set; }

    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? TopP { get; set; }

    [JsonPropertyName("top_k")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TopK { get; set; }

    [JsonPropertyName("stop_sequences")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? StopSequences { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AnthropicTool>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? ToolChoice { get; set; }

    /// <summary>Model decides whether to call tools.</summary>
    public void EnableAutoToolChoice() => ToolChoice = new { type = "auto" };

    /// <summary>Disables tool calling for this request.</summary>
    public void DisableToolChoice() => ToolChoice = new { type = "none" };

    /// <summary>Forces a specific tool to be called.</summary>
    public void ForceToolChoice(string name) => ToolChoice = new { type = "tool", name };
}

/// <summary>
/// A single conversation message in the Anthropic Messages format.
/// Roles: "user" or "assistant". System prompts go in the top-level "system" field.
/// </summary>
public class AnthropicMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public List<AnthropicContentBlock> Content { get; set; } = [];
}

/// <summary>
/// A content block inside an Anthropic message: text, image, tool_use, or tool_result.
/// </summary>
public class AnthropicContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    /// <summary>Tool-use block id (returned by the model).</summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    /// <summary>Tool name for tool_use blocks.</summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    /// <summary>Tool input object for tool_use blocks (Anthropic expects an object, not a JSON string).</summary>
    [JsonPropertyName("input")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Input { get; set; }

    /// <summary>The tool_use id this tool_result answers.</summary>
    [JsonPropertyName("tool_use_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolUseId { get; set; }

    /// <summary>Tool result content (string or list of blocks).</summary>
    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Content { get; set; }

    [JsonPropertyName("is_error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsError { get; set; }

    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AnthropicImageSource? Source { get; set; }

    [JsonPropertyName("thinking")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Thinking { get; set; }
}

/// <summary>
/// Image source for Anthropic image blocks (base64 or URL).
/// </summary>
public class AnthropicImageSource
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "base64";

    [JsonPropertyName("media_type")]
    public string MediaType { get; set; } = "image/jpeg";

    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; set; }
}

/// <summary>
/// Tool definition for the Anthropic Messages API (uses "input_schema" instead of "parameters").
/// Function tools only need name/description/input_schema; server tools such as
/// web_search_20250305 are represented by type + name.
/// </summary>
public class AnthropicTool
{
    /// <summary>Tool discriminator for server tools (e.g. "web_search_20250305").</summary>
    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("input_schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? InputSchema { get; set; }

    public static AnthropicTool FromToolDefinition(ChatGPTv3.OpenAIClient.ToolDefinition definition) => new()
    {
        Name = definition.Function.Name,
        Description = definition.Function.Description,
        InputSchema = definition.Function.Parameters
    };

    /// <summary>
    /// Anthropic's built-in web search server tool. The API runs it automatically
    /// and reports results as web_search_tool_result content blocks.
    /// </summary>
    public static AnthropicTool WebSearch() => new()
    {
        Type = "web_search_20250305",
        Name = "web_search"
    };
}
