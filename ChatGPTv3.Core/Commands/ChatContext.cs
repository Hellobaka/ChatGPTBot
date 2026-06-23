using Another_Mirai_Native.Abstractions.Context;
using Another_Mirai_Native.Abstractions.Enums;

namespace ChatGPTv3.Core.Commands;

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

    // ── Cancellation ───────────────────────────────────────
    public CancellationToken CancellationToken { get; set; }

    // ── MCP state ──────────────────────────────────────────
    /// <summary>
    /// Images the LLM requested to see natively (via AddPictureToContext).
    /// Injected at the start of each conversation turn, then cleared.
    /// </summary>
    public List<string> PendingImageHashes { get; set; } = [];
}
