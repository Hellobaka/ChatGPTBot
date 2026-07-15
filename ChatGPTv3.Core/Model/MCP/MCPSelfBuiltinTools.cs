using ChatGPTv3.Core.Api;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;
using System.Text.Json;

namespace ChatGPTv3.Core.Model.MCP;

/// <summary>
/// Registry of all built-in MCP custom tools.
/// Maps tool names to their definitions and execution handlers.
/// </summary>
public static class MCPSelfBuiltinTools
{
    // CQ API methods reference Entry.API.GroupApi / Entry.API.FriendApi
    public static string[] GetBuiltinToolNames() => new[]
    {
        "AddKnowledge", "GetKnowledges",
        "UpdateMood", "UpdateFavorability", "GetRelationship",
        "GetGroupChatHistory", "GetPrivateChatHistory", "GetChatHistoryByIds",
        "GetRangeUsageDetail", "AddPictureToContext", "DescribeImage",
        "UpdateSchedule", "GetCurrentSchedule",
        "CreateSchedule", "ListSchedules", "DeleteSchedule",
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
            "AddPictureToContext" => [MakeTool("AddPictureToContext", "将图片注册到对话上下文，下一轮对话会以原生图片格式注入以获取更详细的信息",
                Schema(new() {
                    ["hash"] = ("string", "图片的MD5哈希值")
                }, ["hash"]))],
            "DescribeImage" => [MakeTool("DescribeImage", "获取图片的文本描述。可附带额外提示词引导描述方向",
                Schema(new() {
                    ["hash"] = ("string", "图片的MD5哈希值"),
                    ["extraPrompt"] = ("string", "额外的描述指导，如'注意图中的文字'、'关注人物表情'")
                }, ["hash"]))],

            "UpdateSchedule" => [MakeTool("UpdateSchedule", "更新或添加日程安排。当你的当前活动因对话而改变时调用。",
                Schema(new() {
                    ["time"] = ("string", "时间，如 14:00"),
                    ["action"] = ("string", "新的活动描述，如'在帮小张查资料（原计划打游戏）'")
                }, ["time", "action"]))],

            "GetCurrentSchedule" => [MakeTool("GetCurrentSchedule", "获取你当前时间段的日程安排。",
                Schema(new() {}, []))],

            "CreateSchedule" => [MakeTool("CreateSchedule",
                "创建一个定时任务。调用前请评估任务的紧急度和频率合理性：每小时或更高频率的任务不建议创建（如提醒喝水这种用户自己能做的事）。",
                Schema(new() {
                    ["name"] = ("string", "任务名称，如'提醒吃药'"),
                    ["cronExpr"] = ("string", "cron表达式，如'0 8 * * *'表示每天8点"),
                    ["prompt"] = ("string", "任务描述/给LLM的指令，如'提醒小王吃降压药，语气温和'")
                }, ["name", "cronExpr", "prompt"]))],

            "ListSchedules" => [MakeTool("ListSchedules", "列出所有定时任务。",
                Schema(new() {}, []))],

            "DeleteSchedule" => [MakeTool("DeleteSchedule", "删除一个定时任务。",
                Schema(new() {
                    ["id"] = ("integer", "任务ID")
                }, ["id"]))],
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
            "RemoveMessage" => [MakeTool("RemoveMessage", "撤回指定消息", Schema(new() {
                ["msgId"] = ("integer", "消息ID")
            }, ["msgId"]))],
            "SetGroupMemberBanSpeak" => [MakeTool("SetGroupMemberBanSpeak", "禁言指定群成员", Schema(new() {
                ["groupId"] = ("integer", "群号"),
                ["qq"] = ("integer", "成员QQ号"),
                ["duration"] = ("integer", "禁言时长(秒)，默认600")
            }, ["groupId", "qq"]))],
            "RemoveGroupMemberBanSpeak" => [MakeTool("RemoveGroupMemberBanSpeak", "解除指定群成员的禁言", Schema(new() {
                ["groupId"] = ("integer", "群号"),
                ["qq"] = ("integer", "成员QQ号")
            }, ["groupId", "qq"]))],
            "SetGroupBanSpeak" => [MakeTool("SetGroupBanSpeak", "开启全员禁言", Schema(new() {
                ["groupId"] = ("integer", "群号")
            }, ["groupId"]))],
            "RemoveGroupBanSpeak" => [MakeTool("RemoveGroupBanSpeak", "解除全员禁言", Schema(new() {
                ["groupId"] = ("integer", "群号")
            }, ["groupId"]))],
            "SetGroupMemberVisitingCard" => [MakeTool("SetGroupMemberVisitingCard", "设置群成员名片", Schema(new() {
                ["groupId"] = ("integer", "群号"),
                ["qq"] = ("integer", "成员QQ号"),
                ["card"] = ("string", "名片内容")
            }, ["groupId", "qq", "card"]))],
            "SetGroupMemberForeverExclusiveTitle" => [MakeTool("SetGroupMemberForeverExclusiveTitle", "设置群成员专属头衔", Schema(new() {
                ["groupId"] = ("integer", "群号"),
                ["qq"] = ("integer", "成员QQ号"),
                ["title"] = ("string", "专属头衔内容")
            }, ["groupId", "qq", "title"]))],
            "RemoveGroupMember" => [MakeTool("RemoveGroupMember", "踢出群成员", Schema(new() {
                ["groupId"] = ("integer", "群号"),
                ["qq"] = ("integer", "成员QQ号"),
                ["refuseJoin"] = ("boolean", "是否拒绝再次加群，默认false")
            }, ["groupId", "qq"]))],
            _ => []
        };
    }

    public static async Task<object?> ExecuteToolAsync(string name, JsonDocument? args, MCPToolContext ctx)
    {
        if (ctx == null)
        {
            return "无上下文";
        }

        try
        {
            return name switch
            {
                // Knowledge
                "AddKnowledge" => Mem(AddKnowledge, args),
                "GetKnowledges" => await GetKnowledgesAsync(args),

                // Relationship
                "UpdateMood" => UpdateMood(args, ctx),
                "UpdateFavorability" => UpdateFavorability(args, ctx),
                "GetRelationship" => GetRelationship(args, ctx),

                // Record
                "GetGroupChatHistory" => GetGroupChatHistory(args, ctx),
                "GetPrivateChatHistory" => GetPrivateChatHistory(args, ctx),
                "GetChatHistoryByIds" => GetChatHistoryByIds(args, ctx),

                // Picture
                "AddPictureToContext" => AddPictureToContext(args, ctx),
                "DescribeImage" => await DescribeImageAsync(args),

                // Misc
                "GetRangeUsageDetail" => GetRangeUsageDetail(args),
                "UpdateSchedule" => UpdateSchedule(args),
                "GetCurrentSchedule" => GetCurrentSchedule(),
                "CreateSchedule" => CreateScheduledTask(args, ctx),
                "ListSchedules" => ListScheduledTasks(),
                "DeleteSchedule" => DeleteScheduledTask(args),

                // Admin CQ API
                "GetLoginQQ" => $"{PromptBuilder.CurrentBotQQ}",
                "GetLoginNick" => AppConfig.BotName,
                "GetFriendList" => CQ_GetFriendList(),
                "GetGroupList" => CQ_GetGroupList(),
                "GetGroupMemberList" => CQ_GetGroupMemberList(args, ctx),
                "GetGroupMemberInfo" => CQ_GetGroupMemberInfo(args),
                "GetGroupInfo" => CQ_GetGroupInfo(args),
                "RemoveMessage" => CQ_RemoveMessage(args),
                "SetGroupMemberBanSpeak" => CQ_SetGroupMemberBanSpeak(args),
                "RemoveGroupMemberBanSpeak" => CQ_RemoveGroupMemberBanSpeak(args),
                "SetGroupBanSpeak" => CQ_SetGroupBanSpeak(args),
                "RemoveGroupBanSpeak" => CQ_RemoveGroupBanSpeak(args),
                "SetGroupMemberVisitingCard" => CQ_SetGroupMemberVisitingCard(args),
                "SetGroupMemberForeverExclusiveTitle" => CQ_SetGroupMemberForeverExclusiveTitle(args),
                "RemoveGroupMember" => CQ_RemoveGroupMember(args),
                _ => "工具暂未实现"
            };
        }
        catch (Exception ex)
        {
            return $"工具执行错误: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Knowledge Tools
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 添加一条客观知识到知识库（全局共享，不关联特定用户，存入 Qdrant）。
    /// </summary>
    private static string AddKnowledge(JsonDocument args)
    {
        var knowledge = args.RootElement.GetProperty("knowledge").GetString()!;
        MemoryManager.AddKnowledge(knowledge);
        return $"知识已添加: {knowledge}";
    }

    /// <summary>
    /// 通过语义搜索查询知识库（Qdrant 向量检索）。
    /// </summary>
    private static async Task<string> GetKnowledgesAsync(JsonDocument args)
    {
        var query = args.RootElement.GetProperty("query").GetString()!;
        var results = await MemoryManager.GetKnowledgeAsync(query);
        if (results.Length == 0)
        {
            return "未找到相关知识";
        }

        return string.Join("\n", results.Select(r => $"- {r.text} (score: {r.score:F2})"));
    }

    // ═══════════════════════════════════════════════════════════
    //  Picture Tools
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 将图片注册到待处理列表。下一轮对话时图片会以原生格式（base64/文件）
    /// 注入到 user message，供多模态模型直接"看到"。
    /// </summary>
    private static string AddPictureToContext(JsonDocument args, MCPToolContext ctx)
    {
        var hash = args.RootElement.GetProperty("hash").GetString()!;
        var picture = Picture.FindByHash(hash);
        if (picture == null)
        {
            return $"未找到 hash={hash} 的图片记录";
        }

        // Register for next-turn native injection
        ctx.PendingImageHashes.Add(hash);

        // If we already have a description, return it as feedback to LLM
        if (!string.IsNullOrEmpty(picture.Description))
        {
            picture.UseCount++;
            picture.LastUsedAt = DateTime.Now;
            Picture.Upsert(picture);
            return $"已注册图片 {hash}，下一轮将注入原生内容。当前描述: {picture.Description}";
        }

        return $"已注册图片 {hash}，当前无描述（将在下次对话中注入原生内容）";
    }

    /// <summary>
    /// 获取图片的文本描述。如果缓存中已有描述则直接返回，
    /// 否则调用视觉模型生成描述。
    /// </summary>
    private static async Task<string> DescribeImageAsync(JsonDocument args)
    {
        var hash = args.RootElement.GetProperty("hash").GetString()!;
        var extraPrompt = args.RootElement.TryGetProperty("extraPrompt", out var ep)
            ? ep.GetString() : null;

        var picture = Picture.FindByHash(hash);
        if (picture == null)
        {
            return $"未找到 hash={hash} 的图片记录";
        }

        // Resolve file path
        var filePath = picture.FilePath;
        if (!File.Exists(filePath))
        {
            var altPath = Path.Combine(CommonHelper.GetAppImageDirectory(), picture.FilePath);
            if (File.Exists(altPath))
            {
                filePath = altPath;
            }
            else
            {
                return $"图片文件不存在: hash={hash}";
            }
        }

        // Describe (uses cache if available, otherwise vision model)
        var desc = await ImageScraper.DescribeAsync(filePath, extraPrompt, picture.IsEmoji);
        if (desc == null)
        {
            return $"图片描述失败: hash={hash}";
        }

        return desc;
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
        if (string.IsNullOrWhiteSpace(moodText))
        {
            return "心情描述不能为空";
        }

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
        {
            return "参数错误：start/end 需要有效的日期时间格式";
        }

        var records = TokenUsage.GetRange(start, end);
        if (records.Count == 0)
        {
            return "该时间段内无使用记录";
        }

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
        if (SchedulerManager.Instance == null)
        {
            return "日程系统未初始化";
        }

        return SchedulerManager.Instance.GetCurrentSchedule(DateTime.Now);
    }

    // ═══════════════════════════════════════════════════════════
    //  Scheduled Task CRUD
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 创建一个定时任务。
    /// </summary>
    /// <param name="args">name (string) — 任务名; cronExpr (string); prompt (string) — LLM指令</param>
    /// <param name="ctx">工具上下文</param>
    /// <returns>确认消息，含任务ID</returns>
    private static string CreateScheduledTask(JsonDocument args, MCPToolContext ctx)
    {
        var name = args.RootElement.GetProperty("name").GetString()!;
        var cronExpr = args.RootElement.GetProperty("cronExpr").GetString()!;
        var prompt = args.RootElement.GetProperty("prompt").GetString()!;

        var minInterval = CronHelper.GetMinIntervalMinutes(cronExpr);
        if (minInterval != null && minInterval < AppConfig.MinCronIntervalMinutes)
        {
            return $"cron表达式频率过高（最小间隔约{minInterval}分钟），最低允许 {AppConfig.MinCronIntervalMinutes} 分钟";
        }

        var next = CronHelper.GetNextFireTime(cronExpr, DateTime.Now);
        if (!next.HasValue)
        {
            return "cron表达式无效，请使用5字段格式: 分 时 日 月 周";
        }

        var task = new ScheduledTask
        {
            TaskName = name,
            CronExpr = cronExpr,
            NextFireAt = next.Value,
            TargetType = ctx.GroupId > 0 ? 0 : 1,
            TargetId = ctx.GroupId > 0 ? ctx.GroupId : ctx.QQ,
            ExtraPrompt = prompt,
            CreatedBy = ctx.QQ,
            CreatedById = ctx.GroupId > 0 ? ctx.GroupId : ctx.QQ,
            CreatedAt = DateTime.Now
        };

        ScheduledTask.Insert(task);
        return $"定时任务已创建: {name} (ID={task.Id}, 下次触发: {task.NextFireAt:yyyy-MM-dd HH:mm})";
    }

    /// <summary>
    /// 列出所有已创建的定时任务。
    /// </summary>
    /// <returns>格式化的任务列表</returns>
    private static string ListScheduledTasks()
    {
        var tasks = ScheduledTask.GetAll();
        if (tasks.Count == 0)
        {
            return "暂无定时任务";
        }

        return string.Join("\n", tasks.Select(t =>
            $"- [{t.Id}] {t.TaskName} | {t.CronExpr} | 下次: {t.NextFireAt:yyyy-MM-dd HH:mm} | {(t.IsEnabled ? "启用" : "禁用")}"));
    }

    /// <summary>
    /// 删除指定ID的定时任务。
    /// </summary>
    /// <param name="args">id (int) — 任务ID</param>
    /// <returns>确认消息</returns>
    private static string DeleteScheduledTask(JsonDocument args)
    {
        var id = args.RootElement.GetProperty("id").GetInt32();
        ScheduledTask.Delete(id);
        return $"定时任务 {id} 已删除";
    }

    // ═══════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════

    private static Relationship GetOrCreateRelationship(long groupId, long qq)
    {
        var rel = Relationship.GetOrCreate(groupId, qq);
        if (string.IsNullOrEmpty(rel.NickName))
        {
            rel.NickName = qq.ToString();
        }

        return rel;
    }

    private static string FormatHistory(List<ChatRecord> records)
    {
        if (records.Count == 0)
        {
            return "无聊天记录";
        }

        return string.Join("\n", records.Select(r =>
            $"[{r.Time:HH:mm}] {r.NickName}[{r.QQ}]: {r.ParsedMessage}"));
    }

    /// <summary>Null-safe wrapper for tools.</summary>
    private static string Mem(Func<JsonDocument, string> fn, JsonDocument? args)
    {
        if (args == null)
        {
            return "参数错误";
        }

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

    // ═══════════════════════════════════════════════════════════
    //  CQ API Tools (need GroupApi / FriendApi wired in Entry)
    // ═══════════════════════════════════════════════════════════

    private static string CQ_GetFriendList()
    {
        if (Entry.FriendApi == null)
        {
            return "接口未就绪 (FriendApi)";
        }

        try
        {
            var friends = Entry.FriendApi.GetFriendInfos();
            if (friends == null || friends.Count == 0)
            {
                return "暂无好友";
            }

            return string.Join("\n", friends.Select(f => $"- {f.QQ} {f.Nick}"));
        }
        catch (Exception ex) { return $"获取失败: {ex.Message}"; }
    }

    private static string CQ_GetGroupList()
    {
        if (Entry.GroupApi == null)
        {
            return "接口未就绪 (GroupApi)";
        }

        try
        {
            var groups = Entry.GroupApi.GetGroupList();
            if (groups == null || groups.Count == 0)
            {
                return "未加入任何群";
            }

            return string.Join("\n", groups.Select(g => $"- {g.Group} {g.Name}"));
        }
        catch (Exception ex) { return $"获取失败: {ex.Message}"; }
    }

    private static string CQ_GetGroupMemberList(JsonDocument args, MCPToolContext ctx)
    {
        if (Entry.GroupApi == null)
        {
            return "接口未就绪 (GroupApi)";
        }

        var groupId = args.RootElement.TryGetProperty("groupId", out var g)
            && g.GetInt64() != 0 ? g.GetInt64() : ctx.GroupId;
        try
        {
            var members = Entry.GroupApi.GetGroupMembers(groupId);
            if (members == null || members.Count == 0)
            {
                return "暂无成员";
            }

            return string.Join("\n", members.Select(m => $"- {m.QQ} {m.Nick}"));
        }
        catch (Exception ex) { return $"获取失败: {ex.Message}"; }
    }

    private static string CQ_GetGroupMemberInfo(JsonDocument args)
    {
        if (Entry.GroupApi == null)
        {
            return "接口未就绪 (GroupApi)";
        }

        var qq = args.RootElement.GetProperty("qq").GetInt64();
        try
        {
            var info = Entry.GroupApi.GetGroupMemberInfo(0, qq);
            if (info == null)
            {
                return $"未找到 QQ={qq} 的成员信息";
            }

            return $"- {info.QQ} {info.Nick}";
        }
        catch (Exception ex) { return $"获取失败: {ex.Message}"; }
    }

    private static string CQ_GetGroupInfo(JsonDocument args)
    {
        if (Entry.GroupApi == null)
        {
            return "接口未就绪 (GroupApi)";
        }

        var groupId = args.RootElement.TryGetProperty("groupId", out var g)
            && g.GetInt64() != 0 ? g.GetInt64() : 0;
        if (groupId == 0)
        {
            return "请提供 groupId";
        }

        try
        {
            var info = Entry.GroupApi.GetGroupInfo(groupId);
            if (info == null)
            {
                return $"未找到群 {groupId} 的信息";
            }

            return $"- {info.Group} {info.Name}";
        }
        catch (Exception ex) { return $"获取失败: {ex.Message}"; }
    }

    private static string CQ_RemoveMessage(JsonDocument args)
    {
        if (Entry.MessageApi == null)
        {
            return "接口未就绪 (MessageApi)";
        }

        var msgId = args.RootElement.GetProperty("msgId").GetInt64();
        try
        {
            Entry.MessageApi.DeleteMessage(msgId);
            return $"消息 {msgId} 已撤回";
        }
        catch (Exception ex) { return $"撤回失败: {ex.Message}"; }
    }

    private static string CQ_SetGroupMemberBanSpeak(JsonDocument args)
    {
        if (Entry.GroupApi == null)
        {
            return "接口未就绪 (GroupApi)";
        }

        var groupId = args.RootElement.GetProperty("groupId").GetInt64();
        var qq = args.RootElement.GetProperty("qq").GetInt64();
        var duration = args.RootElement.TryGetProperty("duration", out var d)
            ? d.GetInt64() : 600;
        try
        {
            Entry.GroupApi.BanMember(groupId, qq, duration);
            return $"已将 {qq} 禁言 {duration} 秒";
        }
        catch (Exception ex) { return $"操作失败: {ex.Message}"; }
    }

    private static string CQ_RemoveGroupMemberBanSpeak(JsonDocument args)
    {
        if (Entry.GroupApi == null)
        {
            return "接口未就绪 (GroupApi)";
        }

        var groupId = args.RootElement.GetProperty("groupId").GetInt64();
        var qq = args.RootElement.GetProperty("qq").GetInt64();
        try
        {
            Entry.GroupApi.BanMember(groupId, qq, 0);
            return $"已解除 {qq} 的禁言";
        }
        catch (Exception ex) { return $"操作失败: {ex.Message}"; }
    }

    private static string CQ_SetGroupBanSpeak(JsonDocument args)
    {
        if (Entry.GroupApi == null)
        {
            return "接口未就绪 (GroupApi)";
        }

        var groupId = args.RootElement.GetProperty("groupId").GetInt64();
        try
        {
            Entry.GroupApi.BanGroup(groupId, true);
            return $"已对群 {groupId} 开启全员禁言";
        }
        catch (Exception ex) { return $"操作失败: {ex.Message}"; }
    }

    private static string CQ_RemoveGroupBanSpeak(JsonDocument args)
    {
        if (Entry.GroupApi == null)
        {
            return "接口未就绪 (GroupApi)";
        }

        var groupId = args.RootElement.GetProperty("groupId").GetInt64();
        try
        {
            Entry.GroupApi.BanGroup(groupId, false);
            return $"已解除群 {groupId} 全员禁言";
        }
        catch (Exception ex) { return $"操作失败: {ex.Message}"; }
    }

    private static string CQ_SetGroupMemberVisitingCard(JsonDocument args)
    {
        if (Entry.GroupApi == null)
        {
            return "接口未就绪 (GroupApi)";
        }

        var groupId = args.RootElement.GetProperty("groupId").GetInt64();
        var qq = args.RootElement.GetProperty("qq").GetInt64();
        var card = args.RootElement.GetProperty("card").GetString()!;
        try
        {
            Entry.GroupApi.SetMemberCard(groupId, qq, card);
            return $"已将 {qq} 的群名片设为: {card}";
        }
        catch (Exception ex) { return $"操作失败: {ex.Message}"; }
    }

    private static string CQ_SetGroupMemberForeverExclusiveTitle(JsonDocument args)
    {
        if (Entry.GroupApi == null)
        {
            return "接口未就绪 (GroupApi)";
        }

        var groupId = args.RootElement.GetProperty("groupId").GetInt64();
        var qq = args.RootElement.GetProperty("qq").GetInt64();
        var title = args.RootElement.GetProperty("title").GetString()!;
        try
        {
            Entry.GroupApi.SetMemberTitle(groupId, qq, title);
            return $"已将 {qq} 的专属头衔设为: {title}";
        }
        catch (Exception ex) { return $"操作失败: {ex.Message}"; }
    }

    private static string CQ_RemoveGroupMember(JsonDocument args)
    {
        if (Entry.GroupApi == null)
        {
            return "接口未就绪 (GroupApi)";
        }

        var groupId = args.RootElement.GetProperty("groupId").GetInt64();
        var qq = args.RootElement.GetProperty("qq").GetInt64();
        var refuseJoin = args.RootElement.TryGetProperty("refuseJoin", out var r) && r.GetBoolean();
        try
        {
            Entry.GroupApi.Kick(groupId, qq, refuseJoin);
            return $"已将 {qq} 踢出群 {groupId} (拒绝再次加群: {refuseJoin})";
        }
        catch (Exception ex) { return $"操作失败: {ex.Message}"; }
    }
}