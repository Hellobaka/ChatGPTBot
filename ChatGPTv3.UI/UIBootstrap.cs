using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Model;
using ChatGPTv3.Core.Model.MCP;
using ChatGPTv3.UI.Mock;

namespace ChatGPTv3.UI;

/// <summary>
/// Standalone bootstrap — replicates Entry.OnEnableAsync() initialization
/// without AMN2 host dependencies. Used for direct UI debugging (WinExe mode).
/// When running as AMN2 plugin, Core's Entry already initializes everything.
/// </summary>
public static class UIBootstrap
{
    public static string AppDir { get; private set; } = string.Empty;

    public static bool IsInitialized { get; private set; }

    public static void Initialize(string appDir)
    {
        if (IsInitialized)
        {
            return;
        }

        AppDir = appDir;

        // ── Mock Plugin API (replaces the AMN2 host IPluginApi) ──
        // Provides Logger/MessageApi/GroupApi/FriendApi/AppApi for the test environment.
        // GroupApi/FriendApi proxy to Entry.ApiGroup/Entry.ApiFriend if available (AMN2 runtime),
        // otherwise return empty lists.
        MockPluginApi.Initialize(appDir);

        // ── Bot QQ (for At-bot and context identity) ──
        ChatGPTv3.Core.Api.PromptBuilder.CurrentBotQQ = MockAppApi.Instance.MockBotQQ;

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