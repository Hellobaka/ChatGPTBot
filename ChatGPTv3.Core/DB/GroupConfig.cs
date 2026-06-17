using SqlSugar;

namespace ChatGPTv3.Core.DB;

/// <summary>
/// Per-group configuration that overrides global AppConfig settings.
/// Priority: GroupConfig > AppConfig. Cached on first lookup.
/// </summary>
[SugarTable("GroupConfig")]
public class GroupConfig
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>Group ID (unique).</summary>
    public long GroupID { get; set; }

    /// <summary>Custom personality prompt — overrides global GroupPrompt if set.</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? CustomPrompt { get; set; }

    /// <summary>Override for EnableMemory (null = follow global).</summary>
    public bool? EnableMemory { get; set; }

    /// <summary>Override for EnableMCP (null = follow global).</summary>
    public bool? EnableMCP { get; set; }

    /// <summary>Override for EnableEmojiPassiveSend (null = follow global).</summary>
    public bool? EnableEmoji { get; set; }

    /// <summary>Override for ReplyWillingAmplifier (null = follow global).</summary>
    public double? ReplyAmplifier { get; set; }

    /// <summary>Group-specific bot nicknames (comma-separated). Null = follow global.</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? CustomNicknames { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // ── Cache ────────────────────────────────────────────

    private static readonly Dictionary<long, GroupConfig?> Cache = [];
    private static readonly object CacheLock = new();

    /// <summary>
    /// Gets group config (with caching). Returns null if no override exists.
    /// </summary>
    public static GroupConfig? Get(long groupId)
    {
        lock (CacheLock)
        {
            if (Cache.TryGetValue(groupId, out var cached)) return cached;
        }

        using var db = SQLiteManager.GetInstance();
        var config = db.Queryable<GroupConfig>()
            .First(c => c.GroupID == groupId);

        lock (CacheLock) { Cache[groupId] = config; }
        return config;
    }

    public static void Save(GroupConfig config)
    {
        config.UpdatedAt = DateTime.Now;
        using var db = SQLiteManager.GetInstance();
        if (config.Id > 0)
            db.Updateable(config).ExecuteCommand();
        else
            db.Insertable(config).ExecuteCommand();

        lock (CacheLock) { Cache[config.GroupID] = config; }
    }

    public static void ClearCache(long groupId)
    {
        lock (CacheLock) { Cache.Remove(groupId); }
    }
}
