using System.Text.RegularExpressions;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.Utilities;

namespace ChatGPTv3.Core.Api;

/// <summary>
/// Post-processes LLM responses: think block removal, empty response handling.
/// No longer uses reflection-based OpenAI SDK access.
/// </summary>
public static class ResponseProcessor
{
    private static readonly Regex ThinkBlockRegex = new(@"<think>[\s\S]*?</think>", RegexOptions.Compiled);
    private static string? _pendingReasoning;

    public static void Reset()
    {
        _pendingReasoning = null;
    }

    /// <summary>
    /// Accumulates reasoning content from streaming (DeepSeek R1 style).
    /// </summary>
    public static void AppendReasoning(string? reasoning)
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
    public static string ProcessResponse(string msg)
    {
        if (AppConfig.RemoveThinkBlock && !string.IsNullOrEmpty(_pendingReasoning))
        {
            // Extract and log reasoning
            if (AppConfig.LogThinkBlock)
            {
                CommonHelper.LogInfo?.Invoke("思考内容", _pendingReasoning);
            }

            // Remove think block from message if present
            var thinkMatch = ThinkBlockRegex.Match(msg);
            if (thinkMatch.Success)
            {
                msg = ThinkBlockRegex.Replace(msg, "").TrimStart('\r', '\n', ' ');
            }
        }

        _pendingReasoning = null;
        return msg;
    }
}
