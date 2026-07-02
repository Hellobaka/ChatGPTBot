using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatGPTv3.OpenAIClient;

/// <summary>
/// Tool definition for OpenAI function calling. Contains the function name,
/// description, and JSON Schema parameters.
/// </summary>
public class ToolDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public FunctionDefinition Function { get; set; } = new();
}

/// <summary>
/// Function descriptor within a tool definition.
/// </summary>
public class FunctionDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// JSON Schema object describing the function parameters.
    /// Stored as JsonElement to preserve the exact schema structure.
    /// </summary>
    [JsonPropertyName("parameters")]
    public JsonElement Parameters { get; set; }

    /// <summary>
    /// Creates a FunctionDefinition with a simple JSON Schema from a type descriptor.
    /// </summary>
    public static FunctionDefinition Create(string name, string description, JsonElement parameters)
    {
        return new FunctionDefinition
        {
            Name = name,
            Description = description,
            Parameters = parameters
        };
    }

    /// <summary>
    /// Creates a FunctionDefinition with no parameters (empty object schema).
    /// </summary>
    public static FunctionDefinition CreateNoParams(string name, string description)
    {
        var emptySchema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new Dictionary<string, object>(),
            required = Array.Empty<string>()
        });
        return new FunctionDefinition
        {
            Name = name,
            Description = description,
            Parameters = emptySchema
        };
    }
}

/// <summary>
/// Helper to build JSON Schema parameter definitions using System.Text.Json.
/// </summary>
public static class ToolSchemaBuilder
{
    /// <summary>
    /// Creates a JSON Schema object with the specified properties.
    /// Each property is defined by name, type, description, and whether it's required.
    /// </summary>
    public static JsonElement CreateSchema(
        Dictionary<string, (string type, string description)>? properties = null,
        string[]? required = null)
    {
        properties ??= new();
        required ??= Array.Empty<string>();

        var schemaProperties = new Dictionary<string, object>();
        foreach (var (name, (type, description)) in properties)
        {
            schemaProperties[name] = new Dictionary<string, object>
            {
                ["type"] = type,
                ["description"] = description
            };
        }

        var schema = new
        {
            type = "object",
            properties = schemaProperties,
            required = required
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    /// <summary>
    /// Creates a FunctionDefinition from a name, description, and property definitions.
    /// </summary>
    public static FunctionDefinition CreateFunction(
        string name,
        string description,
        Dictionary<string, (string type, string description)>? properties = null,
        string[]? required = null)
    {
        return FunctionDefinition.Create(name, description, CreateSchema(properties, required));
    }
}