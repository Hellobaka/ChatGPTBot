using Another_Mirai_Native.Abstractions.Enums;
using Another_Mirai_Native.Abstractions.Models;
using Another_Mirai_Native.Abstractions.Models.MessageItem;
using ChatGPTv3.Core.Api;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Model;
using ChatGPTv3.Core.Model.MCP;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Commands;

/// <summary>
/// Extension methods that add middleware components to the pipeline.
/// </summary>
public static class PipelineMiddlewareExtensions
{
    public static ChatPipelineBuilder UsePrivateAccessControl(this ChatPipelineBuilder builder)
    {
        return builder.Use(async (ctx, next) =>
        {
            if (AppConfig.IsPersonBlackList)
            {
                if (AppConfig.PersonList.Contains(ctx.QQ))
                {
                    ctx.Trace("PrivateAccessControl", false, $"QQ {ctx.QQ} 命中私聊黑名单");
                    ctx.Result = EventHandleResult.Pass;
                    return;
                }
            }
            else
            {
                if (!AppConfig.PersonList.Contains(ctx.QQ))
                {
                    ctx.Trace("PrivateAccessControl", false, $"QQ {ctx.QQ} 不在私聊白名单");
                    ctx.Result = EventHandleResult.Pass;
                    return;
                }
            }
            ctx.Trace("PrivateAccessControl", true, "私聊权限检查通过");
            await next();
        });
    }

    public static ChatPipelineBuilder UsePrivateReplyDecision(this ChatPipelineBuilder builder)
    {
        return builder.Use(async (ctx, next) =>
        {
            ctx.IsMentioned = true; // private — always respond
            ctx.Trace("PrivateReplyDecision", true, "私聊模式固定响应");
            await next();
        });
    }

    /// <summary>
    /// Reads a config value, preferring the group override if available.
    /// Falls back to the global default when no group config exists or the key is absent.
    /// </summary>
    private static T Effective<T>(ChatContext ctx, string key, T globalDefault)
    {
        if (ctx.GroupConfig != null)
        {
            var overrideValue = ctx.GroupConfig.GetConfigValue<T>(key);
            if (overrideValue != null)
            {
                return overrideValue;
            }
        }
        return globalDefault;
    }

    // ── Access control (global only) ───────────────────────

    public static ChatPipelineBuilder UseAccessControl(this ChatPipelineBuilder builder)
    {
        return builder.Use(async (ctx, next) =>
        {
            if (AppConfig.IsGroupBlackList)
            {
                if (AppConfig.GroupList.Contains(ctx.GroupId))
                {
                    ctx.Trace("AccessControl", false, $"群 {ctx.GroupId} 命中黑名单");
                    ctx.Result = EventHandleResult.Pass;
                    return;
                }
            }
            else
            {
                if (!AppConfig.GroupList.Contains(ctx.GroupId))
                {
                    ctx.Trace("AccessControl", false, $"群 {ctx.GroupId} 不在白名单");
                    ctx.Result = EventHandleResult.Pass;
                    return;
                }
            }
            ctx.Trace("AccessControl", true, "群权限检查通过");
            await next();
        });
    }

    public static ChatPipelineBuilder UseMessageFilter(this ChatPipelineBuilder builder)
    {
        return builder.Use(async (ctx, next) =>
        {
            // Load per-group config override early so all downstream middleware can use Effective()
            if (ctx.IsGroup)
            {
                ctx.GroupConfig = GroupConfig.Get(ctx.GroupId);
            }

            if (string.IsNullOrWhiteSpace(ctx.MessageText) && !HasImage(ctx))
            {
                ctx.Trace("MessageFilter", false, "消息为空且不含图片");
                ctx.Result = EventHandleResult.Pass;
                return;
            }
            ctx.Trace("MessageFilter", true, "消息内容有效");
            await next();
        });
    }

    public static ChatPipelineBuilder UseConcurrencyGate(this ChatPipelineBuilder builder)
    {
        // Per-context key: GroupId (or QQ for private). Stores the current CTS + debounce source.
        var active = new Dictionary<long, (CancellationTokenSource cts, int version)>();
        var gateLock = new object();

        return builder.Use(async (ctx, next) =>
        {
            // Per-context key: group id for group chats, QQ for private chats
            var key = ctx.IsGroup ? ctx.GroupId : ctx.QQ;

            CancellationTokenSource cts;
            int myVersion;

            lock (gateLock)
            {
                // Cancel and remove any previous entry for this context
                if (active.TryGetValue(key, out var old))
                {
                    old.cts.Cancel();
                    old.cts.Dispose();
                }

                cts = new CancellationTokenSource();
                myVersion = old.version + 1;
                active[key] = (cts, myVersion);
            }

            try
            {
                // ── Debounce: wait, but if a newer version arrives, cancel ──
                var debounceMs = Effective(ctx, "MessageDebounceMs", AppConfig.MessageDebounceMs);
                if (debounceMs > 0)
                {
                    try
                    {
                        await Task.Delay(debounceMs, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Newer message arrived during debounce — discard this one
                        CommonHelper.DebugLog("Pipeline", $"防抖取消 {key}");
                        ctx.Trace("ConcurrencyGate", false, $"防抖阶段被更新消息替换，key={key}");
                        ctx.Result = EventHandleResult.Pass;
                        return;
                    }
                }

                // Check I'm still the latest version after debounce
                bool iAmLatest;
                lock (gateLock)
                {
                    iAmLatest = active.TryGetValue(key, out var current)
                        && current.version == myVersion;
                }
                if (!iAmLatest)
                {
                    ctx.Trace("ConcurrencyGate", false, $"当前消息已过期，存在更新版本，key={key}");
                    ctx.Result = EventHandleResult.Pass;
                    return;
                }

                ctx.CancellationToken = cts.Token;
                ctx.Trace("ConcurrencyGate", true, $"并发门控通过，key={key}");
                await next();
            }
            catch (OperationCanceledException)
            {
                CommonHelper.DebugLog("Pipeline", $"已打断 {key}");
                ctx.Trace("ConcurrencyGate", false, $"处理过程中被打断，key={key}");
                ctx.Result = EventHandleResult.Pass;
            }
            finally
            {
                lock (gateLock)
                {
                    if (active.TryGetValue(key, out var current) && current.version == myVersion)
                    {
                        current.cts.Dispose();
                        active.Remove(key);
                    }
                }
            }
        });
    }

    public static ChatPipelineBuilder UseReplyDecision(this ChatPipelineBuilder builder)
    {
        return builder.Use(async (ctx, next) =>
        {
            ctx.IsMentioned = CheckAtBot(GetMessage(ctx));
            ctx.ContainsNickname = CheckNickname(ctx, ctx.MessageText);
            ctx.IsImageOnly = HasImage(ctx) && string.IsNullOrWhiteSpace(ctx.MessageText);
            ctx.IsReplyToBot = CheckReplyToBot(GetMessage(ctx), ctx.GroupId);
            ctx.HasQuestion = ctx.MessageText.Contains('?') || ctx.MessageText.Contains('？');

            var replyManager = ReplyManager.Get(ctx.GroupId);
            bool fromSamePerson = ctx.QQ == replyManager.LastReplyQQ;
            ctx.ReplyProbability = replyManager.UpdateWillingness(
                ctx.IsMentioned, ctx.IsReplyToBot, ctx.ContainsNickname,
                ctx.HasQuestion, fromSamePerson, ctx.IsImageOnly, ctx.QQ);

            // LLM-based check
            if (Effective(ctx, "EnableLLMCheckShouldResponse", AppConfig.EnableLLMCheckShouldResponse) && !ctx.IsMentioned)
            {
                var history = ChatRecord.GetGroupHistory(ctx.GroupId, Effective(ctx, "ContextMaxLength", AppConfig.ContextMaxLength));
                var recentTexts = history
                    .Select(r => $"[{r.NickName}]: {r.ParsedMessage}")
                    .ToList();

                (bool shouldResponse, double confidence, string reasoning) = await ReplyManager.CheckByLLM(
                    Effective(ctx, "BotName", AppConfig.BotName),
                    Effective(ctx, "BotNicknames", AppConfig.BotNicknames),
                    PromptBuilder.CurrentBotQQ, recentTexts);
                if (!shouldResponse)
                {
                    ctx.Trace("ReplyDecision", false, $"LLM认为不应该回复，置信度: {confidence}");
                    ctx.Reasoning = reasoning;
                    ctx.Result = EventHandleResult.Pass;
                    return;
                }
                if (confidence < 0)
                {
                    ctx.Trace("ReplyDecision", false, $"LLM判断失败，使用内置方案");
                }
                else
                {
                    ctx.ReplyProbability = confidence;
                }
            }

            // Random check
            double decision = CommonHelper.NextDouble();
            if (decision >= ctx.ReplyProbability)
            {
                replyManager.AfterSkip();
                ctx.Trace("ReplyDecision", false,
                    $"未触发回复：prob={ctx.ReplyProbability:F3}, decision={decision}, mentioned={ctx.IsMentioned}, replyToBot={ctx.IsReplyToBot}, nickname={ctx.ContainsNickname}, question={ctx.HasQuestion}, imageOnly={ctx.IsImageOnly}");
                ctx.Result = EventHandleResult.Pass;
                return;
            }

            ctx.Trace("ReplyDecision", true,
                $"触发回复：prob={ctx.ReplyProbability:F3}, decision={decision}, mentioned={ctx.IsMentioned}, replyToBot={ctx.IsReplyToBot}, nickname={ctx.ContainsNickname}, question={ctx.HasQuestion}, imageOnly={ctx.IsImageOnly}");
            await next();

            if (ctx.Result == EventHandleResult.Block)
            {
                replyManager.AfterSend();
            }
            else
            {
                replyManager.AfterSkip();
            }
        });
    }

    public static ChatPipelineBuilder UseChatHandler(this ChatPipelineBuilder builder)
    {
        return builder
            .UseBackgroundCounters()
            .UseChatExecutor()
            .UseBotMessageRecorder()
            .UseToolCallRecorder();
    }

    public static ChatPipelineBuilder UseMessageImageResolver(this ChatPipelineBuilder builder)
    {
        return builder.Use(async (ctx, next) =>
        {
            var msg = GetMessage(ctx);
            var images = msg?.MessageChain?.OfType<Image>().ToList();
            if (images is not { Count: > 0 })
            {
                ctx.Trace("ImageResolver", true, "无图片，跳过图片解析");
                await next();
                return;
            }

            // ── Decision: should we describe images? ──
            var shouldDescribe = Effective(ctx, "EnableVision", AppConfig.EnableVision)
                && (!Effective(ctx, "EnableVisionWhenMentioned", AppConfig.EnableVisionWhenMentioned) || ctx.IsMentioned);

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(ctx.MessageText))
            {
                parts.Add(ctx.MessageText);
            }

            foreach (var image in images)
            {
                if (!shouldDescribe || (Effective(ctx, "IgnoreNotEmoji", AppConfig.IgnoreNotEmoji) && !image.IsEmoji))
                {
                    parts.Add("[图片]");
                    continue;
                }

                // Resolve file path
                var filePath = await ImageScraper.ResolvePathAsync(image.FilePath, image.Hash);
                if (filePath == null)
                {
                    parts.Add("[图片]");
                    continue;
                }

                // Choose prompt based on emoji status, describe
                var extraPrompt = image.IsEmoji ? ImageScraper.EmojiPrompt : null;
                var description = await ImageScraper.DescribeAsync(filePath, extraPrompt, image.IsEmoji);

                if (description != null)
                {
                    var type = image.IsEmoji ? "表情包" : "图片";
                    parts.Add($"[{type} hash:{image.Hash};描述:{description}]");
                }
                else
                {
                    parts.Add("[图片]");
                }
            }

            ctx.MessageText = string.Join("\n", parts);
            ctx.Trace("ImageResolver", true, $"已处理 {images.Count} 张图片，vision={(shouldDescribe ? "on" : "off")}");

            await next();
        });
    }

    public static ChatPipelineBuilder UseMessageAtResolver(this ChatPipelineBuilder builder)
    {
        return builder.Use(async (ctx, next) =>
        {
            var msg = GetMessage(ctx);
            var atItems = msg?.MessageChain?.OfType<At>().ToList();
            if (atItems is not { Count: > 0 })
            {
                ctx.Trace("AtResolver", true, "无 @ 元素，跳过解析");
                await next();
                return;
            }

            var botQQ = PromptBuilder.CurrentBotQQ;
            var atTexts = new List<string>();
            foreach (var at in atItems)
            {
                if (at.AllTarget)
                {
                    atTexts.Add("[@全体成员]");
                }
                else if (at.Target == botQQ)
                {
                    atTexts.Add("[@Bot]");
                }
                else
                {
                    var nick = TryGetNickOrCard(ctx.GroupId, at.Target);
                    var label = nick ?? at.Target.ToString();
                    atTexts.Add($"[@{label}]");
                }
            }

            var existingText = ctx.MessageText ?? "";
            ctx.MessageText = string.Join(" ", atTexts) + (existingText.Length > 0 ? " " + existingText : "");
            ctx.Trace("AtResolver", true, $"已解析 {atItems.Count} 个 @ 元素");

            await next();
        });
    }

    private static string? TryGetNickOrCard(long groupId, long qq)
    {
        try
        {
            var member = Entry.ApiGroup?.GetGroupMemberInfo(groupId, qq);
            if (member != null)
            {
                return !string.IsNullOrWhiteSpace(member.Card) ? member.Card : member.Nick;
            }

            var friend = Entry.ApiFriend?.GetFriendInfos()?.FirstOrDefault(f => f.QQ == qq);
            return friend?.Nick;
        }
        catch
        {
            return null;
        }
    }

    public static ChatPipelineBuilder UseMessageReferenceResolver(this ChatPipelineBuilder builder)
    {
        return builder.Use(async (ctx, next) =>
        {
            var msg = GetMessage(ctx);
            if (msg?.MessageChain != null)
            {
                var currentText = ctx.MessageText;
                var replyItems = msg.MessageChain.OfType<Reply>().ToList();
                if (replyItems.Count > 0)
                {
                    var botQQ = PromptBuilder.CurrentBotQQ;
                    foreach (var reply in replyItems)
                    {
                        var records = ChatRecord.GetByIds([reply.Id], ctx.GroupId);
                        var quoted = records.FirstOrDefault();
                        if (quoted != null && quoted.QQ == botQQ)
                        {
                            currentText = $"[用户引用了你之前说过的话]\n你: {quoted.ParsedMessage}\n[用户现在说]\n{currentText}";
                        }
                    }

                    ctx.MessageText = currentText;
                    ctx.Trace("MessageReferenceResolver", true, $"处理了 {replyItems.Count} 条引用消息");
                }
            }
            else
            {
                ctx.Trace("MessageReferenceResolver", true, "无可解析的引用消息");
            }

            await next();
        });
    }

    public static ChatPipelineBuilder UseMessageRecorder(this ChatPipelineBuilder builder)
    {
        return builder.Use(async (ctx, next) =>
        {
            try
            {
                var msg = GetMessage(ctx);
                var rawText = msg?.Text ?? "";
                var parsedText = ctx.MessageText ?? "";

                ChatRecord.Insert(new ChatRecord
                {
                    GroupID = ctx.GroupId,
                    QQ = ctx.QQ,
                    NickName = ctx.QQ.ToString(),
                    Message = rawText,
                    ParsedMessage = parsedText,
                    SenderType = SenderType.User,
                    MessageID = msg.Id,
                    Time = DateTime.Now,
                    IsMentioned = ctx.IsMentioned,
                    IsImage = ctx.IsImageOnly,
                    IsEmpty = !ctx.IsImageOnly && string.IsNullOrWhiteSpace(parsedText)
                });
            }
            catch (Exception ex)
            {
                CommonHelper.LogError?.Invoke("Record", ex.Message);
                ctx.Trace("MessageRecorder", false, $"记录消息失败：{ex.Message}");
            }
            if (!ctx.PipelineTrace.Any(t => t.Step == "MessageRecorder" && !t.Passed))
            {
                ctx.Trace("MessageRecorder", true, "消息记录完成");
            }

            await next();
        });
    }

    public static ChatPipelineBuilder UseBackgroundCounters(this ChatPipelineBuilder builder)
    {
        return builder.Use(async (ctx, next) =>
        {
            DiaryMemoryManager.OnMessageProcessed(ctx.GroupId);
            ContextCompressor.OnMessageProcessed(ctx.GroupId);
            ctx.Trace("BackgroundCounters", true, "后台计数与压缩触发完成");
            await next();
        });
    }

    public static ChatPipelineBuilder UseChatExecutor(this ChatPipelineBuilder builder)
    {
        return builder.Use(async (ctx, next) =>
        {
            ctx.Result = await DoChatAsync(ctx);
            ctx.Trace("ChatExecutor", ctx.Result == EventHandleResult.Block,
                ctx.Result == EventHandleResult.Block ? "聊天执行并发送完成" : "聊天执行未产出可发送回复");
            await next();
        });
    }

    public static ChatPipelineBuilder UseBotMessageRecorder(this ChatPipelineBuilder builder)
    {
        return builder.Use(async (ctx, next) =>
        {
            await next();

            var hasFallback = !string.IsNullOrWhiteSpace(ctx.FallbackReply);
            var hasSegments = ctx.BotReplies.Count > 0;

            if (!hasFallback && !hasSegments)
            {
                return;
            }

            try
            {
                if (hasFallback)
                {
                    ChatRecord.Insert(new ChatRecord
                    {
                        GroupID = ctx.GroupId,
                        QQ = PromptBuilder.CurrentBotQQ,
                        NickName = Effective(ctx, "BotName", AppConfig.BotName),
                        Message = ctx.FallbackReply,
                        ParsedMessage = ctx.FallbackReply,
                        SenderType = SenderType.Assistant,
                        Time = DateTime.Now
                    });
                }
                else
                {
                    foreach (var (text, time) in ctx.BotReplies)
                    {
                        ChatRecord.Insert(new ChatRecord
                        {
                            GroupID = ctx.GroupId,
                            QQ = PromptBuilder.CurrentBotQQ,
                            NickName = Effective(ctx, "BotName", AppConfig.BotName),
                            Message = text,
                            ParsedMessage = text,
                            SenderType = SenderType.Assistant,
                            Time = time
                        });
                    }
                }

                ChatRecord.Cleanup(ctx.GroupId);
            }
            catch (Exception ex)
            {
                CommonHelper.LogError?.Invoke("RecordBot", ex.Message);
            }
        });
    }

    public static ChatPipelineBuilder UseToolCallRecorder(this ChatPipelineBuilder builder)
    {
        return builder.Use(async (ctx, next) =>
        {
            await next();

            if (ctx.PendingToolCalls == null || ctx.PendingToolCalls.Count == 0)
            {
                return;
            }

            try
            {
                ChatRecord.Insert(new ChatRecord
                {
                    GroupID = ctx.GroupId,
                    QQ = PromptBuilder.CurrentBotQQ,
                    NickName = Effective(ctx, "BotName", AppConfig.BotName),
                    SenderType = SenderType.Assistant,
                    ParsedMessage = string.Empty,
                    HasToolCalls = true,
                    Time = DateTime.Now
                });

                var placeholderIds = new List<int>();
                foreach (var tc in ctx.PendingToolCalls)
                {
                    var id = ChatRecord.Insert(new ChatRecord
                    {
                        GroupID = ctx.GroupId,
                        QQ = ctx.QQ,
                        SenderType = SenderType.Tool,
                        ParsedMessage = $"[{tc.name}]...",
                        ToolCallId = tc.callId,
                        ToolName = tc.name,
                        IsToolSuccess = tc.success,
                        Time = DateTime.Now
                    });
                    placeholderIds.Add(id);
                }

                ToolResultSummarizer.SummarizeAsync(
                    ctx.GroupId, ctx.MessageText,
                    ctx.PendingToolCalls, string.Join(" ", ctx.BotReplies.Select(r => r.text)), placeholderIds);
            }
            catch (Exception ex)
            {
                CommonHelper.LogError?.Invoke("ToolRecorder", ex.Message);
            }
        });
    }

    // ── Shared helpers ────────────────────────────────────

    private static Message? GetMessage(ChatContext ctx)
        => ctx.IsGroup ? ctx.GroupCtx?.Message : ctx.PrivateCtx?.Message;

    private static bool CheckAtBot(Message? msg)
    {
        if (msg?.MessageChain == null)
        {
            return false;
        }

        var botQQ = PromptBuilder.CurrentBotQQ;
        return msg.MessageChain.OfType<At>().Any(a => a.Target == botQQ || a.AllTarget);
    }

    private static bool CheckNickname(ChatContext ctx, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var nicknames = Effective(ctx, "BotNicknames", AppConfig.BotNicknames);
        return nicknames.Any(n =>
            !string.IsNullOrEmpty(n) && text.Contains(n, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasImage(ChatContext ctx)
        => GetMessage(ctx)?.MessageChain?.OfType<Image>().Any() ?? false;

    private static bool CheckReplyToBot(Message? msg, long groupId)
    {
        if (msg?.MessageChain == null)
        {
            return false;
        }

        var replyItems = msg.MessageChain.OfType<Reply>().ToList();
        if (replyItems.Count == 0)
        {
            return false;
        }

        var botQQ = PromptBuilder.CurrentBotQQ;
        foreach (var reply in replyItems)
        {
            var records = ChatRecord.GetByIds([reply.Id], groupId);
            if (records.Any(r => r.QQ == botQQ))
            {
                return true;
            }
        }
        return false;
    }

    private static async Task<EventHandleResult> DoChatAsync(ChatContext ctx)
    {
        var keys = AppConfig.ChatAPIKeyId;
        if (keys.Count == 0)
        {
            ctx.Trace("ChatPreparation", false, "未配置 ChatAPIKey");
            return EventHandleResult.Pass;
        }

        var botQQ = PromptBuilder.CurrentBotQQ;
        var groupCfg = ctx.GroupConfig;
        var effectivePrompt = groupCfg?.GetConfigValue<string>("GroupPrompt") ?? AppConfig.GroupPrompt;
        var effectiveNicknames = string.Join(",", groupCfg?.GetConfigValue<List<string>>("BotNicknames") ?? AppConfig.BotNicknames);

        var systemPrompt = PromptBuilder.BuildSystemPrompt(
            Effective(ctx, "BotName", AppConfig.BotName),
            effectiveNicknames, botQQ,
            Effective(ctx, "ChatEmptyResponse", AppConfig.ChatEmptyResponse),
            string.Join(",", Effective(ctx, "MasterQQ", AppConfig.MasterQQ)),
            effectivePrompt);

        var history = ChatRecord.GetGroupHistory(ctx.GroupId, Effective(ctx, "ContextMaxLength", AppConfig.ContextMaxLength));

        var knowledge = MemoryManager.GetKnowledge(ctx.MessageText);

        var moodText = MoodState.GetMood(ctx.GroupId);
        var scheduleText = Effective(ctx, "EnableSchedules", AppConfig.EnableSchedules)
            ? SchedulerManager.Instance?.GetCurrentSchedule(DateTime.Now)
            : null;
        var diaryText = DiaryMemoryManager.GetDiaryContext(ctx.GroupId);

        var dynamicContent = PromptBuilder.BuildDynamicUserContent(
            moodText, scheduleText,
            knowledge.Select(k => k.text).ToList(),
            diaryText,
            effectivePrompt);

        var last = history.Last();
        dynamicContent += $"[{last.Time:HH:mm}]{last.NickName}[{last.QQ}]: {last.ParsedMessage}";

        var messages = PromptBuilder.BuildRequestBody(systemPrompt,
            history.Take(history.Count - 1).ToList(), dynamicContent);

        // ── Inject pending images from AddPictureToContext (native multimodal) ──
        if (ctx.PendingImageHashes.Count > 0 && messages.Count > 0)
        {
            var lastMsg = messages.Last();
            var parts = new List<ContentPart>();
            if (lastMsg.Content is string text && !string.IsNullOrWhiteSpace(text))
            {
                parts.Add(ContentPart.FromText(text));
            }

            foreach (var hash in ctx.PendingImageHashes)
            {
                var pic = Picture.FindByHash(hash);
                if (pic == null)
                {
                    continue;
                }

                var path = pic.FilePath;
                if (!File.Exists(path))
                {
                    var alt = Path.Combine(CommonHelper.GetAppImageDirectory(), pic.FilePath);
                    if
                        (File.Exists(alt))
                    {
                        path = alt;
                    }
                    else
                    {
                        continue;
                    }
                }
                parts.Add(ContentPart.FromImageFile(path));
            }

            if (parts.Count > 1) // has image parts beyond the original text
            {
                lastMsg.Parts = parts;
                lastMsg.Content = null;
            }
            ctx.PendingImageHashes.Clear();
        }

        // ── MCP tool setup ──
        ToolExecutor? toolExecutor = null;
        if (Effective(ctx, "EnableMCP", AppConfig.EnableMCP))
        {
            var mcpCtx = new MCPToolContext
            {
                GroupId = ctx.GroupId,
                QQ = ctx.QQ,
                ChatIdentity = $"group_{ctx.GroupId}",
                PendingImageHashes = ctx.PendingImageHashes  // share the list
            };
            foreach (var c in MCPClientManager.Clients.OfType<MCPCustomClient>())
            {
                c.Context = mcpCtx;
            }

            toolExecutor = new ToolExecutor(
                () => MCPClientManager.GetToolsForConversation(mcpCtx).ToList(),
                async (tc, ct2) => await MCPClientManager.ExecuteToolAsync(tc, ct2, mcpCtx));
        }

        var chatService = new ChatService();
        var response = await chatService.GetChatResultAsync(
            keys, messages, ChatService.Purpose.聊天,
            timeout: Effective(ctx, "ChatTimeout", AppConfig.ChatTimeout), toolExecutor: toolExecutor,
            cancellationToken: ctx.CancellationToken,
            onIntermediateText: async text =>
            {
                if (ctx.SendFunc != null)
                {
                    await ctx.SendFunc(text);
                    ctx.BotReplies.Add((text, DateTime.Now));
                }
            });

        ctx.Reasoning = chatService.LastReasoning;

        if (response == ChatService.ErrorMessage)
        {
            ctx.Trace("ChatService", false, "聊天服务返回错误消息");
            if (ctx.IsMentioned)
            {
                await HandleFallback(ctx, systemPrompt, keys, chatService, response);
            }

            return EventHandleResult.Pass;
        }

        var abnormalReason = chatService.LastAbnormalFinishReason;
        if (ctx.IsMentioned && !string.IsNullOrWhiteSpace(abnormalReason) && string.IsNullOrWhiteSpace(response))
        {
            ctx.Trace("ChatService", false, $"回复被异常终止：{abnormalReason}");
            await HandleFallback(ctx, systemPrompt, keys, chatService, response);
            return EventHandleResult.Block;
        }

        if (response.Contains(Effective(ctx, "ChatEmptyResponse", AppConfig.ChatEmptyResponse)))
        {
            response = response.Replace(Effective(ctx, "ChatEmptyResponse", AppConfig.ChatEmptyResponse), "").Trim();
            if (string.IsNullOrWhiteSpace(response))
            {
                ctx.Trace("ChatService", false, "模型选择保持沉默");
                return EventHandleResult.Pass;
            }
        }

        if (!string.IsNullOrWhiteSpace(response) && IsNearDuplicate(response, ctx.GroupId))
        {
            ctx.Trace("ChatService", false, "回复与最近消息高度重复，被去重");
            return EventHandleResult.Pass;
        }

        if (!string.IsNullOrWhiteSpace(response))
        {
            var sentText = await SendReplyWithEmoji(ctx, response);
            ctx.Trace("ChatService", true, $"生成回复长度 {sentText.Length}");
        }

        // Tool call log — store on context for UseToolCallRecorder middleware
        if (chatService.ToolCallLog.Count > 0)
        {
            ctx.PendingToolCalls = chatService.ToolCallLog;
        }

        return EventHandleResult.Block;
    }

    private static async Task HandleFallback(
        ChatContext ctx, string systemPrompt, List<APIKeyPurpose> keys,
        ChatService chatService, string response)
    {
        var abnormalReason = chatService.LastAbnormalFinishReason;
        if (Effective(ctx, "UseLLMContentFilterFallback", AppConfig.UseLLMContentFilterFallback))
        {
            var hint = abnormalReason switch
            {
                "content_filter" => "因内容过滤被拦截",
                "length" => "因输出达到长度限制被截断",
                "insufficient_system_resource" => "因系统资源不足被中断",
                _ => $"因未知原因({abnormalReason})被中断"
            };
            var deflectionMessages = new List<ChatMessage>
            {
                ChatMessage.System(systemPrompt + $"\n\n[系统] 你的上一条回复{hint}，请自然地继续对话。"),
                ChatMessage.User(ctx.MessageText)
            };
            var deflectionService = new ChatService();
            var deflection = await deflectionService.GetChatResultAsync(
                keys, deflectionMessages, ChatService.Purpose.聊天,
                timeout: Effective(ctx, "ChatTimeout", AppConfig.ChatTimeout));
            if (deflection != ChatService.ErrorMessage && !string.IsNullOrWhiteSpace(deflection))
            {
                await ctx.SendFunc!(deflection);
                ctx.FallbackReply = deflection;
            }
        }
        else if (Effective(ctx, "ContentFilterFallbacks", AppConfig.ContentFilterFallbacks).Count > 0)
        {
            var fallbacks = Effective(ctx, "ContentFilterFallbacks", AppConfig.ContentFilterFallbacks);
            var fallback = fallbacks[CommonHelper.Next(0, fallbacks.Count)];
            await ctx.SendFunc!(fallback);
            ctx.FallbackReply = fallback;
        }
    }

    private static bool IsNearDuplicate(string candidate, long groupId)
    {
        var recentBotMessages = ChatRecord.GetGroupHistory(groupId, 5)
            .Where(r => r.SenderType == SenderType.Assistant)
            .Select(r => r.ParsedMessage)
            .ToList();
        if (recentBotMessages.Count == 0 || candidate.Length < 6)
        {
            return false;
        }

        foreach (var recent in recentBotMessages)
        {
            if (recent.Length < 6)
            {
                continue;
            }

            if (LevenshteinSimilarity(candidate, recent) >= 0.85)
            {
                return true;
            }
        }
        return false;
    }

    private static double LevenshteinSimilarity(string a, string b)
    {
        int maxLen = Math.Max(a.Length, b.Length);
        if (maxLen == 0)
        {
            return 1.0;
        }

        int distance = LevenshteinDistance(a, b);
        return 1.0 - ((double)distance / maxLen);
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        if (a.Length < b.Length)
        {
            (a, b) = (b, a);
        }

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++)
        {
            prev[j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(prev[j] + 1, curr[j - 1] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[b.Length];
    }

    /// <summary>
    /// Splits the LLM response on <@Emoji{description}> markers, resolves each
    /// emoji to a matching image, and sends text/images as separate messages.
    /// </summary>
    private static async Task<string> SendReplyWithEmoji(ChatContext ctx, string response)
    {
        var activeSend = Effective(ctx, "EnableEmojiActiveSend", AppConfig.EnableEmojiActiveSend) && response.Contains("<@Emoji");
        var displayText = new System.Text.StringBuilder();

        // ── Level 1: Splitter for pacing ──
        var segments = Effective(ctx, "EnableSplitter", AppConfig.EnableSplitter)
            ? new Splitter(response).Split()
            : [response];

        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                continue;
            }

            // ── Level 2: SplitEmoji within each segment ──
            var parts = Splitter.SplitEmoji(segment);
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];

                if (part.isEmoji)
                {
                    if (activeSend)
                    {
                        await SendEmojiImage(ctx, part.content, displayText);
                    }
                    // else: silently drop
                }
                else
                {
                    var text = part.content.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        await ctx.SendFunc!(text);
                        ctx.BotReplies.Add((text, DateTime.Now));
                        displayText.Append(text + ' ');
                    }
                }

                if (i < parts.Length - 1)
                {
                    var delayText = part.isEmoji ? "" : part.content;
                    await Splitter.ApplyTypingDelay(delayText, ctx.CancellationToken);
                }
            }
        }

        return displayText.ToString().Trim();
    }

    private static async Task SendEmojiImage(ChatContext ctx, string emotion, System.Text.StringBuilder displayText)
    {
        var matches = await Picture.GetRecommendEmojiAsync(emotion, 3);
        if (matches.Count > 0)
        {
            var match = matches[0];
            var relativePath = CommonHelper.GetRelativePath(
                match.picture.FilePath, CommonHelper.GetAppImageDirectory());
            if (string.IsNullOrEmpty(relativePath))
            {
                relativePath = match.picture.FilePath;
            }

            var msg = new Another_Mirai_Native.Abstractions.Models.MessageBuilder()
                .Image(relativePath)
                .Build();
            await ctx.SendFunc!(msg.ToString());
            displayText.Append($"\n[表情包: {match.picture.Description}]");
        }
        else
        {
            displayText.Append($"\n[表情包未找到: {emotion}]");
        }
    }
}