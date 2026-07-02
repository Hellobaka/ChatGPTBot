using SqlSugar;

namespace ChatGPTv3.Core.DB;

/// <summary>
/// User-created scheduled task triggered by cron expression.
/// When triggered, the bot proactively executes via MCP tool chain.
/// </summary>
[SugarTable("ScheduledTask")]
public class ScheduledTask
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>Human-readable task name (e.g., "提醒吃药").</summary>
    public string TaskName { get; set; } = string.Empty;

    /// <summary>Cron expression for scheduling (e.g., "0 8,20 * * *").</summary>
    public string CronExpr { get; set; } = string.Empty;

    /// <summary>Pre-computed next fire time for efficient querying.</summary>
    public DateTime NextFireAt { get; set; }

    /// <summary>Target type: 0 = Group, 1 = Private.</summary>
    public int TargetType { get; set; }

    /// <summary>Group ID (TargetType=0) or QQ (TargetType=1).</summary>
    public long TargetId { get; set; }

    /// <summary>
    /// Task description for the LLM — what to do when triggered.
    /// e.g., "提醒小王吃降压药，语气温和关心"
    /// </summary>
    public string ExtraPrompt { get; set; } = string.Empty;

    /// <summary>QQ of the user who created this task.</summary>
    public long CreatedBy { get; set; }

    /// <summary>Conversation ID (GroupID or QQ) where the task was created.</summary>
    public long CreatedById { get; set; }

    /// <summary>Whether the task is active.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>When the task was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>When the task last fired (null if never).</summary>
    public DateTime? LastFiredAt { get; set; }

    // ── CRUD ─────────────────────────────────────────────

    public static void Insert(ScheduledTask task)
    {
        using var db = SQLiteManager.GetInstance();
        db.Insertable(task).ExecuteCommand();
    }

    public static List<ScheduledTask> GetDue(DateTime now)
    {
        using var db = SQLiteManager.GetInstance();
        return db.Queryable<ScheduledTask>()
            .Where(t => t.IsEnabled && t.NextFireAt <= now)
            .ToList();
    }

    public static void Update(ScheduledTask task)
    {
        using var db = SQLiteManager.GetInstance();
        db.Updateable(task).ExecuteCommand();
    }

    public static void Delete(int id)
    {
        using var db = SQLiteManager.GetInstance();
        db.Deleteable<ScheduledTask>().Where(t => t.Id == id).ExecuteCommand();
    }

    public static List<ScheduledTask> GetAll()
    {
        using var db = SQLiteManager.GetInstance();
        return db.Queryable<ScheduledTask>().ToList();
    }
}