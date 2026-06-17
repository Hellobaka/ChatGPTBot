using ChatGPTv3.Core.Api;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Model;

/// <summary>
/// Per-group reply willingness calculator.
///
/// WILLINGNESS = ATTENTION × TIMING × ACTIVITY
///
/// Attention — how much this message concerns the bot (mention, nickname, question...)
/// Timing    — how many messages since the bot last spoke (too soon or too late = lower)
/// Activity  — how many times the bot has spoken recently (throttle against spam)
///
/// No timers, no decay formulas, no mode switching.
/// State only changes on message arrival — fully predictable and debuggable.
/// </summary>
public class ReplyManager
{
    private static readonly Dictionary<long, ReplyManager> _managers = [];

    // ── Per-group state ────────────────────────────────────

    private int _silentMessageCount;
    private readonly List<DateTime> _recentSendTimes = [];
    private DateTime _lastReplyTime = DateTime.MinValue;
    private long _lastReplyQQ;

    public long LastReplyQQ => _lastReplyQQ;

    // ── Factory ────────────────────────────────────────────

    public static ReplyManager Get(long id)
    {
        if (_managers.TryGetValue(id, out var mgr))
            return mgr;
        return _managers[id] = new ReplyManager();
    }

    // ═══════════════════════════════════════════════════════════
    //  Core Calculation
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Call on every incoming message. Returns the probability [0,1] that
    /// the bot should respond.
    /// </summary>
    public double UpdateWillingness(
        bool isMentioned,
        bool isReplyToBot,
        bool containsNickname,
        bool hasQuestion,
        bool fromSamePerson,
        bool isImageOnly,
        long qq)
    {
        _silentMessageCount++;

        // ── Attention: is this message about me? ──
        double attention = AppConfig.BaseAttention;

        if (isMentioned)
        {
            attention = AppConfig.AttnMention;
        }
        else
        {
            if (isReplyToBot)     attention = Math.Max(attention, AppConfig.AttnReplyToBot);
            if (containsNickname) attention = Math.Max(attention, AppConfig.AttnNickname);
            if (hasQuestion)      attention = Math.Max(attention, AppConfig.AttnQuestion);
            if (fromSamePerson)   attention = Math.Max(attention, AppConfig.AttnContinuity);
        }

        if (isImageOnly && !isMentioned)
            attention *= AppConfig.AttnImageFactor;

        // ── Timing: how long have I been silent? ──
        double timing = EvaluateTiming();

        // ── Activity: have I been talking too much? ──
        CleanRecentSendTimes();
        double activity = EvaluateActivity();

        // ── Combine ──
        double willingness = attention * timing * activity * AppConfig.ReplyWillingAmplifier;

        _lastReplyQQ = qq;

        CommonHelper.DebugLog("Reply",
            $"attn={attention:F3} time={timing:F3} act={activity:F3} " +
            $"silent={_silentMessageCount} sends={_recentSendTimes.Count} → {willingness:F3}");

        return Math.Clamp(willingness, 0, 1);
    }

    // ── Timing curve ───────────────────────────────────────

    private double EvaluateTiming()
    {
        return _silentMessageCount switch
        {
            0    => AppConfig.TimingJustSent,
            <= 3 => AppConfig.TimingBriefPause,
            <= 10 => AppConfig.TimingOptimal,
            <= 20 => AppConfig.TimingStale,
            _    => AppConfig.TimingVeryStale,
        };
    }

    // ── Activity throttle ──────────────────────────────────

    private double EvaluateActivity()
    {
        return _recentSendTimes.Count switch
        {
            0 => AppConfig.ActivityFirst,
            1 => AppConfig.ActivitySecond,
            2 => AppConfig.ActivityThird,
            _ => 0,  // 3+ sends in the window → don't send
        };
    }

    private void CleanRecentSendTimes()
    {
        var cutoff = DateTime.Now - TimeSpan.FromSeconds(AppConfig.ActivityThrottleSeconds);
        _recentSendTimes.RemoveAll(t => t < cutoff);
    }

    // ═══════════════════════════════════════════════════════════
    //  Post-action
    // ═══════════════════════════════════════════════════════════

    /// <summary>Call after the bot sends a reply.</summary>
    public void AfterSend()
    {
        _recentSendTimes.Add(DateTime.Now);
        _lastReplyTime = DateTime.Now;
        _silentMessageCount = 0;
    }

    /// <summary>Call after the bot decides NOT to reply.</summary>
    public void AfterSkip()
    {
        // Willingness naturally builds via silentMessageCount accumulation.
        // No explicit increment needed.
    }

    // ═══════════════════════════════════════════════════════════
    //  LLM-based Check (kept for borderline cases)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// LLM-based reply check. Returns (shouldRespond, confidence).
    /// Used as an alternative or supplement to the built-in calculation.
    /// </summary>
    public static async Task<(bool shouldRespond, double confidence)> CheckByLLM(
        string botName,
        List<string> botNicknames,
        List<string> recentMessages)
    {
        var nickStr = string.Join(",", botNicknames);
        var prompt = """
你是一个群聊消息分析器，职责是判断"最新消息"是否在呼叫群聊助手"{BotName}"。你的代称还有"{BotNicknames}"。
呼叫助手的方式包括：直接叫名字、@助手、或在对话中指代助手。

判断时请严格遵循两步分析流程：
第一步：找出最新消息中所有的"你/您"以及助手的名称/别名。
第二步：检查上下文，确定这些代词的指代对象。
   - 如果"你"指代助手，则 should_respond=true。
   - 如果"你"指代其他群成员（如"你昨天说的电影"），或用于泛指("你们怎么看")，则 should_respond=false。
   - 如果没有第二人称也没有助手称呼，但消息本身是接续助手刚才的话题，也应判断为 true。

规则补充：
- 仅当上下文显示用户正在期待助手回应时才应回复。
- 闲聊、表情包、多人对话中的无明确指向性消息 -> false。
- 如果用户表现出对助手的不满(如"你话好多")，confidence 降低50%。
- 如果不确定，一律选择 false(安全优先)。

我会提供最近的消息记录和最新消息，请直接分析并输出 JSON：
{"should_respond": true/false, "confidence": 0.0~1.0}
请不要输出任何其他文字。
""";

        try
        {
            var messages = new List<ChatMessage> {
                ChatMessage.System(prompt.Replace("{BotName}", botName).Replace("{BotNicknames}", nickStr)),
                ChatMessage.User(string.Join("\n", recentMessages.Skip(1))),
                ChatMessage.Assistant("好的，我会分析是否需要回复，请提供最新消息。"),
                ChatMessage.User(recentMessages.First()),
            };
            var chatService = new ChatService();
            var result = await chatService.GetChatResultAsync(
                AppConfig.ReplyAPIKeyId, messages,
                ChatService.Purpose.回复意愿,
                timeout: AppConfig.ReplyTimeout);

            if (result == ChatService.ErrorMessage)
                return (true, 0);

            result = result.ToLower().Replace("`", "").Replace("json", "").Trim();
            int start = result.IndexOf('{');
            int end = result.LastIndexOf('}');
            if (start >= 0 && end > start)
                result = result[start..(end + 1)];
            using var doc = System.Text.Json.JsonDocument.Parse(result);
            var shouldRespond = doc.RootElement.GetProperty("should_respond").GetBoolean();
            var confidence = doc.RootElement.GetProperty("confidence").GetDouble();
            return (shouldRespond, confidence);
        }
        catch
        {
            return (true, 0); // Fallback: respond on error
        }
    }
}
