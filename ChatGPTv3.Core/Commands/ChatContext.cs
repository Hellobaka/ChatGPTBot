using Another_Mirai_Native.Abstractions.Context;
using Another_Mirai_Native.Abstractions.Enums;

namespace ChatGPTv3.Core.Commands;

public class PipelineTraceEntry
{
    public string Step { get; init; } = string.Empty;

    public bool Passed { get; init; }

    public string Detail { get; init; } = string.Empty;

    public DateTime Time { get; init; } = DateTime.Now;
}

/// <summary>
/// Data carrier that flows through the entire chat pipeline.
/// Supports both group and private messages.
/// </summary>
public class ChatContext
{
    // ── Input ──────────────────────────────────────────────
    public GroupMessageContext? GroupCtx { get; init; }

    public PrivateMessageContext? PrivateCtx { get; init; }

    public bool IsGroup => GroupCtx != null;

    public long GroupId => GroupCtx?.FromGroup.Id ?? 0;

    public long QQ => IsGroup ? GroupCtx!.FromQQ.Id : PrivateCtx!.FromQQ.Id;

    public string MessageText { get; set; } = string.Empty;

    /// <summary>For sending replies — group or private.</summary>
    public Func<string, Task>? SendFunc { get; set; }

    // ── Middleware results ─────────────────────────────────
    public bool IsMentioned { get; set; }

    public bool ContainsNickname { get; set; }

    public bool IsImageOnly { get; set; }

    public bool IsReplyToBot { get; set; }

    public bool HasQuestion { get; set; }

    public double ReplyProbability { get; set; }

    // ── Output ─────────────────────────────────────────────
    public EventHandleResult Result { get; set; } = EventHandleResult.Pass;

    /// <summary>LLM reasoning content (DeepSeek R1 style), populated during chat execution.</summary>
    public string? Reasoning { get; set; }

    // ── Cancellation ───────────────────────────────────────
    public CancellationToken CancellationToken { get; set; }

    // ── MCP state ──────────────────────────────────────────
    /// <summary>
    /// Images the LLM requested to see natively (via AddPictureToContext).
    /// Injected at the start of each conversation turn, then cleared.
    /// </summary>
    public List<string> PendingImageHashes { get; set; } = [];

    // ── Output recording ───────────────────────────────────
    /// <summary>Bot reply segments with send timestamps. Populated by SendReplyWithEmoji, consumed by UseBotMessageRecorder.</summary>
    public List<(string text, DateTime time)> BotReplies { get; } = [];

    /// <summary>Fallback reply text, set by HandleFallback. Consumed by UseBotMessageRecorder.</summary>
    public string? FallbackReply { get; set; }

    /// <summary>Tool call log from the chat turn. Consumed by UseToolCallRecorder.</summary>
    public List<(string callId, string name, string args, string result, bool success)>? PendingToolCalls { get; set; }

    // ── Debug / trace ──────────────────────────────────────
    public List<PipelineTraceEntry> PipelineTrace { get; } = [];

    public void Trace(string step, bool passed, string detail)
    {
        PipelineTrace.Add(new PipelineTraceEntry
        {
            Step = step,
            Passed = passed,
            Detail = detail,
            Time = DateTime.Now
        });
    }

    public IReadOnlyList<PipelineTraceEntry> GetTraceEntries(bool failedOnly = false)
        => failedOnly
            ? PipelineTrace.Where(t => !t.Passed).ToList()
            : PipelineTrace;

    public string FormatTraceReport(bool failedOnly = false)
    {
        var entries = GetTraceEntries(failedOnly);
        if (entries.Count == 0)
        {
            return failedOnly ? "没有失败节点" : "没有可用追踪记录";
        }

        return string.Join("\n", entries.Select(t =>
            $"- [{t.Time:HH:mm:ss}] {t.Step} | {(t.Passed ? "PASS" : "FAIL")} | {t.Detail}"));
    }
}