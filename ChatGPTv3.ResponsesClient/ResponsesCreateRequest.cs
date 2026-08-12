using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatGPTv3.ResponsesClient;

/// <summary>
/// Request body for the OpenAI Responses API (POST /v1/responses).
/// </summary>
public class ResponsesCreateRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("instructions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Instructions { get; set; }

    /// <summary>Either a plain string or a list of input items (message / function_call / function_call_output).</summary>
    [JsonPropertyName("input")]
    public object Input { get; set; } = string.Empty;

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ResponsesTool>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? ToolChoice { get; set; }

    [JsonPropertyName("parallel_tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ParallelToolCalls { get; set; }

    [JsonPropertyName("max_output_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxOutputTokens { get; set; }

    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Temperature { get; set; }

    [JsonPropertyName("top_p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? TopP { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("stream_options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponsesStreamOptions? StreamOptions { get; set; }

    /// <summary>
    /// Whether to store the response for later retrieval. Defaults to false for
    /// this stateless chat bot to avoid retaining conversation data.
    /// </summary>
    [JsonPropertyName("store")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Store { get; set; } = false;

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponsesTextConfig? Text { get; set; }

    /// <summary>Model decides whether to call tools.</summary>
    public void EnableAutoToolChoice() => ToolChoice = "auto";

    /// <summary>Disables tool calling for this request.</summary>
    public void DisableToolChoice() => ToolChoice = "none";

    /// <summary>Forces a specific function tool to be called.</summary>
    public void ForceToolChoice(string name) => ToolChoice = new { type = "function", name };

    /// <summary>Enables JSON mode via text.format = json_object.</summary>
    public void EnableJsonMode()
    {
        Text = new ResponsesTextConfig
        {
            Format = new ResponsesTextFormat { Type = "json_object" }
        };
    }
}

/// <summary>
/// Tool definition for the Responses API. Function tools use type "function" with
/// name/description/parameters; built-in tools such as web_search only carry a type.
/// </summary>
public class ResponsesTool
{
    /// <summary>Tool discriminator: "function", "web_search", etc.</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Parameters { get; set; }

    public static ResponsesTool FromToolDefinition(ChatGPTv3.OpenAIClient.ToolDefinition definition) => new()
    {
        Name = definition.Function.Name,
        Description = definition.Function.Description,
        Parameters = definition.Function.Parameters
    };

    /// <summary>
    /// OpenAI's built-in web search tool. The API runs it server-side and returns
    /// web_search_call output items; no client-side tool result is required.
    /// </summary>
    public static ResponsesTool WebSearch() => new()
    {
        Type = "web_search"
    };
}

/// <summary>Streaming options for the Responses API.</summary>
public class ResponsesStreamOptions
{
    [JsonPropertyName("include_obfuscation")]
    public bool IncludeObfuscation { get; set; }
}

/// <summary>Text response configuration (plain text or structured JSON).</summary>
public class ResponsesTextConfig
{
    [JsonPropertyName("format")]
    public ResponsesTextFormat Format { get; set; } = new();
}

/// <summary>Text format selector: "text", "json_object", or "json_schema".</summary>
public class ResponsesTextFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";
}
