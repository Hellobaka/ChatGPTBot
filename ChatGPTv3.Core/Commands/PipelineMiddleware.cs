using Another_Mirai_Native.Abstractions.Enums;
using Another_Mirai_Native.Abstractions.Models;
using Another_Mirai_Native.Abstractions.Models.MessageItem;
using ChatGPTv3.Core.Api;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Model;
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
                { ctx.Result = EventHandleResult.Pass; return; }
            }
            else
            {
                if (!AppConfig.PersonList.Contains(ctx.QQ))
                { ctx.Result = EventHandleResult.Pass; return; }
            }
            await next();
        });
    }

    public static ChatPipelineBuilder UsePrivateReplyDecision(this ChatPipelineBuilder builder)
    {
        return builder.Use(async (ctx, next) =>
        {
            ctx.IsMentioned = true; // private — always respond
            await next();
        });
    }

    public static ChatPipelineBuilder UseAccessControl(this ChatPipelineBuilder builder)
    {
        return builder.Use(async (ctx, next) =>
        {
            if (AppConfig.IsGroupBlackList)
            {
                if (AppConfig.GroupList.Contains(ctx.GroupId))
                { ctx.Result = EventHandleResult.Pass; return; }
            }
            else
            {
                if (!AppConfig.GroupList.Contains(ctx.GroupId))
                { ctx.Result = EventHandleResult.Pass; return; }
            }
            await next();
        });
    }

    public static ChatPipelineBuilder UseMessageFilter(this ChatPipelineBuilder builder)
    {
        return builder.Use(async (ctx, next) =>
        {
            if (string.IsNullOrWhiteSpace(ctx.MessageText) && !HasImage(ctx))
            { ctx.Result = EventHandleResult.Pass; return; }
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
                var debounceMs = AppConfig.MessageDebounceMs;
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
                    ctx.Result = EventHandleResult.Pass;
                    return;
                }

                ctx.CancellationToken = cts.Token;
                await next();
            }
            catch (OperationCanceledException)
            {
                CommonHelper.DebugLog("Pipeline", $"已打断 {key}");
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
            ctx.ContainsNickname = CheckNickname(ctx.MessageText);
            ctx.IsImageOnly = HasImage(ctx) && string.IsNullOrWhiteSpace(ctx.MessageText);
            ctx.IsReplyToBot = CheckReplyToBot(GetMessage(ctx), ctx.GroupId);
            ctx.HasQuestion = ctx.MessageText.Contains('?') || ctx.MessageText.Contains('？');

            var replyManager = ReplyManager.Get(ctx.GroupId);
            bool fromSamePerson = ctx.QQ == replyManager.LastReplyQQ;
            ctx.ReplyProbability = replyManager.UpdateWillingness(
                ctx.IsMentioned, ctx.IsReplyToBot, ctx.ContainsNickname,
                ctx.HasQuestion, fromSamePerson, ctx.IsImageOnly, ctx.QQ);

            // LLM-based check
            if (AppConfig.EnableLLMCheckShouldResponse && !ctx.IsMentioned)
            {
                (_, ctx.ReplyProbability) = await ReplyManager.CheckByLLM(
                    AppConfig.BotName, AppConfig.BotNicknames, [ctx.MessageText]);
            }

            // Random check
            if (CommonHelper.NextDouble() >= ctx.ReplyProbability)
            {
                replyManager.AfterSkip();
                ctx.Result = EventHandleResult.Pass;
                return;
            }

            await next();

            if (ctx.Result == EventHandleResult.Block)
                replyManager.AfterSend();
            else
                replyManager.AfterSkip();
        });
    }

    public static ChatPipelineBuilder UseChatHandler(this ChatPipelineBuilder builder)
    {
        return builder.Use(async (ctx, next) =>
        {
            ctx.MessageText = await ResolveImages(ctx, ctx.MessageText);
            ctx.MessageText = ResolveReferences(ctx);
            RecordMessage(ctx);

            // Increment background counters (diary + compression)
            DiaryMemoryManager.OnMessageProcessed(ctx.GroupId);
            ContextCompressor.OnMessageProcessed(ctx.GroupId);

            var result = await DoChatAsync(ctx);
            ctx.Result = result;
        });
    }

    // ── Shared helpers ────────────────────────────────────

    private static Message? GetMessage(ChatContext ctx)
        => ctx.IsGroup ? ctx.GroupCtx?.Message : ctx.PrivateCtx?.Message;

    private static bool CheckAtBot(Message? msg)
    {
        if (msg?.MessageChain == null) return false;
        var botQQ = PromptBuilder.CurrentBotQQ;
        return msg.MessageChain.OfType<At>().Any(a => a.Target == botQQ || a.AllTarget);
    }

    private static bool CheckNickname(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return AppConfig.BotNicknames.Any(n =>
            !string.IsNullOrEmpty(n) && text.Contains(n, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasImage(ChatContext ctx)
        => GetMessage(ctx)?.MessageChain?.OfType<Image>().Any() ?? false;

    private static bool CheckReplyToBot(Message? msg, long groupId)
    {
        if (msg?.MessageChain == null) return false;
        var replyItems = msg.MessageChain.OfType<Reply>().ToList();
        if (replyItems.Count == 0) return false;
        var botQQ = PromptBuilder.CurrentBotQQ;
        foreach (var reply in replyItems)
        {
            var records = ChatRecord.GetByIds([reply.Id], groupId);
            if (records.Any(r => r.QQ == botQQ)) return true;
        }
        return false;
    }

    private static async Task<string> ResolveImages(ChatContext ctx, string currentText)
    {
        var msg = GetMessage(ctx);
        var images = msg?.MessageChain?.OfType<Image>().ToList();
        if (images == null || images.Count == 0) return currentText;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(currentText))
            parts.Add(currentText);

        foreach (var image in images)
        {
            var desc = await ImageScraper.DescribeAsync(image, ctx.IsMentioned);
            if (desc != null)
                parts.Add(desc);
            else
                parts.Add("[图片]");
        }

        return string.Join("\n", parts);
    }

    private static string ResolveReferences(ChatContext ctx)
    {
        var msg = GetMessage(ctx);
        if (msg?.MessageChain == null) return ctx.MessageText;
        var currentText = ctx.MessageText;
        var groupId = ctx.GroupId;
        var replyItems = msg.MessageChain.OfType<Reply>().ToList();
        if (replyItems.Count == 0) return currentText;
        var botQQ = PromptBuilder.CurrentBotQQ;
        foreach (var reply in replyItems)
        {
            var records = ChatRecord.GetByIds([reply.Id], groupId);
            var quoted = records.FirstOrDefault();
            if (quoted != null && quoted.QQ == botQQ)
            {
                currentText = $"[用户引用了你之前说过的话]\n你: {quoted.ParsedMessage}\n[用户现在说]\n{currentText}";
            }
        }
        return currentText;
    }

    private static void RecordMessage(ChatContext ctx)
    {
        try
        {
            var msg = GetMessage(ctx);
            var rawText = msg.Text ?? "";
            var parsedText = string.Join("", msg.MessageChain?
                .Where(i => i is Text)
                .Select(i => ((Text)i).Content) ?? [rawText]);

            ChatRecord.Insert(new ChatRecord
            {
                GroupID = ctx.GroupId, QQ = ctx.QQ,
                NickName = ctx.QQ.ToString(),
                Message = rawText, ParsedMessage = parsedText,
                SenderType = SenderType.User,
                MessageID = msg.Id, Time = DateTime.Now,
                IsMentioned = ctx.IsMentioned,
                IsImage = ctx.IsImageOnly,
                IsEmpty = !ctx.IsImageOnly && string.IsNullOrWhiteSpace(parsedText)
            });
        }
        catch (Exception ex)
        { CommonHelper.LogError?.Invoke("Record", ex.Message); }
    }

    private static async Task<EventHandleResult> DoChatAsync(ChatContext ctx)
    {
        var keys = AppConfig.ChatAPIKeyId;
        if (keys.Count == 0) return EventHandleResult.Pass;

        var botQQ = PromptBuilder.CurrentBotQQ;
        var groupCfg = GroupConfig.Get(ctx.GroupId);
        var effectivePrompt = groupCfg?.CustomPrompt ?? AppConfig.GroupPrompt;
        var effectiveNicknames = groupCfg?.CustomNicknames ?? string.Join(",", AppConfig.BotNicknames);

        var systemPrompt = PromptBuilder.BuildSystemPrompt(
            AppConfig.BotName, effectiveNicknames, ctx.QQ,
            AppConfig.ChatEmptyResponse, string.Join(",", AppConfig.MasterQQ), effectivePrompt);

        var history = ChatRecord.GetGroupHistory(ctx.GroupId, AppConfig.ContextMaxLength);

        var knowledge = MemoryManager.GetKnowledge(ctx.MessageText);

        var moodText = MoodState.GetMood(ctx.GroupId);
        var scheduleText = AppConfig.EnableSchedules
            ? SchedulerManager.Instance?.GetCurrentSchedule(DateTime.Now)
            : null;
        var diaryText = DiaryMemoryManager.GetDiaryContext(ctx.GroupId);

        var dynamicContent = PromptBuilder.BuildDynamicUserContent(
            moodText, scheduleText,
            knowledge.Select(k => k.text).ToList(),
            diaryText,
            effectivePrompt);

        var historyText = string.Join("\n",
            history.Select(r => $"[{r.Time:HH:mm}]{r.NickName}[{r.QQ}]: {r.ParsedMessage}"));
        dynamicContent += historyText;

        var messages = PromptBuilder.BuildRequestBody(systemPrompt,
            history.Take(history.Count - 1).ToList(), dynamicContent);

        var chatService = new ChatService();
        var response = await chatService.GetChatResultAsync(
            keys, messages, ChatService.Purpose.聊天,
            timeout: AppConfig.ChatTimeout, cancellationToken: ctx.CancellationToken);

        if (response == ChatService.ErrorMessage)
        {
            if (ctx.IsMentioned) await HandleFallback(ctx, systemPrompt, keys, chatService, response);
            return EventHandleResult.Pass;
        }

        var abnormalReason = chatService.LastAbnormalFinishReason;
        if (ctx.IsMentioned && !string.IsNullOrWhiteSpace(abnormalReason) && string.IsNullOrWhiteSpace(response))
        {
            await HandleFallback(ctx, systemPrompt, keys, chatService, response);
            return EventHandleResult.Block;
        }

        if (response.Contains(AppConfig.ChatEmptyResponse))
        {
            response = response.Replace(AppConfig.ChatEmptyResponse, "").Trim();
            if (string.IsNullOrWhiteSpace(response))
                return EventHandleResult.Pass;
        }

        if (!string.IsNullOrWhiteSpace(response) && IsNearDuplicate(response, ctx.GroupId))
            return EventHandleResult.Pass;

        if (!string.IsNullOrWhiteSpace(response))
        {
            await ctx.SendFunc!(response);
            RecordBotMessage(ctx.GroupId, response);
        }

        // Tool call chain recording
        if (chatService.ToolCallLog.Count > 0)
        {
            ChatRecord.Insert(new ChatRecord
            {
                GroupID = ctx.GroupId, QQ = PromptBuilder.CurrentBotQQ,
                NickName = AppConfig.BotName, SenderType = SenderType.Assistant,
                ParsedMessage = string.Empty, HasToolCalls = true, Time = DateTime.Now
            });

            var placeholderIds = new List<int>();
            foreach (var tc in chatService.ToolCallLog)
            {
                var id = ChatRecord.Insert(new ChatRecord
                {
                    GroupID = ctx.GroupId, QQ = ctx.QQ,
                    SenderType = SenderType.Tool,
                    ParsedMessage = $"[{tc.name}]...",
                    ToolCallId = tc.callId, ToolName = tc.name,
                    IsToolSuccess = tc.success, Time = DateTime.Now
                });
                placeholderIds.Add(id);
            }

            ToolResultSummarizer.SummarizeAsync(
                ctx.GroupId, ctx.MessageText,
                chatService.ToolCallLog, response, placeholderIds);
        }

        return EventHandleResult.Block;
    }

    private static async Task HandleFallback(
        ChatContext ctx, string systemPrompt, List<APIKeyPurpose> keys,
        ChatService chatService, string response)
    {
        var abnormalReason = chatService.LastAbnormalFinishReason;
        if (AppConfig.UseLLMContentFilterFallback)
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
                timeout: AppConfig.ChatTimeout);
            if (deflection != ChatService.ErrorMessage && !string.IsNullOrWhiteSpace(deflection))
            {
                await ctx.SendFunc!(deflection);
                RecordBotMessage(ctx.GroupId, deflection);
            }
        }
        else if (AppConfig.ContentFilterFallbacks.Count > 0)
        {
            var fallback = AppConfig.ContentFilterFallbacks[
                CommonHelper.Next(0, AppConfig.ContentFilterFallbacks.Count)];
            await ctx.SendFunc!(fallback);
            RecordBotMessage(ctx.GroupId, fallback);
        }
    }

    private static void RecordBotMessage(long groupId, string message)
    {
        try
        {
            ChatRecord.Insert(new ChatRecord
            {
                GroupID = groupId, QQ = PromptBuilder.CurrentBotQQ,
                NickName = AppConfig.BotName, Message = message,
                ParsedMessage = message, SenderType = SenderType.Assistant, Time = DateTime.Now
            });
            ChatRecord.Cleanup(groupId);
        }
        catch (Exception ex) { CommonHelper.LogError?.Invoke("RecordBot", ex.Message); }
    }

    private static bool IsNearDuplicate(string candidate, long groupId)
    {
        var recentBotMessages = ChatRecord.GetGroupHistory(groupId, 5)
            .Where(r => r.SenderType == SenderType.Assistant)
            .Select(r => r.ParsedMessage)
            .ToList();
        if (recentBotMessages.Count == 0 || candidate.Length < 6) return false;
        foreach (var recent in recentBotMessages)
        {
            if (recent.Length < 6) continue;
            if (LevenshteinSimilarity(candidate, recent) >= 0.85) return true;
        }
        return false;
    }

    private static double LevenshteinSimilarity(string a, string b)
    {
        int maxLen = Math.Max(a.Length, b.Length);
        if (maxLen == 0) return 1.0;
        int distance = LevenshteinDistance(a, b);
        return 1.0 - (double)distance / maxLen;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        if (a.Length < b.Length) (a, b) = (b, a);
        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) prev[j] = j;
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
}
