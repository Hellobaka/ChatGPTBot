using Another_Mirai_Native.Abstractions.Models;
using Another_Mirai_Native.Abstractions.Services;
using ChatGPTv3.Core;

namespace ChatGPTv3.UI.Mock;

/// <summary>
/// Mock IFriendApi — proxies GetFriendInfos to Entry.FriendApi if available;
/// otherwise returns empty list. Praise / request handling return true (no-op).
/// </summary>
public sealed class MockFriendApi : IFriendApi
{
    public static MockFriendApi Instance { get; } = new();

    private static readonly List<FriendInfo> MockFriends =
    [
        new(10001, "小明", "同学", DateTimeOffset.Now.ToUnixTimeSeconds()),
        new(10002, "小红", "群友", DateTimeOffset.Now.ToUnixTimeSeconds()),
        new(10003, "测试用户", "测试联系人", DateTimeOffset.Now.ToUnixTimeSeconds()),
        new(114514, "田所浩二", "哲学前辈", DateTimeOffset.Now.ToUnixTimeSeconds())
    ];

    // ── List ───────────────────────────────────────────────

    public List<FriendInfo> GetFriendInfos()
        => Entry.ApiFriend?.GetFriendInfos() ?? [.. MockFriends];

    public async Task<List<FriendInfo>> GetFriendInfosAsync()
        => Entry.ApiFriend != null
            ? await Entry.ApiFriend.GetFriendInfosAsync()
            : [.. MockFriends];

    // ── Praise ─────────────────────────────────────────────

    public bool SendPraise(long userId, int count) => true;
    public Task<bool> SendPraiseAsync(long userId, int count) => Task.FromResult(true);

    // ── Request ────────────────────────────────────────────

    public bool SetFriendAddRequest(string flag, bool approve, string remark) => true;
    public Task<bool> SetFriendAddRequestAsync(string flag, bool approve, string remark) => Task.FromResult(true);
}