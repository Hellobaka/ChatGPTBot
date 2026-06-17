using System.Text.Json;
using ChatGPTv3.Core.Api;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Model.MCP;

/// <summary>
/// Registry of all built-in MCP custom tools.
/// Maps tool names to their definitions and execution handlers.
/// </summary>
public static class MCPSelfBuiltinTools
{
    public static string[] GetBuiltinToolNames() => new[]
    {
        "AddShortTermMemory", "RenewShortTermMemory", "RemoveShortTermMemory",
        "RemoveShortTermMemories", "AddToDoItem", "CompleteToDoItem",
        "RemoveToDoItem", "RemoveToDoItems",
        "AddLongTermMemory", "GetLongTermMemories",
        "AddKnowledge", "GetKnowledges",
        "UpdateMood", "UpdateFavorability", "GetRelationship",
        "GetGroupChatHistory", "GetPrivateChatHistory", "GetChatHistoryByIds",
        "GetRangeUsageDetail", "AddPictureToContext",
        "UpdateSchedule", "GetCurrentSchedule",
        "GetLoginQQ", "GetLoginNick", "GetFriendList",
        "GetGroupList", "GetGroupMemberList", "GetGroupMemberInfo", "GetGroupInfo",
        "RemoveMessage", "SetGroupMemberBanSpeak",
        "RemoveGroupMemberBanSpeak", "SetGroupBanSpeak", "RemoveGroupBanSpeak",
        "SetGroupMemberVisitingCard", "SetGroupMemberForeverExclusiveTitle",
        "RemoveGroupMember"
    };

    public static ToolDefinition[] GetToolDefinitionsForClient(string clientName, MCPToolContext? context)
    {
        return clientName switch
        {
            "AddShortTermMemory" => [MakeTool("AddShortTermMemory", "添加新的短期记忆", Schema(new() {
                ["memory"] = ("string", "需要记录的记忆内容")
            }, ["memory"]))],
            "RenewShortTermMemory" => [MakeTool("RenewShortTermMemory", "刷新短期记忆的过期时间", Schema(new() {
                ["id"] = ("integer", "需要刷新的记忆ID")
            }, ["id"]))],
            "RemoveShortTermMemory" => [MakeTool("RemoveShortTermMemory", "删除指定的短期记忆", Schema(new() {
                ["id"] = ("integer", "需要删除的记忆ID")
            }, ["id"]))],
            "RemoveShortTermMemories" => [MakeTool("RemoveShortTermMemories", "批量删除短期记忆", Schema(new() {
                ["ids"] = ("array", "需要删除的记忆ID数组")
            }, ["ids"]))],
            "AddToDoItem" => [MakeTool("AddToDoItem", "添加待办事项", Schema(new() {
                ["todo"] = ("string", "待办事项内容"),
                ["isGlobalTodo"] = ("boolean", "是否为全局待办，默认false")
            }, ["todo"]))],
            "CompleteToDoItem" => [MakeTool("CompleteToDoItem", "标记待办事项为已完成", Schema(new() {
                ["id"] = ("integer", "待办事项ID")
            }, ["id"]))],
            "RemoveToDoItem" => [MakeTool("RemoveToDoItem", "删除待办事项", Schema(new() {
                ["id"] = ("integer", "待办事项ID")
            }, ["id"]))],
            "RemoveToDoItems" => [MakeTool("RemoveToDoItems", "批量删除待办事项", Schema(new() {
                ["ids"] = ("array", "待办事项ID数组")
            }, ["ids"]))],
            "AddLongTermMemory" => [MakeTool("AddLongTermMemory", "添加长期记忆（与QQ号关联的持久信息）", Schema(new() {
                ["memory"] = ("string", "需要长期记忆的内容，如'用户小王5月20日生日'")
            }, ["memory"]))],
            "GetLongTermMemories" => [MakeTool("GetLongTermMemories", "查询当前用户的长期记忆", Schema(new() {
                ["query"] = ("string", "搜索查询文本")
            }, ["query"]))],
            "AddKnowledge" => [MakeTool("AddKnowledge", "添加知识到知识库", Schema(new() {
                ["knowledge"] = ("string", "知识内容，客观事实")
            }, ["knowledge"]))],
            "GetKnowledges" => [MakeTool("GetKnowledges", "搜索知识库", Schema(new() {
                ["query"] = ("string", "搜索查询文本")
            }, ["query"]))],
            "UpdateMood" => [MakeTool("UpdateMood", "更新你在这个群里的心情状态。仅在心情有明显变化时调用。", Schema(new() {
                ["mood"] = ("string", "用自然语言描述当前心情，如'刚才和小王聊游戏很开心，现在很放松'")
            }, ["mood"]))],
            "UpdateFavorability" => [MakeTool("UpdateFavorability", "调整对当前用户的好感度。每次发言后调用。", Schema(new() {
                ["change"] = ("integer", "好感变化量，正值提升，负值降低，范围-100到+100")
            }, ["change"]))],
            "GetRelationship" => [MakeTool("GetRelationship", "查询你对指定用户的好感度", Schema(new() {
                ["qq"] = ("integer", "要查询的QQ号")
            }, ["qq"]))],
            "GetGroupChatHistory" => [MakeTool("GetGroupChatHistory", "获取群聊历史消息", Schema(new() {
                ["count"] = ("integer", "获取消息数量，默认20")
            }, []))],
            "GetPrivateChatHistory" => [MakeTool("GetPrivateChatHistory", "获取私聊历史消息", Schema(new() {
                ["count"] = ("integer", "获取消息数量，默认20")
            }, []))],
            "GetChatHistoryByIds" => [MakeTool("GetChatHistoryByIds", "通过消息ID获取聊天记录", Schema(new() {
                ["ids"] = ("array", "消息ID数组")
            }, ["ids"]))],
            "GetRangeUsageDetail" => [MakeTool("GetRangeUsageDetail", "获取指定时间范围内的Token消耗详情", Schema(new() {
                ["start"] = ("string", "开始时间，如 2025-10-13T10:56:40"),
                ["end"] = ("string", "结束时间，如 2025-10-13T19:56:40")
            }, ["start", "end"]))],
            "AddPictureToContext" => [MakeTool("AddPictureToContext", "将图片添加到对话上下文中，用于获取更详细的图片信息",
                Schema(new() {
                    ["hash"] = ("string", "图片的MD5哈希值")
                }, ["hash"]))],

            "UpdateSchedule" => [MakeTool("UpdateSchedule", "更新或添加日程安排。当你的当前活动因对话而改变时调用。",
                Schema(new() {
                    ["time"] = ("string", "时间，如 14:00"),
                    ["action"] = ("string", "新的活动描述，如'在帮小张查资料（原计划打游戏）'")
                }, ["time", "action"]))],

            "GetCurrentSchedule" => [MakeTool("GetCurrentSchedule", "获取你当前时间段的日程安排。",
                Schema(new() {}, []))],
            "GetLoginQQ" => [MakeTool("GetLoginQQ", "获取当前登录的Bot QQ号",
                Schema(new() {}, []))],
            "GetLoginNick" => [MakeTool("GetLoginNick", "获取当前登录的Bot昵称",
                Schema(new() {}, []))],
            "GetFriendList" => [MakeTool("GetFriendList", "获取Bot的好友列表",
                Schema(new() {}, []))],
            "GetGroupList" => [MakeTool("GetGroupList", "获取Bot加入的群列表",
                Schema(new() {}, []))],
            "GetGroupMemberList" => [MakeTool("GetGroupMemberList", "获取指定群的成员列表", Schema(new() {
                ["groupId"] = ("integer", "群号，默认当前群")
            }, []))],
            "GetGroupMemberInfo" => [MakeTool("GetGroupMemberInfo", "获取指定群成员的信息", Schema(new() {
                ["qq"] = ("integer", "成员QQ号")
            }, ["qq"]))],
            "GetGroupInfo" => [MakeTool("GetGroupInfo", "获取指定群的群信息", Schema(new() {
                ["groupId"] = ("integer", "群号，默认当前群")
            }, []))],
            _ => []
        };
    }

    public static Task<object?> ExecuteToolAsync(string name, JsonDocument? args, MCPToolContext ctx)
    {
        if (ctx == null) return Task.FromResult<object?>("无上下文");

        try
        {
            return Task.FromResult<object?>(name switch
            {
                // Memory
                "AddShortTermMemory"      => Mem(AddShortTermMemory, args, ctx),
                "RenewShortTermMemory"    => Mem(RenewShortTermMemory, args),
                "RemoveShortTermMemory"   => Mem(RemoveShortTermMemory, args),
                "RemoveShortTermMemories" => Mem(RemoveShortTermMemories, args),
                "AddToDoItem"             => Mem(AddToDoItem, args, ctx),
                "CompleteToDoItem"        => Mem(CompleteToDoItem, args),
                "RemoveToDoItem"          => Mem(RemoveToDoItem, args),
                "RemoveToDoItems"         => Mem(RemoveToDoItems, args),
                "AddLongTermMemory"       => Mem(AddLongTermMemory, args, ctx),
                "GetLongTermMemories"     => Mem(GetLongTermMemories, args, ctx),
                "AddKnowledge"            => Mem(AddKnowledge, args),
                "GetKnowledges"           => Mem(GetKnowledges, args),

                // Relationship
                "UpdateMood"       => UpdateMood(args, ctx),
                "UpdateFavorability" => UpdateFavorability(args, ctx),
                "GetRelationship"   => GetRelationship(args, ctx),

                // Record
                "GetGroupChatHistory"  => GetGroupChatHistory(args, ctx),
                "GetPrivateChatHistory" => GetPrivateChatHistory(args, ctx),
                "GetChatHistoryByIds"  => GetChatHistoryByIds(args, ctx),

                // Misc
                "GetRangeUsageDetail" => GetRangeUsageDetail(args),
                "UpdateSchedule"     => UpdateSchedule(args),
                "GetCurrentSchedule" => GetCurrentSchedule(),

                // Admin CQ API
                "GetLoginQQ"   => $"{PromptBuilder.CurrentBotQQ}",
                "GetLoginNick" => AppConfig.BotName,
                _ => "工具暂未实现"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult<object?>($"工具执行错误: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Memory Tools
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 添加一条短期记忆。短期记忆有使用次数上限，超限后自动过期。
    /// </summary>
    /// <param name="args">memory (string) — 需要记录的自然语言描述</param>
    /// <param name="ctx">工具上下文，提供 GroupID 和 QQ</param>
    /// <returns>确认消息，含添加的记忆内容</returns>
    private static string AddShortTermMemory(JsonDocument args, MCPToolContext ctx)
    {
        var memory = args.RootElement.GetProperty("memory").GetString()!;
        MemoryManager.AddShortTerm(memory, ctx.GroupId, ctx.QQ);
        return $"短期记忆已添加: {memory}";
    }

    /// <summary>
    /// 重置一条短期记忆的过期计数器，使其重新生效。
    /// </summary>
    /// <param name="args">id (int) — 记忆 ID</param>
    /// <returns>确认消息</returns>
    private static string RenewShortTermMemory(JsonDocument args)
    {
        var id = args.RootElement.GetProperty("id").GetInt32();
        MemoryManager.RenewShortTerm(id);
        return $"短期记忆 {id} 已刷新";
    }

    /// <summary>
    /// 删除指定 ID 的短期记忆。
    /// </summary>
    /// <param name="args">id (int) — 记忆 ID</param>
    /// <returns>确认消息</returns>
    private static string RemoveShortTermMemory(JsonDocument args)
    {
        var id = args.RootElement.GetProperty("id").GetInt32();
        MemoryManager.RemoveShortTerm(id);
        return $"短期记忆 {id} 已删除";
    }

    /// <summary>
    /// 批量删除短期记忆。
    /// </summary>
    /// <param name="args">ids (int[]) — 记忆 ID 数组</param>
    /// <returns>确认消息，含删除数量</returns>
    private static string RemoveShortTermMemories(JsonDocument args)
    {
        var ids = args.RootElement.GetProperty("ids").EnumerateArray()
            .Select(e => e.GetInt32()).ToArray();
        foreach (var id in ids) MemoryManager.RemoveShortTerm(id);
        return $"已批量删除 {ids.Length} 条短期记忆";
    }

    /// <summary>
    /// 添加一条待办事项。可设为全局（所有群可见）或本群可见。
    /// </summary>
    /// <param name="args">todo (string) — 待办内容; isGlobalTodo (bool, 可选) — 是否全局待办</param>
    /// <param name="ctx">工具上下文</param>
    /// <returns>确认消息</returns>
    private static string AddToDoItem(JsonDocument args, MCPToolContext ctx)
    {
        var todo = args.RootElement.GetProperty("todo").GetString()!;
        bool isGlobal = args.RootElement.TryGetProperty("isGlobalTodo", out var g) && g.GetBoolean();
        MemoryManager.AddToDo(todo, isGlobal, ctx.GroupId, ctx.QQ);
        return $"待办已添加: {todo}";
    }

    /// <summary>
    /// 将待办事项标记为已完成。
    /// </summary>
    /// <param name="args">id (int) — 待办 ID</param>
    /// <returns>确认消息</returns>
    private static string CompleteToDoItem(JsonDocument args)
    {
        var id = args.RootElement.GetProperty("id").GetInt32();
        MemoryManager.CompleteToDo(id);
        return $"待办 {id} 已标记完成";
    }

    /// <summary>
    /// 删除指定 ID 的待办事项。
    /// </summary>
    /// <param name="args">id (int) — 待办 ID</param>
    /// <returns>确认消息</returns>
    private static string RemoveToDoItem(JsonDocument args)
    {
        var id = args.RootElement.GetProperty("id").GetInt32();
        MemoryManager.RemoveToDo(id);
        return $"待办 {id} 已删除";
    }

    /// <summary>
    /// 批量删除待办事项。
    /// </summary>
    /// <param name="args">ids (int[]) — 待办 ID 数组</param>
    /// <returns>确认消息，含删除数量</returns>
    private static string RemoveToDoItems(JsonDocument args)
    {
        var ids = args.RootElement.GetProperty("ids").EnumerateArray()
            .Select(e => e.GetInt32()).ToArray();
        foreach (var id in ids) MemoryManager.RemoveToDo(id);
        return $"已批量删除 {ids.Length} 条待办";
    }

    /// <summary>
    /// 添加一条长期记忆，与当前用户 QQ 关联，跨会话持久保存（存入 Qdrant 向量数据库）。
    /// </summary>
    /// <param name="args">memory (string) — 需要长期记住的自然语言描述</param>
    /// <param name="ctx">工具上下文</param>
    /// <returns>确认消息</returns>
    private static string AddLongTermMemory(JsonDocument args, MCPToolContext ctx)
    {
        var memory = args.RootElement.GetProperty("memory").GetString()!;
        MemoryManager.AddLongTerm(memory, ctx.QQ);
        return $"长期记忆已添加: {memory}";
    }

    /// <summary>
    /// 通过语义搜索查询当前用户的长期记忆（Qdrant 向量检索）。
    /// </summary>
    /// <param name="args">query (string) — 搜索查询文本</param>
    /// <param name="ctx">工具上下文，提供 QQ 用于过滤结果</param>
    /// <returns>匹配的记忆列表（含相似度分数），或"未找到"</returns>
    private static string GetLongTermMemories(JsonDocument args, MCPToolContext ctx)
    {
        var query = args.RootElement.GetProperty("query").GetString()!;
        var results = MemoryManager.GetLongTerm(query, ctx.QQ);
        if (results.Length == 0) return "未找到相关长期记忆";
        return string.Join("\n", results.Select(r => $"- {r.text} (score: {r.score:F2})"));
    }

    /// <summary>
    /// 添加一条客观知识到知识库（全局共享，不关联特定用户，存入 Qdrant）。
    /// </summary>
    /// <param name="args">knowledge (string) — 知识内容</param>
    /// <returns>确认消息</returns>
    private static string AddKnowledge(JsonDocument args)
    {
        var knowledge = args.RootElement.GetProperty("knowledge").GetString()!;
        MemoryManager.AddKnowledge(knowledge);
        return $"知识已添加: {knowledge}";
    }

    /// <summary>
    /// 通过语义搜索查询知识库（Qdrant 向量检索）。
    /// </summary>
    /// <param name="args">query (string) — 搜索查询文本</param>
    /// <returns>匹配的知识列表（含相似度分数），或"未找到"</returns>
    private static string GetKnowledges(JsonDocument args)
    {
        var query = args.RootElement.GetProperty("query").GetString()!;
        var results = MemoryManager.GetKnowledge(query);
        if (results.Length == 0) return "未找到相关知识";
        return string.Join("\n", results.Select(r => $"- {r.text} (score: {r.score:F2})"));
    }

    // ═══════════════════════════════════════════════════════════
    //  Relationship Tools
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 更新你在当前对话上下文中的心情状态（自然语言一句描述）。
    /// 仅在心情有明显变化时调用。
    /// </summary>
    /// <param name="args">mood (string) — 自然语言心情描述，如"刚才和小王聊游戏很开心"</param>
    /// <param name="ctx">工具上下文，用于确定心情所属的群/私聊</param>
    /// <returns>确认消息</returns>
    private static string UpdateMood(JsonDocument args, MCPToolContext ctx)
    {
        var moodText = args.RootElement.GetProperty("mood").GetString()!;
        if (string.IsNullOrWhiteSpace(moodText)) return "心情描述不能为空";
        long contextId = ctx.GroupId > 0 ? ctx.GroupId : ctx.QQ;
        MoodState.UpdateMood(contextId, moodText);
        return $"心情已更新: {moodText}";
    }

    /// <summary>
    /// 调整对当前用户的好感度值（0-100）。
    /// </summary>
    /// <param name="args">change (int) — 好感变化量，正值提升/负值降低，范围 -100~+100</param>
    /// <param name="ctx">工具上下文</param>
    /// <returns>确认消息，含调整后的好感度值</returns>
    private static string UpdateFavorability(JsonDocument args, MCPToolContext ctx)
    {
        var change = args.RootElement.GetProperty("change").GetInt32();
        change = Math.Clamp(change, -100, 100);
        var rel = GetOrCreateRelationship(ctx.GroupId, ctx.QQ);
        rel.Favorability = Math.Clamp(rel.Favorability + change, 0, 100);
        rel.InteractionCount++;
        rel.LastInteractionTime = DateTime.Now;
        Relationship.Update(rel);
        return $"好感度已调整 {change:+0;-0}，当前值: {rel.Favorability}";
    }

    /// <summary>
    /// 查询你对指定用户的好感度信息。
    /// </summary>
    /// <param name="args">qq (long) — 要查询的 QQ 号</param>
    /// <param name="ctx">工具上下文，提供 GroupID</param>
    /// <returns>好感度数值、互动次数等信息</returns>
    private static string GetRelationship(JsonDocument args, MCPToolContext ctx)
    {
        var qq = args.RootElement.GetProperty("qq").GetInt64();
        var rel = GetOrCreateRelationship(ctx.GroupId, qq);
        return $"QQ={qq} 对你好感度: {rel.Favorability}/100，互动次数: {rel.InteractionCount}";
    }

    // ═══════════════════════════════════════════════════════════
    //  Record / History Tools
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 获取当前群的最近聊天记录。
    /// </summary>
    /// <param name="args">count (int, 可选, 默认 20) — 获取消息数量</param>
    /// <param name="ctx">工具上下文，提供 GroupID</param>
    /// <returns>格式化的历史消息文本 [HH:mm] NickName[QQ]: message，或"无聊天记录"</returns>
    private static string GetGroupChatHistory(JsonDocument args, MCPToolContext ctx)
    {
        int count = args.RootElement.TryGetProperty("count", out var c) ? c.GetInt32() : 20;
        var records = ChatRecord.GetGroupHistory(ctx.GroupId, count);
        return FormatHistory(records);
    }

    /// <summary>
    /// 获取当前用户的最近私聊记录。
    /// </summary>
    /// <param name="args">count (int, 可选, 默认 20) — 获取消息数量</param>
    /// <param name="ctx">工具上下文，提供 QQ</param>
    /// <returns>格式化的历史消息文本</returns>
    private static string GetPrivateChatHistory(JsonDocument args, MCPToolContext ctx)
    {
        int count = args.RootElement.TryGetProperty("count", out var c) ? c.GetInt32() : 20;
        var records = ChatRecord.GetPrivateHistory(ctx.QQ, count);
        return FormatHistory(records);
    }

    /// <summary>
    /// 通过消息 ID 数组获取对应的聊天记录，用于解析被引用消息的内容。
    /// </summary>
    /// <param name="args">ids (long[]) — 消息 ID 数组</param>
    /// <param name="ctx">工具上下文，提供 GroupID</param>
    /// <returns>格式化的消息文本</returns>
    private static string GetChatHistoryByIds(JsonDocument args, MCPToolContext ctx)
    {
        var ids = args.RootElement.GetProperty("ids").EnumerateArray()
            .Select(e => e.GetInt64()).ToArray();
        var records = ChatRecord.GetByIds(ids, ctx.GroupId);
        return FormatHistory(records);
    }

    // ═══════════════════════════════════════════════════════════
    //  Usage / Misc Tools
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 查询指定时间范围内的 Token 消耗详情，用于统计和分析。
    /// </summary>
    /// <param name="args">start (DateTime) — 开始时间; end (DateTime) — 结束时间</param>
    /// <returns>格式化的使用记录文本，或"无记录"</returns>
    private static string GetRangeUsageDetail(JsonDocument args)
    {
        if (!args.RootElement.TryGetProperty("start", out var s) ||
            !args.RootElement.TryGetProperty("end", out var e) ||
            !DateTime.TryParse(s.GetString(), out var start) ||
            !DateTime.TryParse(e.GetString(), out var end))
            return "参数错误：start/end 需要有效的日期时间格式";

        var records = TokenUsage.GetRange(start, end);
        if (records.Count == 0) return "该时间段内无使用记录";
        return string.Join("\n", records.Select(r =>
            $"- {r.Time:yyyy-MM-dd HH:mm} | {r.Purpose} | {r.Model} | in:{r.PromptTokens} out:{r.CompletionTokens}"));
    }

    // ═══════════════════════════════════════════════════════════
    //  Schedule Tools
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 更新或添加日程安排。当你的当前活动因对话而改变时调用。
    /// </summary>
    /// <param name="args">time (string) — 时间如"14:00"; action (string) — 新活动描述</param>
    /// <returns>确认消息</returns>
    private static string UpdateSchedule(JsonDocument args)
    {
        var time = args.RootElement.GetProperty("time").GetString()!;
        var action = args.RootElement.GetProperty("action").GetString()!;
        SchedulerManager.Instance?.UpdateSchedule(time, action);
        return $"日程已更新: {time} → {action}";
    }

    /// <summary>
    /// 获取当前时间段的日程安排。
    /// </summary>
    /// <returns>当前活动描述文本</returns>
    private static string GetCurrentSchedule()
    {
        if (SchedulerManager.Instance == null) return "日程系统未初始化";
        return SchedulerManager.Instance.GetCurrentSchedule(DateTime.Now);
    }

    // ═══════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════

    private static Relationship GetOrCreateRelationship(long groupId, long qq)
    {
        var rel = Relationship.GetOrCreate(groupId, qq);
        if (string.IsNullOrEmpty(rel.NickName)) rel.NickName = qq.ToString();
        return rel;
    }

    private static string FormatHistory(List<ChatRecord> records)
    {
        if (records.Count == 0) return "无聊天记录";
        return string.Join("\n", records.Select(r =>
            $"[{r.Time:HH:mm}] {r.NickName}[{r.QQ}]: {r.ParsedMessage}"));
    }

    /// <summary>Null-safe wrapper for memory tools that need context.</summary>
    private static string Mem(Func<JsonDocument, MCPToolContext, string> fn, JsonDocument? args, MCPToolContext ctx)
    {
        if (args == null) return "参数错误";
        return fn(args, ctx);
    }

    /// <summary>Null-safe wrapper for memory tools without context.</summary>
    private static string Mem(Func<JsonDocument, string> fn, JsonDocument? args)
    {
        if (args == null) return "参数错误";
        return fn(args);
    }

    private static ToolDefinition MakeTool(string name, string description, JsonElement parameters)
    {
        return new ToolDefinition
        {
            Type = "function",
            Function = FunctionDefinition.Create(name, description, parameters)
        };
    }

    private static JsonElement Schema(Dictionary<string, (string type, string desc)> props, string[] required)
    {
        return ToolSchemaBuilder.CreateSchema(props, required);
    }
}
