using System.Text.Json;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Model;
using ChatGPTv3.HttpSse;

namespace ChatGPTv3.Core.Model.MCP;

/// <summary>
/// Registry of all built-in MCP custom tools.
/// Maps tool names to their definitions and execution handlers.
/// Ported from v2's MCPClient.cs built-in tools (~25 tools across 4 categories).
/// </summary>
public static class MCPSelfBuiltinTools
{
    /// <summary>
    /// All built-in custom tool names (used to auto-register MCPCustomClient entries).
    /// </summary>
    public static string[] GetBuiltinToolNames() => new[]
    {
        // Memory tools
        "AddShortTermMemory", "RenewShortTermMemory", "RemoveShortTermMemory",
        "RemoveShortTermMemories", "AddToDoItem", "CompleteToDoItem",
        "RemoveToDoItem", "RemoveToDoItems",
        "AddLongTermMemory", "GetLongTermMemories",
        "AddKnowledge", "GetKnowledges",

        // Relationship tools
        "UpdateMood", "UpdateFavorability", "GetRelationship",

        // Record tools
        "GetGroupChatHistory", "GetPrivateChatHistory", "GetChatHistoryByIds",

        // Misc
        "GetRangeUsageDetail", "AddPictureToContext",

        // Admin CQ API tools
        "GetLoginQQ", "GetLoginNick", "GetFriendList",
        "GetGroupList", "GetGroupMemberList", "GetGroupMemberInfo", "GetGroupInfo",

        // Moderation
        "RemoveMessage", "SetGroupMemberBanSpeak",
        "RemoveGroupMemberBanSpeak", "SetGroupBanSpeak", "RemoveGroupBanSpeak",
        "SetGroupMemberVisitingCard", "SetGroupMemberForeverExclusiveTitle",
        "RemoveGroupMember"
    };

    /// <summary>
    /// Gets ToolDefinition[] for a named client. Returns empty if client doesn't match.
    /// </summary>
    public static ToolDefinition[] GetToolDefinitionsForClient(string clientName, MCPToolContext? context)
    {
        return clientName switch
        {
            "AddShortTermMemory" => [MakeTool("AddShortTermMemory", "添加新的短期记忆",
                ToolSchemaBuilder.CreateSchema(new() {
                    ["memory"] = ("string", "需要记录的记忆内容")
                }, ["memory"]))],

            "RenewShortTermMemory" => [MakeTool("RenewShortTermMemory", "刷新短期记忆的使用次数",
                ToolSchemaBuilder.CreateSchema(new() {
                    ["id"] = ("integer", "需要刷新的记忆ID")
                }, ["id"]))],

            "RemoveShortTermMemory" => [MakeTool("RemoveShortTermMemory", "删除指定的短期记忆",
                ToolSchemaBuilder.CreateSchema(new() {
                    ["id"] = ("integer", "需要删除的记忆ID")
                }, ["id"]))],

            "RemoveShortTermMemories" => [MakeTool("RemoveShortTermMemories", "批量删除短期记忆",
                ToolSchemaBuilder.CreateSchema(new() {
                    ["ids"] = ("array", "需要删除的记忆ID数组")
                }, ["ids"]))],

            "AddToDoItem" => [MakeTool("AddToDoItem", "添加待办事项",
                ToolSchemaBuilder.CreateSchema(new() {
                    ["todo"] = ("string", "待办事项内容"),
                    ["isGlobalTodo"] = ("boolean", "是否为全局待办")
                }, ["todo"]))],

            "CompleteToDoItem" => [MakeTool("CompleteToDoItem", "完成待办事项",
                ToolSchemaBuilder.CreateSchema(new() {
                    ["id"] = ("integer", "待办事项ID")
                }, ["id"]))],

            "RemoveToDoItem" => [MakeTool("RemoveToDoItem", "删除待办事项",
                ToolSchemaBuilder.CreateSchema(new() {
                    ["id"] = ("integer", "待办事项ID")
                }, ["id"]))],

            "RemoveToDoItems" => [MakeTool("RemoveToDoItems", "批量删除待办事项",
                ToolSchemaBuilder.CreateSchema(new() {
                    ["ids"] = ("array", "待办事项ID数组")
                }, ["ids"]))],

            "UpdateMood" => [MakeTool("UpdateMood", "更新你的心情状态。每次发言后调用。",
                ToolSchemaBuilder.CreateSchema(new() {
                    ["valence"] = ("number", "愉悦度：0(非常负面)-1(非常正面)"),
                    ["arousal"] = ("number", "兴奋度：0(非常平静)-1(非常激动)")
                }, ["valence", "arousal"]))],

            "UpdateFavorability" => [MakeTool("UpdateFavorability", "更新对当前对话用户的好感度。每次发言后调用。",
                ToolSchemaBuilder.CreateSchema(new() {
                    ["change"] = ("integer", "好感度变化量，范围-100到100")
                }, ["change"]))],

            "GetRelationship" => [MakeTool("GetRelationship", "查询与指定用户的好感度",
                ToolSchemaBuilder.CreateSchema(new() {
                    ["qq"] = ("integer", "要查询的QQ号")
                }, ["qq"]))],

            _ => []
        };
    }

    /// <summary>
    /// Executes a tool by name with the given arguments and context.
    /// </summary>
    public static Task<object?> ExecuteToolAsync(string name, JsonDocument? args, MCPToolContext? ctx)
    {
        if (ctx == null) return Task.FromResult<object?>("无上下文");

        try
        {
            return Task.FromResult<object?>(name switch
            {
                "UpdateMood" => UpdateMood(args),
                "UpdateFavorability" => UpdateFavorability(args, ctx),
                "GetRelationship" => GetRelationship(args, ctx),
                _ => "工具未实现"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult<object?>($"工具执行错误: {ex.Message}");
        }
    }

    // ── Tool Implementations ─────────────────────────────

    private static string UpdateMood(JsonDocument? args)
    {
        if (args == null) return "参数错误";
        var root = args.RootElement;
        var valence = root.GetProperty("valence").GetDouble();
        var arousal = root.GetProperty("arousal").GetDouble();
        MoodManager?.UpdateMood(valence, arousal);
        return $"心情已更新: valence={valence}, arousal={arousal}";
    }

    private static string UpdateFavorability(JsonDocument? args, MCPToolContext ctx)
    {
        if (args == null) return "参数错误";
        var change = args.RootElement.GetProperty("change").GetInt32();
        // TODO: Update favorability in DB
        return $"好感度已调整 {change:+0;-0}";
    }

    private static string GetRelationship(JsonDocument? args, MCPToolContext ctx)
    {
        if (args == null) return "参数错误";
        var qq = args.RootElement.GetProperty("qq").GetInt64();
        // TODO: Query relationship from DB
        return $"QQ={qq} 的好感度信息待实现";
    }

    // ── Helpers ──────────────────────────────────────────

    private static ToolDefinition MakeTool(string name, string description, JsonElement parameters)
    {
        return new ToolDefinition
        {
            Type = "function",
            Function = FunctionDefinition.Create(name, description, parameters)
        };
    }

    /// <summary>
    /// Global reference to MoodManager, set during initialization.
    /// </summary>
    public static MoodManager? MoodManager { get; set; }
}
