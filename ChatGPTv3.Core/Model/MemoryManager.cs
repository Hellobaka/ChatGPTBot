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
            // Fire-and-forget health check and collection creation
            _ = InitializeQdrantAsync();
        }
        else
        {
            CommonHelper.LogInfo?.Invoke("Memory", "Qdrant 已禁用 (EnableQdrant=false)");
        }
    }

    private static async Task InitializeQdrantAsync()
    {
        try
        {
            if (await Qdrant!.CheckHealthAsync())
            {
                CommonHelper.LogInfo?.Invoke("Memory", "Qdrant 连接成功，创建集合...");
                await Qdrant.CreateCollectionAsync(QdrantService.KnowledgeCollectionName);
                await Qdrant.CreateCollectionAsync(QdrantService.ImageCollectionName);
                CommonHelper.LogInfo?.Invoke("Memory", "Qdrant 集合就绪");
            }
            else
            {
                CommonHelper.LogWarning?.Invoke("Memory", "Qdrant 连接失败，知识库已禁用");
                Qdrant = null;
            }
        }
        catch (Exception ex)
        {
            CommonHelper.LogError?.Invoke("Memory", $"Qdrant 初始化失败: {ex.Message}");
            Qdrant = null;
        }
    }

    // ── Knowledge ─────────────────────────────────────────────

    public static void AddKnowledge(string text)
    {
        // Fire-and-forget: don't block the caller
        _ = Task.Run(async () =>
        {
            try
            {
                await Qdrant?.InsertAsync(text, QdrantService.KnowledgeCollectionName)!;
            }
            catch { }
        });
    }

    public static async Task<(string id, string text, DateTime time, float score)[]> GetKnowledgeAsync(string query)
    {
        if (Qdrant == null)
        {
            return [];
        }

        return (await Qdrant.SearchAsync(query, QdrantService.KnowledgeCollectionName, AppConfig.MaxMemoryCount)).ToArray();
    }
}