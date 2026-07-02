using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Api;

/// <summary>
/// Post-hoc summarization of tool call results.
///
/// A "group" = one conversation turn's worth of tool calls:
///   User → [ToolCall1 → Result1] → [ToolCall2 → Result2] → Assistant FinalMessage
///
/// The LLM observes the entire group and produces ONE summary covering all tool calls
/// — what information was actually used in the final response.
///
/// RawMessage is always empty — the LLM summary in ParsedMessage is the canonical record.
/// Fire-and-forget — does not block the reply.
/// </summary>
public static class ToolResultSummarizer
{
    /// <summary>
    /// FIXED system prompt — cacheable. Per-group data goes into the user message.
    /// </summary>
    private const string SummarySystemPrompt = """
你是一个对话记录编辑器。观察一组工具调用的完整过程，
只记录对后续对话有意义的、被机器人实际采纳的信息。

请用 1-2 句话总结这组工具调用。规则：
- 只记录机器人在回复中实际使用或参考的信息
- 如果工具返回了大量数据但机器人只用了其中一部分，注明"仅使用了X"
- 如果某个工具调用失败或机器人忽略了结果，只记录"调用失败: {原因}"
- 不描述过程，只记录结果
""";

    /// <summary>
    /// Triggers async summarization for one turn's tool call group.
    /// Fire-and-forget — does not block the calling thread.
    /// Produces ONE ChatRecord per turn (not per tool call).
    /// </summary>
    /// <param name="groupId">Group ID (context for DB insertion).</param>
    /// <param name="qq">User QQ.</param>
    /// <param name="userMessage">The user's message that triggered this turn.</param>
    /// <param name="toolCalls">All tool calls in this turn (callId, name, args, result, success).</param>
    /// <param name="finalResponse">The bot's final response text.</param>
    public static void SummarizeAsync(
        long groupId,
        string userMessage,
        List<(string callId, string name, string args, string result, bool success)> toolCalls,
        string finalResponse,
        List<int> placeholderIds)
    {
        if (toolCalls.Count == 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var summary = await SummarizeGroup(toolCalls, userMessage, finalResponse);

                // UPDATE all placeholder records with the merged summary
                foreach (var id in placeholderIds)
                {
                    ChatRecord.UpdateParsedMessage(id, summary);
                }
            }
            catch (Exception ex)
            {
                var fallback = $"[{toolCalls.Count} 次工具调用: {string.Join(", ", toolCalls.Select(t => t.name))}]";
                foreach (var id in placeholderIds)
                {
                    ChatRecord.UpdateParsedMessage(id, fallback);
                }

                CommonHelper.LogWarning?.Invoke("Summarizer", $"总结失败: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Summarizes a group of tool calls into one concise description.
    /// Builds a proper User→Assistant(tool_calls)→Tool→Assistant message chain
    /// so the LLM sees the conversation in its natural format.
    /// </summary>
    private static async Task<string> SummarizeGroup(
        List<(string callId, string name, string args, string result, bool success)> toolCalls,
        string userMessage,
        string finalResponse)
    {
        try
        {
            var keys = AppConfig.SummarizerApiKeyId.Count > 0
                ? AppConfig.SummarizerApiKeyId
                : AppConfig.ChatAPIKeyId;
            if (keys.Count == 0)
            {
                return FallbackText(toolCalls);
            }

            // Build structured messages: System → User → Assistant(tool_calls) → Tool → Assistant(final)
            var messages = new List<ChatMessage>
            {
                ChatMessage.System(SummarySystemPrompt),
                ChatMessage.User(userMessage)
            };

            // Assistant with tool_calls — use real call IDs so tool results link correctly
            var toolCallList = toolCalls.Select(tc =>
                new ToolCallRequest
                {
                    Id = string.IsNullOrEmpty(tc.callId) ? $"call_{Guid.NewGuid():N}"[..8] : tc.callId,
                    Type = "function",
                    Function = new FunctionCall
                    {
                        Name = tc.name,
                        Arguments = Truncate(tc.args ?? "{}", 200)
                    }
                }).ToList();

            messages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = null,
                ToolCalls = toolCallList
            });

            // Tool results — link each to its call ID
            for (int i = 0; i < toolCalls.Count; i++)
            {
                messages.Add(ChatMessage.Tool(toolCallList[i].Id, Truncate(toolCalls[i].result, 3000)));
            }

            // Final assistant response
            messages.Add(ChatMessage.Assistant(Truncate(finalResponse, 1000)));

            var chatService = new ChatService();
            var result = await chatService.GetChatResultAsync(
                keys, messages,
                ChatService.Purpose.工具总结,
                timeout: 15000);

            if (result == ChatService.ErrorMessage || string.IsNullOrWhiteSpace(result))
            {
                return FallbackText(toolCalls);
            }

            return result.Trim();
        }
        catch
        {
            return FallbackText(toolCalls);
        }
    }

    private static string FallbackText(List<(string callId, string name, string args, string result, bool success)> toolCalls)
        => toolCalls.Count == 1
            ? Truncate(toolCalls[0].result, 200)
            : $"[{toolCalls.Count} 次工具调用: {string.Join(", ", toolCalls.Select(t => t.name))}]";

    private static string Truncate(string text, int maxLen)
        => text.Length > maxLen ? text[..maxLen] + "...(已截断)" : text;
}