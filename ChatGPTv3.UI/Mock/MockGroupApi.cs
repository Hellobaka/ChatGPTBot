using Another_Mirai_Native.Abstractions.Enums;
using Another_Mirai_Native.Abstractions.Models;
using Another_Mirai_Native.Abstractions.Services;
using ChatGPTv3.Core;

namespace ChatGPTv3.UI.Mock;

/// <summary>
/// Mock IGroupApi — proxies list methods to Entry.GroupApi if available (AMN2 runtime);
/// otherwise returns empty lists. Kick/Ban/SetAdmin return true (no-ops).
/// </summary>
public sealed class MockGroupApi : IGroupApi
{
    public static MockGroupApi Instance { get; } = new();

    private static readonly List<GroupInfo> MockGroups =
    [
        new(123456789, "ChatGPT测试群", 4, 2000, DateTimeOffset.Now.ToUnixTimeSeconds()),
        new(234567890, "摸鱼交流群", 3, 1000, DateTimeOffset.Now.ToUnixTimeSeconds()),
        new(345678901, "插件开发讨论组", 3, 500, DateTimeOffset.Now.ToUnixTimeSeconds())
    ];

    private static readonly Dictionary<long, List<GroupMemberInfo>> MockMembers = new()
    {
        [123456789] =
        [
            CreateMember(123456789, 10001, "小明", "群友小明", QQGroupMemberType.Creator),
            CreateMember(123456789, 10002, "小红", "群友小红", QQGroupMemberType.Manage),
            CreateMember(123456789, 10003, "测试用户", "测试用户", QQGroupMemberType.Member),
            CreateMember(123456789, 114514, "田所浩二", "田所", QQGroupMemberType.Member)
        ],
        [234567890] =
        [
            CreateMember(234567890, 20001, "Alice", "Alice", QQGroupMemberType.Creator),
            CreateMember(234567890, 20002, "Bob", "Bob", QQGroupMemberType.Member),
            CreateMember(234567890, 20003, "Carol", "Carol", QQGroupMemberType.Member)
        ],
        [345678901] =
        [
            CreateMember(345678901, 30001, "开发A", "后端A", QQGroupMemberType.Creator),
            CreateMember(345678901, 30002, "开发B", "前端B", QQGroupMemberType.Member),
            CreateMember(345678901, 30003, "开发C", "测试C", QQGroupMemberType.Member)
        ]
    };

    private static GroupMemberInfo CreateMember(
        long groupId,
        long qq,
        string nick,
        string card,
        QQGroupMemberType type)
    {
        return new GroupMemberInfo(
            groupId,
            qq,
            nick,
            card,
            QQSex.Unknown,
            18,
            "中国",
            DateTime.Now.AddMonths(-6),
            DateTime.Now,
            string.Empty,
            type,
            false,
            string.Empty,
            null,
            false,
            1);
    }

    // ── List methods (proxy to Entry.GroupApi) ─────────────

    public List<GroupInfo> GetGroupList()
        => Entry.ApiGroup?.GetGroupList() ?? [.. MockGroups];

    public async Task<List<GroupInfo>> GetGroupListAsync()
        => Entry.ApiGroup != null
            ? await Entry.ApiGroup.GetGroupListAsync()
            : [.. MockGroups];

    public List<GroupMemberInfo> GetGroupMembers(long groupId)
        => Entry.ApiGroup?.GetGroupMembers(groupId)
           ?? (MockMembers.TryGetValue(groupId, out var members) ? [.. members] : []);

    public async Task<List<GroupMemberInfo>> GetGroupMembersAsync(long groupId)
        => Entry.ApiGroup != null
            ? await Entry.ApiGroup.GetGroupMembersAsync(groupId)
            : (MockMembers.TryGetValue(groupId, out var members) ? [.. members] : []);

    public GroupMemberInfo? GetGroupMemberInfo(long groupId, long qq)
        => Entry.ApiGroup?.GetGroupMemberInfo(groupId, qq)
           ?? (MockMembers.TryGetValue(groupId, out var members)
               ? members.FirstOrDefault(m => m.QQ == qq)
               : null);

    public async Task<GroupMemberInfo?> GetGroupMemberInfoAsync(long groupId, long qq)
        => Entry.ApiGroup != null
            ? await Entry.ApiGroup.GetGroupMemberInfoAsync(groupId, qq)
            : GetGroupMemberInfo(groupId, qq);

    public GroupInfo? GetGroupInfo(long groupId)
        => Entry.ApiGroup?.GetGroupInfo(groupId)
           ?? MockGroups.FirstOrDefault(g => g.Group == groupId);

    public async Task<GroupInfo?> GetGroupInfoAsync(long groupId)
        => Entry.ApiGroup != null
            ? await Entry.ApiGroup.GetGroupInfoAsync(groupId)
            : GetGroupInfo(groupId);

    // ── Admin / Ban / Kick (no-ops in mock, return true) ───

    public bool SetAdmin(long groupId, long qq, bool isAdmin) => true;

    public Task<bool> SetAdminAsync(long groupId, long qq, bool isAdmin) => Task.FromResult(true);

    public bool BanMember(long groupId, long qq, long durationSeconds) => true;

    public Task<bool> BanMemberAsync(long groupId, long qq, long durationSeconds) => Task.FromResult(true);

    public bool BanGroup(long groupId, bool isBan) => true;

    public Task<bool> BanGroupAsync(long groupId, bool isBan) => Task.FromResult(true);

    public bool Kick(long groupId, long qq, bool rejectRequest) => true;

    public Task<bool> KickAsync(long groupId, long qq, bool rejectRequest) => Task.FromResult(true);

    public bool Leave(long groupId) => true;

    public Task<bool> LeaveAsync(long groupId) => Task.FromResult(true);

    // ── Card / Title ───────────────────────────────────────

    public bool SetMemberCard(long groupId, long qq, string card) => true;

    public Task<bool> SetMemberCardAsync(long groupId, long qq, string card) => Task.FromResult(true);

    public bool SetMemberTitle(long groupId, long qq, string title) => true;

    public Task<bool> SetMemberTitleAsync(long groupId, long qq, string title) => Task.FromResult(true);

    // ── Requests ───────────────────────────────────────────

    public bool SetGroupAddRequest(string flag, bool approve, string reason) => true;

    public Task<bool> SetGroupAddRequestAsync(string flag, bool approve, string reason) => Task.FromResult(true);

    public bool SetGroupInviteRequest(string flag, bool approve, string reason) => true;

    public Task<bool> SetGroupInviteRequestAsync(string flag, bool approve, string reason) => Task.FromResult(true);
}