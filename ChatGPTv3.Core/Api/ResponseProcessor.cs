using System.Text.RegularExpressions;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.Utilities;

namespace ChatGPTv3.Core.Api;

/// <summary>
/// Post-processes LLM responses: think block removal, empty response handling.
/// Now instance-based — each ChatService owns its own processor to avoid
/// reasoning leakage across concurrent sessions.
/// </summary>
public class ResponseProcessor
{
    private static readonly Regex ThinkBlockRegex = new(@"<think>[\s\S]*?</think>", RegexOptions.Compiled);
    private static readonly Regex ThinkBlockRegex2 = new(@"[\s\S]*?</think>", RegexOptions.Compiled);
    private string? _pendingReasoning;

    /// <summary>
    /// Retains the last collected reasoning content for external consumers
    /// (e.g. ChatTest UI) after ProcessResponse has consumed it.
    /// </summary>
    public string? LastReasoning { get; private set; }

    public void Reset()
    {
        _pendingReasoning = null;
        LastReasoning = null;
    }

    /// <summary>
    /// Accumulates reasoning content from streaming (DeepSeek R1 style).
    /// </summary>
    public void AppendReasoning(string? reasoning)
    {
        if (!string.IsNullOrEmpty(reasoning))
        {
            _pendingReasoning = (_pendingReasoning ?? "") + reasoning;
        }
    }

    /// <summary>
    /// Processes the final accumulated message.
    /// Removes think blocks and handles empty responses.
    /// </summary>
    public string ProcessResponse(string msg)
    {
        // Persist for consumers before resetting
        LastReasoning = _pendingReasoning;

        if (AppConfig.RemoveThinkBlock)
        {
            // Extract and log reasoning
            if (AppConfig.LogThinkBlock && !string.IsNullOrEmpty(LastReasoning))
            {
                CommonHelper.LogInfo?.Invoke("思考内容", LastReasoning);
            }

            // Remove think block from message if present
            var thinkMatch = ThinkBlockRegex.Match(msg);
            if (thinkMatch.Success)
            {
                msg = ThinkBlockRegex.Replace(msg, "").TrimStart('\r', '\n', ' ');
            }
            thinkMatch = ThinkBlockRegex2.Match(msg);
            if (thinkMatch.Success)
            {
                msg = ThinkBlockRegex2.Replace(msg, "").TrimStart('\r', '\n', ' ');
            }
        }

        _pendingReasoning = null;
        return msg;
    }
}
