using SqlSugar;

namespace ChatGPTv3.Core.DB;

/// <summary>
/// Persisted context compression summary.
/// Uses ChatRecord primary key IDs to track exactly which messages have been
/// compressed — survives restarts with no overlap.
/// </summary>
[SugarTable("ContextSummary")]
public class ContextSummary
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>Group ID this summary belongs to.</summary>
    public long GroupId { get; set; }

    /// <summary>ChatRecord.Id of the first (oldest) compressed message.</summary>
    public int FirstMessageId { get; set; }

    /// <summary>ChatRecord.Id of the last (newest) compressed message.</summary>
    public int LastMessageId { get; set; }

    /// <summary>Number of raw messages this summary replaces.</summary>
    public int MessageCount { get; set; }

    /// <summary>LLM-generated summary text (includes time range header).</summary>
    [SugarColumn(ColumnDataType = "text")]
    public string Summary { get; set; } = string.Empty;

    /// <summary>When this summary was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // ── CRUD ─────────────────────────────────────────────

    /// <summary>
    /// Find the summary whose LastMessageId is just before the given message ID.
    /// Used by GetGroupHistory: does a summary cover messages older than what we fetched?
    /// </summary>
    public static ContextSummary? FindCoveringBefore(long groupId, int oldestFetchedId)
    {
        using var db = SQLiteManager.GetInstance();
        return db.Queryable<ContextSummary>()
            .Where(s => s.GroupId == groupId && s.LastMessageId < oldestFetchedId)
            .OrderByDescending(s => s.CreatedAt)
            .First();
    }

    /// <summary>
    /// Find the latest summary for a group (any ID range).
    /// Used after restart to recover the compression boundary.
    /// </summary>
    public static ContextSummary? FindLatest(long groupId)
    {
        using var db = SQLiteManager.GetInstance();
        return db.Queryable<ContextSummary>()
            .Where(s => s.GroupId == groupId)
            .OrderByDescending(s => s.CreatedAt)
            .First();
    }

    public static void Insert(ContextSummary summary)
    {
        using var db = SQLiteManager.GetInstance();
        summary.Id = db.Insertable(summary).ExecuteReturnIdentity();
    }

    public static void Update(ContextSummary summary)
    {
        using var db = SQLiteManager.GetInstance();
        db.Updateable(summary).ExecuteCommand();
    }

    public static void DeleteByGroup(long groupId)
    {
        using var db = SQLiteManager.GetInstance();
        db.Deleteable<ContextSummary>().Where(s => s.GroupId == groupId).ExecuteCommand();
    }
}