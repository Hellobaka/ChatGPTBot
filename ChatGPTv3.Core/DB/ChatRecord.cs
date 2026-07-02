using SqlSugar;

namespace ChatGPTv3.Core.DB;

public enum SenderType
{
    User = 0,
    Assistant = 1,
    Tool = 2,
}

/// <summary>
/// Chat message record stored in SQLite.
/// </summary>
[SugarTable("ChatRecord")]
[SugarIndex("IX_ChatRecord_GroupID_Time", nameof(GroupID), OrderByType.Asc, nameof(Time), OrderByType.Desc)]
[SugarIndex("IX_ChatRecord_QQ_Time", nameof(QQ), OrderByType.Asc, nameof(Time), OrderByType.Desc)]
[SugarIndex("IX_ChatRecord_GroupID_MessageID", nameof(GroupID), OrderByType.Asc, nameof(MessageID), OrderByType.Asc)]
public class ChatRecord
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public long GroupID { get; set; }

    public long QQ { get; set; }

    public SenderType SenderType { get; set; }

    public string NickName { get; set; } = string.Empty;

    [SugarColumn(ColumnName = "RawMessage")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Parsed/human-readable message text. For Tool records: LLM summary of the tool result.
    /// For User records: extracted text from message chain. For Assistant: final reply text.
    /// </summary>
    [SugarColumn(ColumnDataType = "text")]
    public string ParsedMessage { get; set; } = string.Empty;

    public long MessageID { get; set; }

    public bool IsMentioned { get; set; }

    public bool IsImage { get; set; }

    public bool IsEmpty { get; set; }

    public bool IsInvalidReply { get; set; }

    /// <summary>Whether this assistant message includes tool_calls (for grouping).</summary>
    public bool HasToolCalls { get; set; }

    /// <summary>OpenAI tool call ID — links Tool records to Assistant tool_calls.</summary>
    [SugarColumn(IsNullable = true)]
    public string? ToolCallId { get; set; }

    /// <summary>Name of the tool that was called (for statistics/debugging).</summary>
    [SugarColumn(IsNullable = true)]
    public string? ToolName { get; set; }

    /// <summary>Whether the tool call succeeded.</summary>
    public bool IsToolSuccess { get; set; } = true;

    public DateTime Time { get; set; } = DateTime.Now;

    // ── CRUD ─────────────────────────────────────────────

    public static int Insert(ChatRecord record)
    {
        using var db = SQLiteManager.GetInstance();
        return db.Insertable(record).ExecuteReturnIdentity();
    }

    public static void UpdateParsedMessage(int id, string parsedMessage)
    {
        using var db = SQLiteManager.GetInstance();
        db.Updateable<ChatRecord>()
            .SetColumns(r => r.ParsedMessage == parsedMessage)
            .Where(r => r.Id == id)
            .ExecuteCommand();
    }

    /// <summary>
    /// Get recent group chat history.
    /// </summary>
    /// <param name="includeSummary">When true (default), merges saved ContextSummary for older messages.</param>
    public static List<ChatRecord> GetGroupHistory(long groupId, int count = 20, bool includeSummary = true)
    {
        using var db = SQLiteManager.GetInstance();
        // Take the most recent N messages, return them in chronological order (oldest first)
        var newest = db.Queryable<ChatRecord>()
            .Where(r => r.GroupID == groupId)
            .OrderByDescending(r => r.Time)
            .Take(count)
            .ToList();
        newest.Reverse();

        // Check if there's a saved summary covering messages older than our oldest fetched record
        if (includeSummary && newest.Count > 0)
        {
            var oldestId = newest[0].Id;
            var summary = ContextSummary.FindCoveringBefore(groupId, oldestId);
            if (summary != null)
            {
                newest.Insert(0, new ChatRecord
                {
                    GroupID = groupId,
                    QQ = 0,
                    SenderType = SenderType.User,
                    ParsedMessage = summary.Summary,
                    Time = DateTime.Now
                });
            }
        }

        return newest;
    }

    /// <summary>
    /// Get recent private chat history.
    /// </summary>
    public static List<ChatRecord> GetPrivateHistory(long qq, int count = 20)
    {
        using var db = SQLiteManager.GetInstance();
        var newest = db.Queryable<ChatRecord>()
            .Where(r => r.QQ == qq || r.QQ == 0)
            .OrderByDescending(r => r.Time)
            .Take(count)
            .ToList();
        newest.Reverse();
        return newest;
    }

    /// <summary>
    /// Get group chat history within a time range (for diary, etc.).
    /// </summary>
    /// <param name="includeSummary">When true (default), merges saved ContextSummary for older messages.</param>
    public static List<ChatRecord> GetGroupHistoryByTime(long groupId, DateTime since, int maxCount = 500, bool includeSummary = true)
    {
        using var db = SQLiteManager.GetInstance();
        var result = db.Queryable<ChatRecord>()
            .Where(r => r.GroupID == groupId && r.Time > since)
            .OrderBy(r => r.Time)
            .Take(maxCount)
            .ToList();

        if (includeSummary && result.Count > 0)
        {
            var oldestId = result[0].Id;
            var summary = ContextSummary.FindCoveringBefore(groupId, oldestId);
            if (summary != null)
            {
                result.Insert(0, new ChatRecord
                {
                    GroupID = groupId,
                    QQ = 0,
                    SenderType = SenderType.User,
                    ParsedMessage = summary.Summary,
                    Time = DateTime.Now
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Count messages for a group since a given time.
    /// </summary>
    public static int CountSince(long groupId, DateTime since)
    {
        using var db = SQLiteManager.GetInstance();
        return db.Queryable<ChatRecord>()
            .Where(r => r.GroupID == groupId && r.Time > since)
            .Count();
    }

    /// <summary>
    /// Get messages by their QQ message IDs.
    /// </summary>
    public static List<ChatRecord> GetByIds(long[] messageIds, long groupId)
    {
        using var db = SQLiteManager.GetInstance();
        return db.Queryable<ChatRecord>()
            .Where(r => messageIds.Contains(r.MessageID) && r.GroupID == groupId)
            .ToList();
    }

    /// <summary>
    /// Delete old records beyond the keep count for a group.
    /// </summary>
    public static void Cleanup(long groupId, int keepCount = 500)
    {
        using var db = SQLiteManager.GetInstance();
        var toDelete = db.Queryable<ChatRecord>()
            .Where(r => r.GroupID == groupId)
            .OrderByDescending(r => r.Time)
            .Skip(keepCount)
            .Select(r => r.Id)
            .ToList();
        if (toDelete.Count > 0)
        {
            db.Deleteable<ChatRecord>().In(toDelete).ExecuteCommand();
        }
    }
}