using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;
using System.Collections.Concurrent;

namespace ChatGPTv3.Core.Api;

/// <summary>
/// Tracks tool call counts per conversation identity.
/// Enforces MaxToolCallCountEachTurn and AbortToolCallCountEachTurn limits.
/// Also tracks which tool calls succeed/fail for context filtering.
/// </summary>
public class ToolCallService
{
    private static readonly ConcurrentDictionary<string, int> _counts = new();
    private static readonly ConcurrentDictionary<string, bool> _abortList = new();

    /// <summary>
    /// Tracks tool call results for this conversation turn.
    /// Key: identity, Value: (toolCallId -> success)
    /// </summary>
    private static readonly ConcurrentDictionary<string, List<ToolCallResult>> _results = new();

    public void ResetToolCallState(string identity)
    {
        if (identity == null)
        {
            return;
        }

        _counts.AddOrUpdate(identity, 0, (_, _) => 0);
        _results[identity] = [];
        _abortList.TryRemove(identity, out _);
    }

    public void CleanupToolCallState(string identity)
    {
        if (identity == null)
        {
            return;
        }

        _counts.TryRemove(identity, out _);
        _results.TryRemove(identity, out _);
        _abortList.TryRemove(identity, out _);
    }

    public bool ShouldAbortConversation(string identity)
    {
        return identity != null && _abortList.ContainsKey(identity);
    }

    /// <summary>
    /// Records a tool call invocation and enforces limits.
    /// Returns the max tool call message if limits exceeded.
    /// </summary>
    public async Task<(object? result, ToolCallResult? trackResult)> LogToolCallAsync(
        ToolCallRequest toolCall,
        Func<ToolCallRequest, CancellationToken, Task<object?>> invoker,
        string identity,
        CancellationToken token)
    {
        CommonHelper.DebugLog("ToolCall", $"调用函数 {toolCall.Function.Name}，参数 {toolCall.Function.Arguments}");

        try
        {
            if (identity != null && _counts.TryGetValue(identity, out int count))
            {
                var newCount = _counts.AddOrUpdate(identity, 1, (_, v) => v + 1);

                if (newCount > AppConfig.AbortToolCallCountEachTurn)
                {
                    CommonHelper.LogWarning?.Invoke("ToolCall", $"本轮已调用 {newCount} 次，强制终止");
                    _abortList[identity] = true;
                    return ("已达到本轮流式工具调用次数上限，本次会话将被中止。", null);
                }
                if (newCount > AppConfig.MaxToolCallCountEachTurn)
                {
                    CommonHelper.LogWarning?.Invoke("ToolCall", $"本轮已调用 {newCount} 次，无法再调用");
                    return ("已达到本轮工具调用次数上限，你无法再调用任何工具。", null);
                }
            }

            var result = await invoker(toolCall, token);
            CommonHelper.DebugLog("ToolCall", $"函数 {toolCall.Function.Name} 完成，结果 {result}");

            var trackResult = new ToolCallResult
            {
                ToolCallId = toolCall.Id,
                FunctionName = toolCall.Function.Name,
                Result = result?.ToString() ?? string.Empty,
                IsSuccess = true
            };

            // Record result
            if (identity != null)
            {
                _results.AddOrUpdate(identity,
                    _ => [trackResult],
                    (_, list) => { list.Add(trackResult); return list; });
            }

            return (result, trackResult);
        }
        catch (Exception ex)
        {
            CommonHelper.LogWarning?.Invoke("ToolCall", $"函数 {toolCall.Function.Name} 失败: {ex.Message}");

            var trackResult = new ToolCallResult
            {
                ToolCallId = toolCall.Id,
                FunctionName = toolCall.Function.Name,
                Result = ex.Message,
                IsSuccess = false
            };

            if (identity != null)
            {
                _results.AddOrUpdate(identity,
                    _ => [trackResult],
                    (_, list) => { list.Add(trackResult); return list; });
            }

            return ($"Error: {ex.Message}", trackResult);
        }
    }

    /// <summary>
    /// Gets tool call results for this conversation turn.
    /// </summary>
    public List<ToolCallResult> GetToolCallResults(string identity)
    {
        if (identity == null)
        {
            return [];
        }

        return _results.GetValueOrDefault(identity, []);
    }

    /// <summary>
    /// Gets only the successful tool call results (for context recording).
    /// </summary>
    public List<ToolCallResult> GetSuccessfulResults(string identity)
    {
        return GetToolCallResults(identity).Where(r => r.IsSuccess).ToList();
    }
}