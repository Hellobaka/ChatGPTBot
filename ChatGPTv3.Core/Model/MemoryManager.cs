using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.Core.Api;
using System.Text.Json;

namespace ChatGPTv3.Core.Model;

/// <summary>
/// Unified memory manager — short-term (in-memory + JSON),
/// long-term (Qdrant vector DB), and ToDo items.
/// </summary>
public static class MemoryManager
{
    public static List<ShortTermMemory> ShortTermMemories { get; private set; } = [];
    public static List<ToDoItem> ToDoItems { get; private set; } = [];
    public static QdrantService? Qdrant { get; private set; }

    private static Dictionary<long, int> _extractionCounts = new();
    private static string _appDir = string.Empty;
    private static readonly object _lock = new();

    // ── Init ──────────────────────────────────────────────

    public static void Initialize(string appDir)
    {
        _appDir = appDir;
        LoadShortTermMemories();
        LoadToDoItems();

        if (AppConfig.EnableQdrant)
        {
            CommonHelper.LogInfo?.Invoke("Memory", $"正在连接 Qdrant ({AppConfig.QdrantHost}:{AppConfig.QdrantPort})...");
            Qdrant = new QdrantService();
            if (Qdrant.CheckHealth())
            {
                CommonHelper.LogInfo?.Invoke("Memory", "Qdrant 连接成功，创建集合...");
                Qdrant.CreateCollection(GetLongTermCollectionName(0));
                Qdrant.CreateCollection(QdrantService.KnowledgeCollectionName);
                CommonHelper.LogInfo?.Invoke("Memory", "Qdrant 集合就绪");
            }
            else
            {
                CommonHelper.LogWarning?.Invoke("Memory", "Qdrant 连接失败，长期记忆已禁用");
                Qdrant = null;
            }
        }
        else
        {
            CommonHelper.LogInfo?.Invoke("Memory", "Qdrant 已禁用 (EnableQdrant=false)");
        }
    }

    // ── Short-term memory ─────────────────────────────────

    public static void AddShortTerm(string text, long groupId, long qq)
    {
        if (ShortTermMemory.NextId == int.MaxValue - 1) ShortTermMemory.NextId = 1;
        ShortTermMemories.Add(new ShortTermMemory
        {
            Id = ShortTermMemory.NextId++, Memory = text,
            GroupId = groupId, QQ = qq, CreateTime = DateTime.Now
        });
        SaveShortTermMemories();
    }

    public static ShortTermMemory[] GetShortTerm(long groupId, long qq)
    {
        var items = ShortTermMemories
            .Where(x => x.GroupId == groupId || (x.GroupId == -1 && x.QQ == qq)).ToArray();
        foreach (var m in items) m.UsedCount++;
        ShortTermMemories.RemoveAll(x => x.UsedCount > AppConfig.ShortTermMemoryMaxUseCount);
        SaveShortTermMemories();
        return items;
    }

    public static void RenewShortTerm(int id)
    {
        var m = ShortTermMemories.FirstOrDefault(x => x.Id == id);
        if (m != null) m.UsedCount = 0;
        SaveShortTermMemories();
    }

    public static void RemoveShortTerm(int id)
    {
        ShortTermMemories.RemoveAll(x => x.Id == id);
        SaveShortTermMemories();
    }

    // ── Long-term memory ──────────────────────────────────

    public static void AddLongTerm(string text, long qq)
    {
        Qdrant?.Insert(text, GetLongTermCollectionName(qq));
    }

    public static (string id, string text, DateTime time, float score)[] GetLongTerm(string query, long qq)
    {
        if (Qdrant == null) return [];
        return Qdrant.Search(query, GetLongTermCollectionName(qq), AppConfig.MaxMemoryCount).ToArray();
    }

    // ── Knowledge ─────────────────────────────────────────

    public static void AddKnowledge(string text)
    {
        Qdrant?.Insert(text, QdrantService.KnowledgeCollectionName);
    }

    public static (string id, string text, DateTime time, float score)[] GetKnowledge(string query)
    {
        if (Qdrant == null) return [];
        return Qdrant.Search(query, QdrantService.KnowledgeCollectionName, AppConfig.MaxMemoryCount).ToArray();
    }

    // ── ToDo ──────────────────────────────────────────────

    public static void AddToDo(string text, bool isGlobal, long groupId, long qq)
    {
        if (ToDoItem.NextId == int.MaxValue - 1) ToDoItem.NextId = 1;
        ToDoItems.Add(new ToDoItem
        {
            Id = ToDoItem.NextId++, ToDo = text, IsGlobal = isGlobal,
            GroupId = groupId, QQ = qq, CreateTime = DateTime.Now
        });
        SaveToDoItems();
    }

    public static ToDoItem[] GetToDos(long groupId, long qq)
    {
        return ToDoItems.Where(x => x.IsGlobal || x.GroupId == groupId || (x.GroupId == -1 && x.QQ == qq)).ToArray();
    }

    public static void CompleteToDo(int id)
    {
        var item = ToDoItems.FirstOrDefault(x => x.Id == id);
        if (item != null) { item.Completed = true; item.CompletedTime = DateTime.Now; }
        SaveToDoItems();
    }

    public static void RemoveToDo(int id) { ToDoItems.RemoveAll(x => x.Id == id); SaveToDoItems(); }

    // ── Memory extraction trigger ─────────────────────────

    public static bool ShouldExtract(long key)
    {
        if (!_extractionCounts.ContainsKey(key)) _extractionCounts[key] = 0;
        _extractionCounts[key]++;
        if (_extractionCounts[key] >= AppConfig.MemoryExtractionCount) { _extractionCounts[key] = 0; return true; }
        return false;
    }

    // ── Persistence ───────────────────────────────────────

    private static void SaveShortTermMemories()
    {
        lock (_lock) TrySave("ShortTermMemories.json", ShortTermMemories);
    }

    private static void SaveToDoItems()
    {
        lock (_lock) TrySave("ToDoItems.json", ToDoItems);
    }

    public static void LoadShortTermMemories()
    {
        ShortTermMemories = TryLoad<List<ShortTermMemory>>("ShortTermMemories.json");
        if (ShortTermMemories.Count > 0) ShortTermMemory.NextId = ShortTermMemories.Max(m => m.Id) + 1;
    }

    public static void LoadToDoItems()
    {
        ToDoItems = TryLoad<List<ToDoItem>>("ToDoItems.json");
        if (ToDoItems.Count > 0) ToDoItem.NextId = ToDoItems.Max(t => t.Id) + 1;
    }

    private static void TrySave<T>(string file, T data)
    {
        try { File.WriteAllText(Path.Combine(_appDir, file), data!.ToJson(true)); }
        catch (Exception ex) { CommonHelper.LogError?.Invoke("Memory", $"Save {file}: {ex.Message}"); }
    }

    private static T TryLoad<T>(string file) where T : new()
    {
        try
        {
            var path = Path.Combine(_appDir, file);
            if (File.Exists(path)) return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? new T();
        }
        catch (Exception ex) { CommonHelper.LogError?.Invoke("Memory", $"Load {file}: {ex.Message}"); }
        return new T();
    }

    private static string GetLongTermCollectionName(long qq) => $"long_term_{qq}";
}
