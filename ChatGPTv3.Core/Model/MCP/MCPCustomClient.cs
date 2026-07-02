using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Model.MCP;

/// <summary>
/// Built-in custom MCP client that executes tool functions in-process.
/// Tool definitions are generated via reflection-like helper from MCPSelfBuiltinTools registry.
/// </summary>
public class MCPCustomClient : MCPClientBase
{
    public override MCPClientType ToolType { get; set; } = MCPClientType.Custom;

    /// <summary>
    /// Context data passed to tool invocations.
    /// </summary>
    public MCPToolContext? Context { get; set; }

    public override Task<ToolDefinition[]> GetToolsAsync()
    {
        var tools = MCPSelfBuiltinTools.GetToolDefinitionsForClient(Name, Context);
        return Task.FromResult(tools);
    }

    public Task<object?> ExecuteToolAsync(ToolCallRequest toolCall, CancellationToken ct)
        => ExecuteToolAsync(toolCall, ct, null);

    /// <summary>
    /// Execute a tool with an explicit context override.
    /// When <paramref name="context"/> is provided it takes precedence over <see cref="Context"/>.
    /// This avoids race conditions when concurrent callers (conversation + scheduled task)
    /// share the same MCPCustomClient instance.
    /// </summary>
    public Task<object?> ExecuteToolAsync(ToolCallRequest toolCall, CancellationToken ct, MCPToolContext? context)
    {
        var name = toolCall.Function.Name;
        var args = toolCall.Function.ParseArguments();
        var ctx = context ?? Context;
        return MCPSelfBuiltinTools.ExecuteToolAsync(name, args, ctx);
    }
}

/// <summary>
/// Context information available to MCP tool invocations.
/// </summary>
public class MCPToolContext
{
    public long GroupId { get; set; }

    public long QQ { get; set; }

    public string ChatIdentity { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    public Action<string, long, long, int>? SendReply { get; set; }

    public int MessageId { get; set; }

    public bool EnableMemoryFunction { get; set; } = true;

    public bool EnableCQApiFunction { get; set; } = true;

    public bool EnableRelationshipFunction { get; set; } = true;

    public bool EnableRecordFunction { get; set; } = true;

    public string[]? DisabledTool { get; set; }

    /// <summary>
    /// Shared list for AddPictureToContext — the pipeline reads this
    /// after tool execution to inject images into the next request.
    /// </summary>
    public List<string> PendingImageHashes { get; set; } = [];
}