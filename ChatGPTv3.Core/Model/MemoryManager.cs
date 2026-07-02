using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Utilities;

namespace ChatGPTv3.Core.Model;

/// <summary>
/// Knowledge base manager — Qdrant-backed semantic search for facts the bot has been taught.
/// Short-term memory, long-term memory, and ToDo items have been removed (replaced by Diary + Mood).
/// </summary>
public static class MemoryManager
{
    public static QdrantService? Qdrant { get; private set; }

    private static string _appDir = string.Empty;

    // ── Init ──────────────────────────────────────────────────

    public static void Initialize(string appDir)
    {
        _appDir = appDir;

        if (AppConfig.EnableQdrant)
        {
            CommonHelper.LogInfo?.Invoke("Memory", $"正在连接 Qdrant ({AppConfig.QdrantHost}:{AppConfig.QdrantPort})...");
            Qdrant = new QdrantService();
            if (Qdrant.CheckHealth())
            {
                CommonHelper.LogInfo?.Invoke("Memory", "Qdrant 连接成功，创建集合...");
                Qdrant.CreateCollection(QdrantService.KnowledgeCollectionName);
                Qdrant.CreateCollection(QdrantService.ImageCollectionName);
                CommonHelper.LogInfo?.Invoke("Memory", "Qdrant 集合就绪");
            }
            else
            {
                CommonHelper.LogWarning?.Invoke("Memory", "Qdrant 连接失败，知识库已禁用");
                Qdrant = null;
            }
        }
        else
        {
            CommonHelper.LogInfo?.Invoke("Memory", "Qdrant 已禁用 (EnableQdrant=false)");
        }
    }

    // ── Knowledge ─────────────────────────────────────────────

    public static void AddKnowledge(string text)
    {
        Qdrant?.Insert(text, QdrantService.KnowledgeCollectionName);
    }

    public static (string id, string text, DateTime time, float score)[] GetKnowledge(string query)
    {
        if (Qdrant == null)
        {
            return [];
        }

        return Qdrant.Search(query, QdrantService.KnowledgeCollectionName, AppConfig.MaxMemoryCount).ToArray();
    }
}