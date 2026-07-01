using Another_Mirai_Native.Abstractions.Services;

namespace ChatGPTv3.UI.Mock;

/// <summary>
/// Mock IAppApi for the standalone UI test environment.
/// Returns the UI bootstrap app directory and a configurable mock bot QQ.
/// ReloadPlugin/DisablePlugin are no-ops (they kill the process in real AMN2).
/// </summary>
public sealed class MockAppApi : IAppApi
{
    public static MockAppApi Instance { get; private set; } = new();

    /// <summary>The mock bot QQ used when no real AMN2 login is available.</summary>
    public long MockBotQQ { get; set; } = 10000;

    internal string AppDir { get; set; } = "";

    public static void Configure(string appDir, long mockBotQQ = 10000)
    {
        Instance.AppDir = appDir;
        Instance.MockBotQQ = mockBotQQ;
    }

    public string GetAppDirectory() => AppDir;
    public Task<string> GetAppDirectoryAsync() => Task.FromResult(AppDir);

    public long GetLoginQQ() => MockBotQQ;
    public Task<long> GetLoginQQAsync() => Task.FromResult(MockBotQQ);

    public string GetLoginQQNick() => "MockBot";
    public Task<string> GetLoginQQNickAsync() => Task.FromResult("MockBot");

    public void ReloadPlugin() { /* no-op */ }
    public Task ReloadPluginAsync() => Task.CompletedTask;

    public void DisablePlugin() { /* no-op */ }
    public Task DisablePluginAsync() => Task.CompletedTask;
}
