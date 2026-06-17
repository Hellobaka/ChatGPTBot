using ChatGPTv3.Core.Utilities;

namespace ChatGPTv3.Core.Model;

/// <summary>
/// Minimal natural-language mood state.
/// Replaces v2's valence/arousal math model.
///
/// Mood = one sentence of self-description about "how I feel in this group".
/// Stored per-context (groupId or qq), persisted to JSON.
///
/// Dual trigger:
///   1. LLM calls UpdateMood during conversation (MCP tool)
///   2. Diary updates mood nightly (Phase 5)
///
/// Time-weakening: if mood is > 24h old, wording weakens automatically
/// ("你之前的心情...已经过去了") so LLM is not bound to old moods.
/// </summary>
public static class MoodState
{
    private static readonly Dictionary<long, (string mood, DateTime updatedAt)> _moods = [];
    private static string _appDir = string.Empty;
    private static readonly object _lock = new();

    public static void Initialize(string appDir)
    {
        _appDir = appDir;
        Load();
    }

    /// <summary>Get mood text for a context (groupId or qq). Returns null if not set.</summary>
    public static string? GetMood(long contextId)
    {
        lock (_lock)
        {
            if (!_moods.TryGetValue(contextId, out var entry))
                return null;

            var age = DateTime.Now - entry.updatedAt;

            // Time-weakening: adjust wording based on age
            if (age.TotalHours < 6)
                return $"你当前的心情：{entry.mood}";
            if (age.TotalHours < 24)
                return $"你今天的心情：{entry.mood}";
            return $"你之前在这个群里的心情：{entry.mood}（已经过去了）";
        }
    }

    /// <summary>Called by MCP UpdateMood tool or diary.</summary>
    public static void UpdateMood(long contextId, string moodText)
    {
        lock (_lock)
        {
            _moods[contextId] = (moodText, DateTime.Now);
        }
        TrySave();
    }

    // ── Persistence ──────────────────────────────────────────

    private static string FilePath => Path.Combine(_appDir, "MoodState.json");

    private static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, MoodEntry>>(json);
                if (data != null)
                {
                    lock (_lock)
                    {
                        foreach (var (key, entry) in data)
                        {
                            if (long.TryParse(key, out var id))
                                _moods[id] = (entry.Mood, entry.UpdatedAt);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            CommonHelper.LogWarning?.Invoke("MoodState", $"加载失败: {ex.Message}");
        }
    }

    private static void TrySave()
    {
        try
        {
            Dictionary<string, MoodEntry> data;
            lock (_lock)
            {
                data = _moods.ToDictionary(k => k.Key.ToString(), v => new MoodEntry
                {
                    Mood = v.Value.mood,
                    UpdatedAt = v.Value.updatedAt
                });
            }
            File.WriteAllText(FilePath, System.Text.Json.JsonSerializer.Serialize(data));
        }
        catch (Exception ex)
        {
            CommonHelper.LogWarning?.Invoke("MoodState", $"保存失败: {ex.Message}");
        }
    }

    private class MoodEntry
    {
        public string Mood { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}
