using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Model;
using ChatGPTv3.Core.Model.MCP;

namespace ChatGPTv3.UI;

/// <summary>
/// Standalone bootstrap — replicates Entry.OnEnableAsync() initialization
/// without AMN2 host dependencies. All AMN2-specific paths are null-guarded.
/// </summary>
public static class UIBootstrap
{
    public static string AppDir { get; private set; } = string.Empty;
    public static bool IsInitialized { get; private set; }

    public static void Initialize(string appDir)
    {
        if (IsInitialized) return;
        AppDir = appDir;

        // ── Config ──
        ConfigManager.Initialize(appDir);
        ConfigManager.Load();

        // ── Database (must be before AppConfig.Init which queries APIKeys) ──
        SQLiteManager.AppDirectory = appDir;
        SQLiteManager.CreateDB();

        AppConfig.Init();

        // ── MCP ──
        MCPClientManager.Load(appDir);
        MCPClientManager.Rebuild();

        // ── Memory / Qdrant ──
        MemoryManager.Initialize(appDir);

        // ── Diary ──
        DiaryMemoryManager.Initialize(appDir);

        // ── Mood ──
        MoodState.Initialize(appDir);

        // ── Scheduler ──
        _ = new SchedulerManager(appDir);

        IsInitialized = true;
    }
}
