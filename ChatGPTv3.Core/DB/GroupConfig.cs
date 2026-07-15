using SqlSugar;

namespace ChatGPTv3.Core.DB;

/// <summary>
/// Per-group configuration that overrides global AppConfig settings.
/// ConfigJson stores a full JSON snapshot of all AppConfig entries for this group.
/// Null = follow global configuration.
/// </summary>
[SugarTable("GroupConfig")]
public class GroupConfig
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>Group ID (unique).</summary>
    public long GroupID { get; set; }

    /// <summary>
    /// Complete configuration snapshot for this group (JSON).
    /// When set, all config reads for this group use this snapshot instead of global AppConfig.
    /// Null = follow global configuration.
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ConfigJson { get; set; }

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
            if (Cache.TryGetValue(groupId, out var cached))
            {
                return cached;
            }
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
        {
            db.Updateable(config).ExecuteCommand();
        }
        else
        {
            config.Id = db.Insertable(config).ExecuteReturnIdentity();
        }

        lock (CacheLock) { Cache[config.GroupID] = config; }
    }

    public static void ClearCache(long groupId)
    {
        lock (CacheLock) { Cache.Remove(groupId); }
    }

    /// <summary>
    /// Returns all groups that have a configuration snapshot.
    /// </summary>
    public static List<GroupConfig> GetAllConfigured()
    {
        using var db = SQLiteManager.GetInstance();
        return db.Queryable<GroupConfig>()
            .Where(c => c.ConfigJson != null)
            .OrderBy(c => c.UpdatedAt)
            .ToList();
    }

    /// <summary>
    /// Reads a single config value from the ConfigJson snapshot.
    /// Returns null if this group has no config snapshot or the key is absent.
    /// For value types the return is T? (Nullable&lt;T&gt;) so callers can use ?? fallback.
    /// </summary>
    public T? GetConfigValue<T>(string key)
    {
        if (string.IsNullOrEmpty(ConfigJson))
        {
            return default;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(ConfigJson);
            if (doc.RootElement.TryGetProperty(key, out var elem))
            {
                var raw = elem.GetRawText();
                return System.Text.Json.JsonSerializer.Deserialize<T>(raw);
            }
        }
        catch { }

        return default;
    }

    /// <summary>
    /// Deletes a group configuration, reverting to global defaults.
    /// </summary>
    public static void Delete(long groupId)
    {
        using var db = SQLiteManager.GetInstance();
        db.Deleteable<GroupConfig>().Where(c => c.GroupID == groupId).ExecuteCommand();
        lock (CacheLock) { Cache.Remove(groupId); }
    }
}