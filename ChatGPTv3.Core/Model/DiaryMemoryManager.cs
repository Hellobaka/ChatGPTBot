using ChatGPTv3.Core.Api;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;
using System.Text.Json;

namespace ChatGPTv3.Core.Model;

/// <summary>
/// Push-mode diary memory system.
///
/// Instead of retrieving memories during conversation (pull),
/// this generates a daily natural-language diary entry per-group
/// and injects it into the next day's system prompt.
///
/// Dual trigger:
///   1. Message count threshold (DiaryMessageThreshold)
///   2. Time interval (DiaryIntervalMinutes) — checked by a periodic timer
///
/// Side effect: updates MoodState for the group after diary generation.
/// </summary>
public static class DiaryMemoryManager
{
    // TODO: 验证日记是否起到了压缩上下文的功能
    private static readonly Dictionary<long, int> _msgCounts = [];

    private static readonly Dictionary<long, DateTime> _lastDiaryTimes = [];
    private static readonly object _lock = new();
    private static Timer? _timer;
    private static string _appDir = string.Empty;
    private static bool _initialized;

    // ── Init ──────────────────────────────────────────────────

    public static void Initialize(string appDir)
    {
        _appDir = appDir;
        LoadState();

        if (AppConfig.EnableDiary)
        {
            _timer = new Timer(_ => OnTimerTick(), null,
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
            CommonHelper.LogInfo?.Invoke("Diary", $"日记系统已启动 — 阈值:{AppConfig.DiaryMessageThreshold}条 间隔:{AppConfig.DiaryIntervalMinutes}分钟");
        }
        else
        {
            CommonHelper.LogInfo?.Invoke("Diary", "日记系统已禁用");
        }

        _initialized = true;
    }

    /// <summary>Shutdown the timer. Call on plugin disable.</summary>
    public static void Shutdown()
    {
        _timer?.Dispose();
        _timer = null;
    }

    // ── Trigger 1: Message count ──────────────────────────────

    /// <summary>
    /// Called by the chat pipeline after each message is processed.
    /// Increments the per-group counter and triggers diary if threshold reached.
    /// </summary>
    public static void OnMessageProcessed(long groupId)
    {
        if (groupId == 0 || !AppConfig.EnableDiary || !_initialized)
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

        if (count >= AppConfig.DiaryMessageThreshold)
        {
            _ = Task.Run(async () => await GenerateDiaryAsync(groupId));
        }
    }

    // ── Trigger 2: Time interval ──────────────────────────────

    private static void OnTimerTick()
    {
        if (!AppConfig.EnableDiary || !_initialized)
        {
            return;
        }

        List<long> groupsToCheck;
        lock (_lock)
        {
            groupsToCheck = _msgCounts.Keys.ToList();
        }

        var now = DateTime.Now;
        foreach (var groupId in groupsToCheck)
        {
            bool shouldGenerate = false;
            lock (_lock)
            {
                if (_msgCounts.TryGetValue(groupId, out var count) && count > 0)
                {
                    if (_lastDiaryTimes.TryGetValue(groupId, out var lastDiary))
                    {
                        if ((now - lastDiary).TotalMinutes >= AppConfig.DiaryIntervalMinutes)
                        {
                            shouldGenerate = true;
                        }
                    }
                    else
                    {
                        // Never generated before but has messages — check if enough time
                        // since first message. We approximate by always generating first diary.
                        shouldGenerate = true;
                    }
                }
            }

            if (shouldGenerate)
            {
                _ = Task.Run(async () => await GenerateDiaryAsync(groupId));
            }
        }
    }

    // ── Core: Diary generation ────────────────────────────────

    /// <summary>
    /// Generate a diary entry for a group by reviewing recent chat history via LLM.
    /// </summary>
    public static async Task GenerateDiaryAsync(long groupId)
    {
        // Reset counter and update timestamp
        lock (_lock)
        {
            _msgCounts[groupId] = 0;
            _lastDiaryTimes[groupId] = DateTime.Now;
        }
        SaveState();

        try
        {
            // 1. Collect recent messages (User + Assistant only, skip Tool)
            var since = DateTime.Now.AddHours(-AppConfig.DiaryReviewHours);
            var messages = ChatRecord.GetGroupHistoryByTime(groupId, since, maxCount: 300);

            // Filter to User + Assistant only
            var chatMessages = messages
                .Where(r => r.SenderType == SenderType.User || r.SenderType == SenderType.Assistant)
                .ToList();

            if (chatMessages.Count < 5)
            {
                CommonHelper.DebugLog("Diary", $"群 {groupId} 消息不足(仅{chatMessages.Count}条)，跳过日记");
                return;
            }

            // 2. Build chat log
            var chatLog = new System.Text.StringBuilder();
            foreach (var r in chatMessages)
            {
                var speaker = r.SenderType == SenderType.Assistant
                    ? AppConfig.BotName
                    : (r.NickName.Length > 0 ? r.NickName : r.QQ.ToString());
                chatLog.AppendLine($"[{r.Time:MM-dd HH:mm}] {speaker}: {r.ParsedMessage}");
            }

            // 3. Build diary prompt
            var prompt = $"""
你是 {AppConfig.BotName}。回顾你今天在群聊中的对话，请用第一人称写一段简短的日记（100-200字），记录：
- 今天和谁聊了什么重要或有趣的事
- 你学到了什么（事实信息、用户偏好、关系变化）
- 有什么需要跟进的事项

格式要求：
- 用自然语言描述，不要元信息（如"今天我和…"开头即可）
- 不要评价自己的表现
- 不要在日记中出现"日记"两个字

聊天记录：
{chatLog}

写你的日记。日记之后，另起一行写 [心情]，然后用一句话描述你现在在这个群里的心情。
""";

            // 4. Call LLM (prefer diary-specific keys, fallback to chat keys)
            var keys = AppConfig.DiaryAPIKeyId.Count > 0
                ? AppConfig.DiaryAPIKeyId
                : AppConfig.ChatAPIKeyId;

            var llmMessages = new List<ChatMessage>
            {
                ChatMessage.System("你是一个日记助手，负责为群聊机器人写简短自然的日记。"),
                ChatMessage.User(prompt)
            };

            var chatService = new ChatService();
            var result = await chatService.GetChatResultAsync(
                keys, llmMessages, ChatService.Purpose.日记,
                timeout: AppConfig.DiaryTimeout);

            if (result == ChatService.ErrorMessage || string.IsNullOrWhiteSpace(result))
            {
                CommonHelper.LogWarning?.Invoke("Diary", $"群 {groupId} 日记生成失败");
                return;
            }

            // 5. Parse diary text + mood
            string diaryText = result.Trim();
            string? moodText = null;

            var moodMarker = diaryText.IndexOf("[心情]");
            if (moodMarker >= 0)
            {
                var afterMood = diaryText[(moodMarker + 4)..].Trim();
                diaryText = diaryText[..moodMarker].Trim();

                // Clean mood text
                if (!string.IsNullOrWhiteSpace(afterMood))
                {
                    moodText = afterMood;
                }
            }

            // 6. Save diary
            SaveDiary(groupId, diaryText);

            // 7. Side effect: update mood
            if (!string.IsNullOrWhiteSpace(moodText))
            {
                MoodState.UpdateMood(groupId, moodText);
                CommonHelper.DebugLog("Diary", $"群 {groupId} 心情联动更新: {moodText}");
            }

            CommonHelper.LogInfo?.Invoke("Diary",
                $"群 {groupId} 日记已生成 ({chatMessages.Count}条消息, {diaryText.Length}字)");
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("Diary", $"群 {groupId} 日记异常: {ex.Message}");
        }
    }

    // ── Diary retrieval (for prompt injection) ─────────────────

    /// <summary>
    /// Get the latest diary context for injection into system prompt.
    /// Returns null if no diary exists or diary is too old.
    /// </summary>
    public static string? GetDiaryContext(long groupId)
    {
        var diaries = LoadDiaries(groupId);
        if (diaries.Count == 0)
        {
            return null;
        }

        var latest = diaries.Last();
        var age = DateTime.Now - latest.time;

        if (age.TotalHours < 6)
        {
            return $"你当前的日记记忆：{latest.text}";
        }

        if (age.TotalHours < 24)
        {
            return $"你今天的日记记忆：{latest.text}";
        }

        return $"你之前的日记记忆({latest.time:MM-dd HH:mm})：{latest.text}（已经过去了）";
    }

    /// <summary>
    /// Returns all diary entries across all groups, grouped by groupId.
    /// </summary>
    public static Dictionary<long, List<(DateTime time, string text)>> GetAllDiaries()
    {
        if (string.IsNullOrEmpty(_appDir))
        {
            return [];
        }

        var result = new Dictionary<long, List<(DateTime time, string text)>>();
        try
        {
            var files = Directory.GetFiles(_appDir, "diary_*.json");
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                // diary_123456.json -> 123456
                if (!long.TryParse(fileName["diary_".Length..^".json".Length], out var groupId))
                {
                    continue;
                }

                result[groupId] = LoadDiaries(groupId);
            }
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("Diary", $"读取所有日记失败: {ex.Message}");
        }

        return result;
    }

    // ── Diary persistence ─────────────────────────────────────

    private static void SaveDiary(long groupId, string text)
    {
        var diaries = LoadDiaries(groupId);
        diaries.Add((DateTime.Now, text));

        // Keep only last N
        var maxKeep = AppConfig.DiaryMaxKeep;
        if (diaries.Count > maxKeep)
        {
            diaries = diaries.Skip(diaries.Count - maxKeep).ToList();
        }

        SaveDiariesToFile(groupId, diaries);
    }

    /// <summary>
    /// Saves a specific diary entry (for editing). Replaces the entry at the given index.
    /// </summary>
    public static void SaveDiaryEntry(long groupId, int index, string text)
    {
        var diaries = LoadDiaries(groupId);
        if (index < 0 || index >= diaries.Count)
        {
            return;
        }

        diaries[index] = (diaries[index].time, text);
        SaveDiariesToFile(groupId, diaries);
    }

    private static void SaveDiariesToFile(long groupId, List<(DateTime time, string text)> diaries)
    {
        var path = Path.Combine(_appDir, $"diary_{groupId}.json");
        try
        {
            var json = JsonSerializer.Serialize(diaries.Select(d => new DiaryEntry
            {
                Time = d.time,
                Text = d.text
            }));
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("Diary", $"保存日记失败: {ex.Message}");
        }
    }

    private static List<(DateTime time, string text)> LoadDiaries(long groupId)
    {
        var path = Path.Combine(_appDir, $"diary_{groupId}.json");
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<DiaryEntry>>(File.ReadAllText(path));
            return entries?.Select(e => (e.Time, e.Text)).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    // ── Counter state persistence ─────────────────────────────

    private static void SaveState()
    {
        if (string.IsNullOrEmpty(_appDir))
        {
            return;
        }

        try
        {
            Dictionary<string, int> counts;
            Dictionary<string, DateTime> lastTimes;
            lock (_lock)
            {
                counts = _msgCounts.ToDictionary(k => k.Key.ToString(), v => v.Value);
                lastTimes = _lastDiaryTimes.ToDictionary(k => k.Key.ToString(), v => v.Value);
            }

            var state = new DiaryState { Counts = counts, LastTimes = lastTimes };
            var path = Path.Combine(_appDir, "diary_state.json");
            File.WriteAllText(path, JsonSerializer.Serialize(state));
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("Diary", $"保存状态失败: {ex.Message}");
        }
    }

    private static void LoadState()
    {
        if (string.IsNullOrEmpty(_appDir))
        {
            return;
        }

        var path = Path.Combine(_appDir, "diary_state.json");
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var state = JsonSerializer.Deserialize<DiaryState>(File.ReadAllText(path));
            if (state?.Counts != null && state.LastTimes != null)
            {
                lock (_lock)
                {
                    foreach (var (key, val) in state.Counts)
                    {
                        if (long.TryParse(key, out var id))
                        {
                            _msgCounts[id] = val;
                        }
                    }

                    foreach (var (key, val) in state.LastTimes)
                    {
                        if (long.TryParse(key, out var id))
                        {
                            _lastDiaryTimes[id] = val;
                        }
                    }
                }
            }
        }
        catch
        {
            // Corrupted state file — start fresh
        }
    }

    // ── Private types ─────────────────────────────────────────

    private class DiaryEntry
    {
        public DateTime Time { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    private class DiaryState
    {
        public Dictionary<string, int> Counts { get; set; } = [];

        public Dictionary<string, DateTime> LastTimes { get; set; } = [];
    }
}