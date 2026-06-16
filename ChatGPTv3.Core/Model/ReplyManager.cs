using ChatGPTv3.Core.Api;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Model;

/// <summary>
/// Per-group reply willingness state machine.
/// Two modes: High (talkative) and Low (quiet).
/// Driven by a 5-second timer that decays willingness and switches modes.
/// </summary>
public class ReplyManager
{
    public static Dictionary<long, ReplyManager> Managers { get; } = [];

    public bool HighReplyWilling { get; set; }
    public int MessageHoldCount { get; set; }
    public DateTime WillingLastChangeTime { get; set; } = DateTime.Now;
    public DateTime LastReplyTime { get; set; } = DateTime.MinValue;
    public long LastReplyQQ { get; set; }
    public bool ContextMode { get; set; }
    public double ReplyWilling { get; set; }
    public TimeSpan CurrentModeKeepInterval { get; set; }

    private Timer? _timer;
    private int _timerCount;

    public ReplyManager(long id)
    {
        Managers[id] = this;
        StartTimer();
    }

    public static ReplyManager Get(long id)
    {
        if (Managers.TryGetValue(id, out var manager))
            return manager;
        return new ReplyManager(id);
    }

    private void StartTimer()
    {
        _timer = new Timer(_ => OnTimerTick(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    private void OnTimerTick()
    {
        _timerCount++;
        if (HighReplyWilling)
            ReplyWilling = Math.Max(0.5, ReplyWilling * 0.95);
        else
            ReplyWilling = Math.Max(0, ReplyWilling * 0.8);

        if (DateTime.Now - WillingLastChangeTime > CurrentModeKeepInterval
            || (!HighReplyWilling && CommonHelper.NextDouble() < 0.1))
        {
            if (HighReplyWilling)
            {
                HighReplyWilling = false;
                ReplyWilling = 0.01;
                CurrentModeKeepInterval = TimeSpan.FromMinutes(CommonHelper.Next(10, 20));
            }
            else
            {
                HighReplyWilling = true;
                ReplyWilling = 1;
                CurrentModeKeepInterval = TimeSpan.FromMinutes(CommonHelper.Next(3, 5));
            }
            WillingLastChangeTime = DateTime.Now;
            MessageHoldCount = 0;
        }

        if ((DateTime.Now - LastReplyTime).TotalMinutes >= 5)
            ContextMode = false;
    }

    /// <summary>
    /// Updates reply willingness based on incoming message characteristics.
    /// Returns the final reply probability.
    /// </summary>
    public double UpdateWillingness(bool isImage, bool isMentioned, bool containsNickname, long qq)
    {
        MessageHoldCount++;

        // Conversation continuity
        if (qq == LastReplyQQ && (DateTime.Now - LastReplyTime).TotalMinutes < 2 && MessageHoldCount <= 5)
        {
            ContextMode = true;
            ReplyWilling += 0.3;
        }

        // Mentioned: always reply
        if (isMentioned)
        {
            ContextMode = true;
            LastReplyQQ = qq;
            return 1;
        }

        // Contains nickname
        if (containsNickname)
        {
            ContextMode = true;
            ReplyWilling += 0.8;
        }

        // Image-only: low willingness
        if (isImage)
            ReplyWilling *= 0.1;

        // Calculate base probability
        double baseProb;
        if (ContextMode)
            baseProb = HighReplyWilling ? 0.5 : 0.25;
        else if (HighReplyWilling)
            baseProb = MessageHoldCount is >= 4 and <= 8 ? 0.5 : 0.2;
        else
            baseProb = MessageHoldCount > 15 ? 0.3 : 0.03 * Math.Min(MessageHoldCount, 10);

        ReplyWilling = Math.Clamp(ReplyWilling, 0, 3);
        LastReplyQQ = qq;

        return ReplyWilling * baseProb * AppConfig.ReplyWillingAmplifier;
    }

    public void AfterSend()
    {
        ReplyWilling -= 0.6;
        ContextMode = true;
        ReplyWilling = Math.Clamp(ReplyWilling, 0, 3);
        LastReplyTime = DateTime.Now;
        MessageHoldCount = 0;
    }

    public void AfterSkip()
    {
        ReplyWilling += ContextMode ? 0.15 : HighReplyWilling ? 0.1 : CommonHelper.NextDouble(0.05, 0.1);
        ReplyWilling = Math.Clamp(ReplyWilling, 0, 3);
    }

    /// <summary>
    /// LLM-based reply check (alternative to built-in state machine).
    /// </summary>
    public static async Task<(bool shouldRespond, double confidence)> CheckByLLM(string botName,
        List<string> botNicknames, List<string> recentMessages)
    {
        var nickStr = string.Join(",", botNicknames);
        var prompt = @"你是一个群聊消息分析器，职责是判断“最新消息”是否在呼叫群聊助手“{BotName}”。你的代称还有“{BotNicknames}”。
呼叫助手的方式包括：直接叫名字、@助手、或在对话中指代助手。

判断时请严格遵循两步分析流程：
第一步：找出最新消息中所有的“你/您”以及助手的名称/别名。
第二步：检查上下文，确定这些代词的指代对象。
   - 如果“你”指代助手，则 should_respond=true。
   - 如果“你”指代其他群成员（如“你昨天说的电影”），或用于泛指（“你们怎么看”），则 should_respond=false。
   - 如果没有第二人称也没有助手称呼，但消息本身是接续助手刚才的话题，也应判断为 true。

规则补充：
- 仅当上下文显示用户正在期待助手回应时才应回复。
- 闲聊、表情包、多人对话中的无明确指向性消息 → false。
- 如果用户表现出对助手的不满（如“你话好多”），confidence 降低50%。
- 如果不确定，一律选择 false（安全优先）。

我会提供最近的消息记录和最新消息，请直接分析并输出 JSON：
{""should_respond"": true/false, ""confidence"": 0.0~1.0}
请不要输出任何其他文字。";

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
