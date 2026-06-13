using ChatGPTv3.Core.Api;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.HttpSse;

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
        var prompt = "你是一个群聊助手，昵称:" + botName +
            "，非常用称呼: " + nickStr + "，需要判断当前是否应该回应最新消息。\n" +
            "规则：明显对话或提问应回应；话题切换无关不应回应；未被@且无人理你则不应回应。\n" +
            "最新消息:<latest_Message>" + (recentMessages.FirstOrDefault() ?? "") + "</latest_Message>\n" +
            "最近对话:<recent_Message>" + string.Join("\n", recentMessages.Skip(1)) + "</recent_Message>\n" +
            "请仅输出JSON: {\"should_respond\": true/false, \"confidence\": 0.0~1.0}";

        var messages = new List<ChatMessage> {
            ChatMessage.System(prompt),
            ChatMessage.User("请回复")
        };

        try
        {
            var chatService = new ChatService();
            var result = await chatService.GetChatResultAsync(
                AppConfig.ReplyAPIKeyId, messages,
                ChatService.Purpose.回复意愿, jsonMode: true,
                timeout: AppConfig.ReplyTimeout);

            if (result == ChatService.ErrorMessage)
                return (true, 0);

            result = result.ToLower().Replace("`", "").Replace("json", "").Trim();
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
