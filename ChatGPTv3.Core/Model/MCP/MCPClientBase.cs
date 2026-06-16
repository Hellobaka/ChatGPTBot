using System.Text.Json;
using System.Text.Json.Serialization;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Model.MCP;

/// <summary>
/// MCP transport type discriminator.
/// </summary>
public enum MCPClientType { Http, Stdio, Custom }

/// <summary>
/// Abstract base for MCP clients. Supports HTTP, STDIO, and custom in-process implementations.
/// Uses ModelContextProtocol NuGet for transport, wraps tools as HttpSse ToolDefinition[].
/// </summary>
public abstract class MCPClientBase
{
    [JsonPropertyName("ToolType")]
    public virtual MCPClientType ToolType { get; set; } = MCPClientType.Custom;

    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    // ── Permission Settings ──
    public bool GroupEnabled { get; set; }
    public bool PersonEnabled { get; set; }
    public bool CanOnlyMasterCall { get; set; }
    public bool IsGroupBlackList { get; set; }
    public long[] Groups { get; set; } = [];
    public bool IsPersonBlackList { get; set; }
    public long[] Persons { get; set; } = [];

    // ── Tool Name Mapping ──
    public Dictionary<string, string> ToolNameConverters { get; set; } = [];

    /// <summary>
    /// Returns the tool definitions from this MCP client.
    /// </summary>
    public virtual Task<ToolDefinition[]> GetToolsAsync() => Task.FromResult(Array.Empty<ToolDefinition>());

    /// <summary>
    /// Starts any background maintenance (e.g., heartbeat).
    /// </summary>
    public virtual void Start() { }

    /// <summary>
    /// Stops background maintenance and releases resources.
    /// </summary>
    public virtual void Stop() { }
}
