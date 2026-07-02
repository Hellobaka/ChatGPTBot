using Another_Mirai_Native.Abstractions.Services;

namespace ChatGPTv3.UI.Mock;

/// <summary>
/// Complete mock IPluginApi — aggregates Logger, MessageApi, GroupApi, FriendApi, AppApi.
/// Used in ChatTestViewModel to construct Group/QQ/Message objects without relying on null!.
/// </summary>
public sealed class MockPluginApi : IPluginApi
{
    private readonly MockLogger _logger = MockLogger.Instance;
    private readonly MockMessageApi _messageApi = new();
    private readonly MockGroupApi _groupApi = MockGroupApi.Instance;
    private readonly MockFriendApi _friendApi = MockFriendApi.Instance;
    private readonly MockAppApi _appApi = MockAppApi.Instance;

    public static MockPluginApi Instance { get; private set; } = new();

    /// <summary>Configure the mock with app directory and optional bot QQ.</summary>
    public static void Initialize(string appDir, long mockBotQQ = 10000)
    {
        MockAppApi.Configure(appDir, mockBotQQ);
        Instance = new MockPluginApi();
    }

    // ── IPluginApi properties ────────────────────────────────

    public ILogger Logger => _logger;

    public IMessageApi MessageApi => _messageApi;

    public IGroupApi GroupApi => _groupApi;

    public IFriendApi FriendApi => _friendApi;

    public IAppApi AppApi => _appApi;
}