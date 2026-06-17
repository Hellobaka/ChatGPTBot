using Another_Mirai_Native.Abstractions.Context;
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
/// Shared chat pipeline logic — used by both GroupChatHandler (direct IGroupMessageHandler)
/// and ChatCommands (CommandHandlerBase fallback).
/// </summary>
public static class ChatPipeline
{
    private static readonly Dictionary<long, bool> _busyLocks = new();

    public static async Task<EventHandleResult> ProcessGroupAsync(
        GroupMessageContext e, CancellationToken ct)
    {
        // ── Group whitelist/blacklist filter (v2 parity) ──
        if (AppConfig.IsGroupBlackList)
        {
            if (AppConfig.GroupList.Contains(e.FromGroup.Id))
                return EventHandleResult.Pass;
        }
        else
        {
            if (!AppConfig.GroupList.Contains(e.FromGroup.Id))
                return EventHandleResult.Pass;
        }

        var groupId = e.FromGroup.Id;
        var qq = e.FromQQ.Id;
        var messageText = e.Message.Text;

        if (string.IsNullOrWhiteSpace(messageText) && !HasImage(e.Message))
            return EventHandleResult.Pass;

        try
        {
            if (!TrySetBusy(groupId)) return EventHandleResult.Pass;
            try
            {
                // ── Parse triggers ──
                bool isMentioned = CheckAtBot(e.Message);
                bool containsNickname = CheckNickname(messageText);
                bool isImageOnly = HasImage(e.Message) && string.IsNullOrWhiteSpace(messageText);
                bool isReplyToBot = CheckReplyToBot(e.Message, groupId);
                bool hasQuestion = messageText.Contains('?') || messageText.Contains('？');

                // ── Reply probability ──
                var replyManager = ReplyManager.Get(groupId);
                bool fromSamePerson = qq == replyManager.LastReplyQQ;
                double replyProbability = replyManager.UpdateWillingness(
                    isMentioned, isReplyToBot, containsNickname,
                    hasQuestion, fromSamePerson, isImageOnly, qq);

                // ── LLM-based check (optional) ──
                if (AppConfig.EnableLLMCheckShouldResponse)
                {
                    if (isMentioned)
                        replyProbability = 1;
                    else
                        (_, replyProbability) = await ReplyManager.CheckByLLM(
                            AppConfig.BotName, AppConfig.BotNicknames, [messageText]);
                }

                // ── Random check ──
                if (CommonHelper.NextDouble() >= replyProbability)
                {
                    replyManager.AfterSkip();
                    CommonHelper.DebugLog("Reply", "skip — probability not met");
                    return EventHandleResult.Pass;
                }

                // ── LLM call ──
                var result = await DoChatAsync(e, groupId, qq, ct);

                if (result == EventHandleResult.Block)
                    replyManager.AfterSend();
                else
                    replyManager.AfterSkip();

                return result;
            }
            finally { SetBusy(groupId, false); }
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("ChatPipeline", ex.Message);
            return EventHandleResult.Pass;
        }
    }

    public static async Task<EventHandleResult> ProcessPrivateAsync(
        PrivateMessageContext e, CancellationToken ct)
    {
        if (AppConfig.IsPersonBlackList)
        {
            if (AppConfig.PersonList.Contains(e.FromQQ.Id)) return EventHandleResult.Pass;
        }
        else
        {
            if (!AppConfig.PersonList.Contains(e.FromQQ.Id)) return EventHandleResult.Pass;
        }

        var qq = e.FromQQ.Id;
        if (string.IsNullOrWhiteSpace(e.Message.Text)) return EventHandleResult.Pass;

        try
        {
            if (!TrySetBusy(qq)) return EventHandleResult.Pass;
            try { return await DoChatPrivateAsync(e, ct); }
            finally { SetBusy(qq, false); }
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("ChatPipeline", ex.Message);
            return EventHandleResult.Pass;
        }
    }

    // ── Chat ────────────────────────────────────────────

    private static async Task<EventHandleResult> DoChatAsync(
        GroupMessageContext e, long groupId, long qq, CancellationToken ct)
    {
        // Record incoming message
        RecordMessage(e.Message, groupId, qq, qq.ToString());

        return await SendChatResponse(groupId, qq, true, AppConfig.GroupPrompt,
            e.Message.Text, async msg => {
                await e.SendMessageAsync(msg);
                RecordBotMessage(groupId, msg);
            }, ct);
    }

    private static async Task<EventHandleResult> DoChatPrivateAsync(
        PrivateMessageContext e, CancellationToken ct)
    {
        var qq = e.FromQQ.Id;
        RecordMessage(e.Message, 0, qq, qq.ToString());

        return await SendChatResponse(0, qq, false, AppConfig.PrivatePrompt,
            e.Message.Text, async msg => {
                await e.SendMessageAsync(msg);
                RecordBotMessage(0, msg);
            }, ct);
    }

    private static async Task<EventHandleResult> SendChatResponse(
        long groupId, long qq, bool isGroup, string character,
        string messageText, Func<string, Task> sendFunc, CancellationToken ct)
    {
        var keys = AppConfig.ChatAPIKeyId;
        if (keys.Count == 0) return EventHandleResult.Pass;

        var botQQ = PromptBuilder.CurrentBotQQ;
        var systemPrompt = PromptBuilder.BuildSystemPrompt(AppConfig.BotName,
                                                           string.Join(",", AppConfig.BotNicknames),
                                                           qq,
                                                           AppConfig.ChatEmptyResponse,
                                                           string.Join(",", AppConfig.MasterQQ),
                                                           AppConfig.GroupPrompt);

        // ── Load chat history ──
        var history = isGroup
            ? ChatRecord.GetGroupHistory(groupId, AppConfig.ContextMaxLength)
            : ChatRecord.GetPrivateHistory(qq, AppConfig.ContextMaxLength);
        var historyText = string.Join("\n",
            history.Select(r => $"[{r.Time:HH:mm}]{r.NickName}[{r.QQ}]: {r.ParsedMessage}"));

        // ── Load memories ──
        var shortTerm = MemoryManager.GetShortTerm(groupId, qq);
        var shortTermLines = shortTerm.Select(m => m.ToString()).ToList();
        var longTerm = MemoryManager.GetLongTerm(messageText, qq);
        var longTermLines = longTerm.Select(m => m.text).ToList();
        var knowledge = MemoryManager.GetKnowledge(messageText);
        var knowledgeLines = knowledge.Select(k => k.text).ToList();
        var todos = MemoryManager.GetToDos(groupId, qq);
        var todoLines = todos.Select(t => t.ToString()).ToList();
        CommonHelper.DebugLog("Memory",
            $"短期={shortTerm.Length} 长期={longTerm.Length} 知识={knowledge.Length} 待办={todos.Length}");

        // ── Build dynamic user content ──
        // TODO: Phase 7 — LLM-based memory selection and summarization
        var moodText = isGroup ? MoodState.GetMood(groupId) : MoodState.GetMood(qq);
        var scheduleText = AppConfig.EnableSchedules
            ? SchedulerManager.Instance?.GetCurrentSchedule(DateTime.Now)
            : null;
        var dynamicContent = PromptBuilder.BuildDynamicUserContent(
            moodText, scheduleText, todoLines, shortTermLines, longTermLines, knowledgeLines, character);
        var last = history.Last();
        dynamicContent += $"[{last.Time:HH:mm}]{last.NickName}[{last.QQ}]: {last.ParsedMessage}";

        var messages = PromptBuilder.BuildRequestBody(systemPrompt, history.Take(history.Count - 1).ToList(), dynamicContent);

        var chatService = new ChatService();
        var response = await chatService.GetChatResultAsync(
            keys, messages, ChatService.Purpose.聊天, timeout: AppConfig.ChatTimeout);

        if (response == ChatService.ErrorMessage) return EventHandleResult.Pass;
        if (response.Contains(AppConfig.ChatEmptyResponse))
        {
            response = response.Replace(AppConfig.ChatEmptyResponse, "").Trim();
            if (string.IsNullOrWhiteSpace(response)) return EventHandleResult.Pass;
        }

        // ── Dedup check ──
        if (!string.IsNullOrWhiteSpace(response) && IsNearDuplicate(response, groupId))
        {
            CommonHelper.DebugLog("Dedup", "跳过重复回复");
            return EventHandleResult.Pass;
        }

        if (!string.IsNullOrWhiteSpace(response))
            await sendFunc(response);

        return EventHandleResult.Block;
    }

    // ── Message parsing ─────────────────────────────────

    private static bool CheckAtBot(Message msg)
    {
        if (msg.MessageChain == null) return false;
        var botQQ = PromptBuilder.CurrentBotQQ;
        return msg.MessageChain.OfType<At>().Any(a => a.Target == botQQ || a.AllTarget);
    }

    private static bool CheckReplyToBot(Message msg, long groupId)
    {
        if (msg.MessageChain == null) return false;
        var replyItems = msg.MessageChain.OfType<Reply>().ToList();
        if (replyItems.Count == 0) return false;

        var botQQ = PromptBuilder.CurrentBotQQ;
        foreach (var reply in replyItems)
        {
            var records = ChatRecord.GetByIds([reply.Id], groupId);
            if (records.Any(r => r.QQ == botQQ))
                return true;
        }
        return false;
    }

    private static bool CheckNickname(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return AppConfig.BotNicknames.Any(n =>
            !string.IsNullOrEmpty(n) && text.Contains(n, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasImage(Message msg)
        => msg.MessageChain?.OfType<Image>().Any() ?? false;

    // ── Message recording ───────────────────────────────

    private static void RecordMessage(Message msg, long groupId, long qq, string nick)
    {
        try
        {
            // Parse text from message chain
            var rawText = msg.Text ?? "";
            var parsedText = string.Join("", msg.MessageChain?
                .Where(i => i is Text)
                .Select(i => ((Text)i).Content) ?? [rawText]);

            var record = new ChatRecord
            {
                GroupID = groupId, QQ = qq, NickName = nick,
                Message = rawText, 
                ParsedMessage = parsedText,
                SenderType = SenderType.User,
                MessageID = msg.Id, Time = DateTime.Now,
                IsMentioned = CheckAtBot(msg),
                IsImage = HasImage(msg) && string.IsNullOrWhiteSpace(parsedText),
                IsEmpty = !HasImage(msg) && string.IsNullOrWhiteSpace(parsedText)
            };
            ChatRecord.Insert(record);

            // Memory extraction trigger
            var key = groupId > 0 ? groupId : qq;
            if (MemoryManager.ShouldExtract(key) && qq != PromptBuilder.CurrentBotQQ)
            {
                // TODO: Phase 7 — LLM-based memory extraction
            }
        }
        catch (Exception ex) { CommonHelper.LogError?.Invoke("Record", ex.Message); }
    }

    private static void RecordBotMessage(long groupId, string message)
    {
        try
        {
            ChatRecord.Insert(new ChatRecord
            {
                GroupID = groupId,
                QQ = PromptBuilder.CurrentBotQQ,
                NickName = AppConfig.BotName,
                Message = message,
                ParsedMessage = message,
                SenderType = SenderType.Assistant,
                Time = DateTime.Now
            });
            ChatRecord.Cleanup(groupId, keepCount: 1000);
        }
        catch (Exception ex) { CommonHelper.LogError?.Invoke("RecordBot", ex.Message); }
    }

    // ── Dedup ───────────────────────────────────────────

    /// <summary>
    /// Checks if a candidate response is too similar to the bot's recent messages.
    /// Uses normalized Levenshtein distance — fast, no LLM needed.
    /// </summary>
    private static bool IsNearDuplicate(string candidate, long groupId)
    {
        var recentBotMessages = ChatRecord.GetGroupHistory(groupId, 5)
            .Where(r => r.SenderType == SenderType.Assistant)
            .Select(r => r.ParsedMessage)
            .ToList();

        if (recentBotMessages.Count == 0) return false;
        if (candidate.Length < 6) return false; // too short to meaningfully compare

        foreach (var recent in recentBotMessages)
        {
            if (recent.Length < 6) continue;
            double similarity = LevenshteinSimilarity(candidate, recent);
            if (similarity >= 0.85)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Normalized Levenshtein similarity [0,1] where 1 = identical.
    /// </summary>
    private static double LevenshteinSimilarity(string a, string b)
    {
        int maxLen = Math.Max(a.Length, b.Length);
        if (maxLen == 0) return 1.0;
        int distance = LevenshteinDistance(a, b);
        return 1.0 - (double)distance / maxLen;
    }

    /// <summary>
    /// Levenshtein (edit) distance between two strings.
    /// </summary>
    private static int LevenshteinDistance(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        // Use the shorter string as the row for O(min(n,m)) space
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
                curr[j] = Math.Min(
                    Math.Min(prev[j] + 1, curr[j - 1] + 1),
                    prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[b.Length];
    }

    // ── Busy lock ───────────────────────────────────────

    private static bool TrySetBusy(long id)
    {
        lock (_busyLocks)
        {
            if (_busyLocks.TryGetValue(id, out var busy) && busy) return false;
            _busyLocks[id] = true;
            return true;
        }
    }

    private static void SetBusy(long id, bool busy)
    {
        lock (_busyLocks) { _busyLocks[id] = busy; }
    }
}
