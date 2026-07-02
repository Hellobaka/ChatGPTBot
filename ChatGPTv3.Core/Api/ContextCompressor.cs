using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Api;

/// <summary>
/// Background context compression — when enough messages accumulate, the oldest
/// are summarized by a cheap LLM and saved to ContextSummary.
///
/// Uses ChatRecord.Id to track compression boundaries — survives restarts
/// with no overlap. The next compression only processes messages with IDs
/// greater than the last compressed ID.
///
/// Never blocks the conversation — compression runs fire-and-forget.
/// Saved summaries are picked up by GetGroupHistory on subsequent calls.
/// </summary>
public static class ContextCompressor
{
    // Per-group tracking (in-memory, rebuilt from DB on restart)
    private static readonly Dictionary<long, int> _msgCounts = [];

    private static readonly Dictionary<long, DateTime> _lastCompressTimes = [];
    private static readonly Dictionary<long, int> _lastCompressedId = []; // Last ChatRecord.Id covered
    private static readonly object _lock = new();
    private static Timer? _timer;
    private static bool _initialized;

    private const string CompressionSystemPrompt = """
你是一个对话压缩器。将一段群聊记录压缩为简短摘要，只保留对后续对话可能有用的信息。

压缩规则：
- 保留：事实信息（谁说了什么）、待解决的问题、用户表达的情绪或偏好、用户之间的称呼/关系
- 丢弃：纯闲聊寒暄、重复消息、与主题无关的插话
- 用第三人称描述，如"小王问了SQLite的问题，你建议用CodeFirst；小李插话聊游戏"
- 输出1-3句话，不要超过150字
- 不要包含"摘要"、"压缩"等元信息词汇
""";

    // ═══════════════════════════════════════════════════════════
    //  Lifecycle
    // ═══════════════════════════════════════════════════════════

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        _timer = new Timer(_ => OnTimerTick(), null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

        CommonHelper.LogInfo?.Invoke("Compressor",
            $"后台压缩已启动 — 阈值:{AppConfig.CompressMessageThreshold}条 间隔:{AppConfig.CompressIntervalMinutes}分钟");
    }

    public static void Shutdown()
    {
        _timer?.Dispose();
        _timer = null;
        _initialized = false;
    }

    // ═══════════════════════════════════════════════════════════
    //  Trigger: message count
    // ═══════════════════════════════════════════════════════════

    public static void OnMessageProcessed(long groupId)
    {
        if (groupId == 0 || !AppConfig.EnableCompressByCount)
        {
            return;
        }

        int count;
        lock (_lock)
        {
            if (!_msgCounts.ContainsKey(groupId))
            {
                _msgCounts[groupId] = 0;
            }

            _msgCounts[groupId]++;
            count = _msgCounts[groupId];
        }

        if (count >= AppConfig.CompressMessageThreshold)
        {
            _ = Task.Run(async () => await RunCompressionAsync(groupId));
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Trigger: time interval
    // ═══════════════════════════════════════════════════════════

    private static void OnTimerTick()
    {
        if (!AppConfig.EnableCompressByTime)
        {
            return;
        }

        List<long> groups;
        lock (_lock) { groups = _msgCounts.Keys.ToList(); }

        var now = DateTime.Now;
        foreach (var groupId in groups)
        {
            bool shouldCompress = false;
            lock (_lock)
            {
                if (_msgCounts.TryGetValue(groupId, out var count) && count > 0)
                {
                    if (_lastCompressTimes.TryGetValue(groupId, out var lastTime))
                    {
                        if ((now - lastTime).TotalMinutes >= AppConfig.CompressIntervalMinutes)
                        {
                            shouldCompress = true;
                        }
                    }
                    else
                    {
                        shouldCompress = true;
                    }
                }
            }

            if (shouldCompress)
            {
                _ = Task.Run(async () => await RunCompressionAsync(groupId));
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Core compression (background)
    // ═══════════════════════════════════════════════════════════

    private static async Task RunCompressionAsync(long groupId)
    {
        lock (_lock)
        {
            _msgCounts[groupId] = 0;
            _lastCompressTimes[groupId] = DateTime.Now;
        }

        try
        {
            // Determine the starting point using ChatRecord.Id.
            // Check in-memory first, then DB (survives restart).
            int lastCoveredId;
            lock (_lock) { lastCoveredId = _lastCompressedId.GetValueOrDefault(groupId, 0); }

            if (lastCoveredId == 0)
            {
                var dbSummary = ContextSummary.FindLatest(groupId);
                if (dbSummary != null)
                {
                    lastCoveredId = dbSummary.LastMessageId;
                    lock (_lock) { _lastCompressedId[groupId] = lastCoveredId; }
                }
            }

            // Fetch only messages with Id > lastCoveredId (new compressible messages)
            var allHistory = FetchHistoryAfterId(groupId, lastCoveredId, AppConfig.ContextMaxLength * 3);
            if (allHistory.Count <= AppConfig.ContextMaxLength)
            {
                CommonHelper.DebugLog("Compressor", $"群 {groupId} 新可压缩消息不足({allHistory.Count}条)，跳过");
                return;
            }

            int dropCount = allHistory.Count - AppConfig.ContextMaxLength;
            var dropped = allHistory.Take(dropCount).ToList();
            if (dropped.Count < 5)
            {
                CommonHelper.DebugLog("Compressor", $"群 {groupId} 可丢弃消息太少({dropped.Count}条)，跳过");
                return;
            }

            // Extend existing summary if present
            var existingSummary = lastCoveredId > 0
                ? ContextSummary.FindLatest(groupId)
                : null;
            var summaryText = await SummarizeWithLLM(dropped, existingSummary?.Summary);
            if (string.IsNullOrWhiteSpace(summaryText))
            {
                return;
            }

            // Save: record the first and last message IDs covered
            int firstId = dropped[0].Id;
            int lastId = dropped[^1].Id;
            lock (_lock) { _lastCompressedId[groupId] = lastId; }

            SaveSummary(groupId, firstId, lastId, dropped.Count, summaryText, existingSummary != null);

            CommonHelper.LogInfo?.Invoke("Compressor",
                $"群 {groupId} 后台压缩完成 (ID {firstId}-{lastId}, {dropped.Count}条 → {summaryText.Length}字)");
        }
        catch (Exception ex)
        {
            CommonHelper.LogWarning?.Invoke("Compressor", $"群 {groupId} 后台压缩异常: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  History fetch (since a given message ID)
    // ═══════════════════════════════════════════════════════════

    private static List<ChatRecord> FetchHistoryAfterId(long groupId, int afterId, int count)
    {
        using var db = SQLiteManager.GetInstance();
        var query = db.Queryable<ChatRecord>()
            .Where(r => r.GroupID == groupId);
        if (afterId > 0)
        {
            query = query.Where(r => r.Id > afterId);
        }

        var newest = query.OrderByDescending(r => r.Time)
            .Take(count)
            .ToList();
        newest.Reverse();
        return newest;
    }

    // ═══════════════════════════════════════════════════════════
    //  LLM summarization
    // ═══════════════════════════════════════════════════════════

    private static async Task<string?> SummarizeWithLLM(List<ChatRecord> dropped, string? existingSummary = null)
    {
        var keys = AppConfig.SummarizerApiKeyId.Count > 0
            ? AppConfig.SummarizerApiKeyId
            : AppConfig.ChatAPIKeyId;
        if (keys.Count == 0)
        {
            return null;
        }

        var chatLog = new System.Text.StringBuilder();
        foreach (var r in dropped)
        {
            var speaker = r.SenderType == SenderType.Assistant
                ? AppConfig.BotName
                : (r.NickName.Length > 0 ? r.NickName : r.QQ.ToString());
            chatLog.AppendLine($"[{r.Time:HH:mm}] {speaker}: {r.ParsedMessage}");
        }

        var userPrompt = existingSummary != null
            ? $"之前已经有对话概要：\n{existingSummary}\n\n请将以下新消息合并进概要，不要重复已有信息：\n\n{chatLog}"
            : $"请压缩以下 {dropped.Count} 条聊天记录：\n\n{chatLog}";

        var messages = new List<ChatMessage>
        {
            ChatMessage.System(CompressionSystemPrompt),
            ChatMessage.User(userPrompt)
        };

        try
        {
            var chatService = new ChatService();
            var result = await chatService.GetChatResultAsync(
                keys, messages,
                ChatService.Purpose.工具总结,
                timeout: AppConfig.ChatTimeout);

            if (result == ChatService.ErrorMessage || string.IsNullOrWhiteSpace(result))
            {
                return null;
            }

            var timeSpan = dropped.Count > 1
                ? $"{dropped[0].Time:HH:mm}-{dropped[^1].Time:HH:mm}"
                : $"{dropped[0].Time:HH:mm}";
            return $"[对话概要 {timeSpan}] {result.Trim()}";
        }
        catch (Exception ex)
        {
            CommonHelper.LogWarning?.Invoke("Compressor", $"LLM压缩失败: {ex.Message}");
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Persistence
    // ═══════════════════════════════════════════════════════════

    private static void SaveSummary(long groupId, int firstId, int lastId, int msgCount,
        string summaryText, bool hasExisting)
    {
        try
        {
            if (hasExisting)
            {
                using var db = SQLiteManager.GetInstance();
                var existing = db.Queryable<ContextSummary>()
                    .Where(s => s.GroupId == groupId)
                    .OrderByDescending(s => s.CreatedAt)
                    .First();
                if (existing != null)
                {
                    existing.LastMessageId = lastId;
                    existing.MessageCount += msgCount;
                    existing.Summary = summaryText;
                    existing.CreatedAt = DateTime.Now;
                    db.Updateable(existing).ExecuteCommand();
                    return;
                }
            }

            ContextSummary.DeleteByGroup(groupId);
            ContextSummary.Insert(new ContextSummary
            {
                GroupId = groupId,
                FirstMessageId = firstId,
                LastMessageId = lastId,
                MessageCount = msgCount,
                Summary = summaryText,
                CreatedAt = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            CommonHelper.LogWarning?.Invoke("Compressor", $"保存摘要失败: {ex.Message}");
        }
    }
}