using ChatGPTv3.Core.DB;

namespace ChatGPTv3.Core.Api;

/// <summary>
/// Lightweight context compression: when history exceeds the limit,
/// keeps the most recent messages and inserts a summary of the oldest.
///
/// TODO: Replace truncation with LLM-based summarization (Phase 5):
///   - Feed dropped messages to a cheap LLM with a summary prompt
///   - Insert the LLM-generated summary instead of "[省略了N条消息]"
///   - Reuse the same pattern as ToolResultSummarizer (fixed system prompt for caching)
/// Currently uses simple truncation — zero cost, zero latency, but information is lost.
/// </summary>
public static class ContextCompressor
{
    /// <summary>
    /// Compresses chat history to fit within the target count.
    /// Keeps the most recent messages and prepends a summary marker.
    /// </summary>
    /// <param name="records">Full chat history (newest first, from DB).</param>
    /// <param name="targetCount">Target message count to keep.</param>
    /// <returns>Compressed history list (oldest first).</returns>
    public static List<ChatRecord> Compress(List<ChatRecord> records, int targetCount)
    {
        if (records.Count <= targetCount) return records;

        // TODO: Phase 5 — LLM-based summarization. Replace truncation with:
        //   var summaryText = await SummarizeWithLLM(dropped);
        //   For now: placeholder truncation.

        var recent = records.Take(targetCount).Reverse().ToList();

        // Generate a simple truncation summary of the dropped messages
        var dropped = records.Skip(targetCount).ToList();
        var droppedCount = dropped.Count;
        var timeSpan = dropped.Count > 1
            ? $"{dropped.Last().Time:HH:mm} - {dropped.First().Time:HH:mm}"
            : $"{dropped.First().Time:HH:mm}";

        var summary = new ChatRecord
        {
            GroupID = recent.FirstOrDefault()?.GroupID ?? 0,
            QQ = 0,
            SenderType = SenderType.User,
            ParsedMessage = $"[省略了 {droppedCount} 条更早的消息 ({timeSpan})]",
            Time = dropped.First().Time
        };

        recent.Insert(0, summary);
        return recent;
    }
}
