using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Config;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Api;

/// <summary>
/// Groups chat records into user/assistant pairs for LLM context.
/// Ensures strict role alternation (user→assistant→user→assistant).
/// Failed tool call results are excluded from context.
/// </summary>
public static class ChatHistoryGrouper
{
    /// <summary>
    /// Groups raw chat records into alternating user/assistant ChatMessage objects.
    /// </summary>
    /// <param name="records">Chat records in time-descending order (newest first).</param>
    /// <param name="botQQ">The bot's QQ number to identify self-messages.</param>
    /// <param name="successfulToolResults">Only successful tool call results to include.</param>
    /// <returns>ChatMessage list in chronological order (oldest first).</returns>
    public static List<ChatMessage> GroupMessages(
        List<ChatRecord> records,
        long botQQ,
        List<ToolCallResult>? successfulToolResults = null)
    {
        if (records.Count == 0) return [];

        // Reverse to chronological order (oldest first)
        records = records.OrderBy(r => r.Time).ToList();

        var result = new List<ChatMessage>();
        var currentRole = string.Empty;
        var currentContent = new System.Text.StringBuilder();
        int currentGroupCount = 0;

        foreach (var record in records)
        {
            var isBot = record.QQ == botQQ;
            var role = isBot ? "assistant" : "user";

            // Format this message line
            string line;
            if (isBot)
            {
                line = $"{record.ParsedMessage}";
            }
            else
            {
                // Include sender info for group chats
                if (record.GroupID > 0)
                {
                    line = $"{record.NickName}[{record.QQ}]: {record.ParsedMessage}";
                }
                else
                {
                    line = record.ParsedMessage;
                }
            }

            // Start a new group if role changes or group gets too large
            if (role != currentRole || currentGroupCount >= 5)
            {
                if (currentContent.Length > 0)
                {
                    result.Add(CreateMessage(currentRole, currentContent.ToString()));
                }
                currentRole = role;
                currentContent.Clear();
                currentContent.Append(line);
                currentGroupCount = 1;
            }
            else
            {
                currentContent.Append('\n');
                currentContent.Append(line);
                currentGroupCount++;
            }
        }

        // Add the last group
        if (currentContent.Length > 0)
        {
            result.Add(CreateMessage(currentRole, currentContent.ToString()));
        }

        return result;
    }

    /// <summary>
    /// Converts grouped messages + tool call results into the final API message list,
    /// including only successful tool calls in the context.
    /// </summary>
    public static List<ChatMessage> BuildApiMessages(
        List<ChatMessage> groupedHistory,
        List<ToolCallResult>? successfulToolResults)
    {
        var messages = new List<ChatMessage>();

        foreach (var msg in groupedHistory)
        {
            messages.Add(msg);

            // Insert successful tool results after assistant messages with tool calls
            if (msg.Role == "assistant" && msg.ToolCalls != null && successfulToolResults != null)
            {
                foreach (var tc in msg.ToolCalls)
                {
                    var toolResult = successfulToolResults.FirstOrDefault(r => r.ToolCallId == tc.Id);
                    if (toolResult != null && toolResult.IsSuccess)
                    {
                        messages.Add(ChatMessage.Tool(tc.Id, toolResult.Result));
                    }
                    // Failed tool calls are silently skipped (not added to context)
                }
            }
        }

        return messages;
    }

    private static ChatMessage CreateMessage(string role, string content)
    {
        return new ChatMessage
        {
            Role = role,
            Content = content
        };
    }
}
