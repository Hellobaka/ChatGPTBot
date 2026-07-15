using SqlSugar;

namespace ChatGPTv3.Core.DB;

/// <summary>
/// Per-target configuration override (group or private chat) that overrides global AppConfig settings.
/// ConfigJson stores a full JSON snapshot of all AppConfig entries for this target.
/// Null = follow global configuration.
/// </summary>
[SugarTable("OverrideConfig")]
public class OverrideConfig
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// Target ID — GroupId when IsGroup=true, QQ number when IsGroup=false.
    /// </summary>
    public long TargetId { get; set; }

    /// <summary>
    /// True = group config, False = private chat config.
    /// </summary>
    public bool IsGroup { get; set; }

    /// <summary>
    /// Complete configuration snapshot for this target (JSON).
    /// Null = follow global configuration.
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ConfigJson { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // ── Cache ────────────────────────────────────────────

    private static readonly Dictionary<(long Id, bool IsGroup), OverrideConfig?> Cache = [];
    private static readonly object CacheLock = new();

    private static (long, bool) Key(long targetId, bool isGroup) => (targetId, isGroup);

    /// <summary>
    /// Gets override config (with caching). Returns null if no override exists.
    /// </summary>
    public static OverrideConfig? Get(long targetId, bool isGroup)
    {
        var k = Key(targetId, isGroup);
        lock (CacheLock)
        {
            if (Cache.TryGetValue(k, out var cached))
            {
                return cached;
            }
        }

        using var db = SQLiteManager.GetInstance();
        var config = db.Queryable<OverrideConfig>()
            .First(c => c.TargetId == targetId && c.IsGroup == isGroup);

        lock (CacheLock) { Cache[k] = config; }
        return config;
    }

    public static void Save(OverrideConfig config)
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

        lock (CacheLock) { Cache[Key(config.TargetId, config.IsGroup)] = config; }
    }

    public static void ClearCache(long targetId, bool isGroup)
    {
        lock (CacheLock) { Cache.Remove(Key(targetId, isGroup)); }
    }

    /// <summary>
    /// Returns all targets that have a configuration snapshot.
    /// </summary>
    public static List<OverrideConfig> GetAllConfigured()
    {
        using var db = SQLiteManager.GetInstance();
        return db.Queryable<OverrideConfig>()
            .Where(c => c.ConfigJson != null)
            .ToList();
    }

    /// <summary>
    /// Reads a single config value from the ConfigJson snapshot.
    /// Returns null if this target has no config snapshot or the key is absent.
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
    /// Deletes an override configuration, reverting to global defaults.
    /// </summary>
    public static void Delete(long targetId, bool isGroup)
    {
        using var db = SQLiteManager.GetInstance();
        db.Deleteable<OverrideConfig>().Where(c => c.TargetId == targetId && c.IsGroup == isGroup).ExecuteCommand();
        lock (CacheLock) { Cache.Remove(Key(targetId, isGroup)); }
    }
}
