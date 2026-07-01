using Another_Mirai_Native.Abstractions.Models;
using Another_Mirai_Native.Abstractions.Services;
using ChatGPTv3.Core;

namespace ChatGPTv3.UI.Mock;

/// <summary>
/// Mock IMessageApi — send methods return a fake messageId; image hash lookup always fails.
/// Other operations return false/empty.
/// </summary>
public sealed class MockMessageApi : IMessageApi
{
    /// <summary>Fired when a message is "sent" via SendGroupMessage/SendPrivateMessage.</summary>
    public event Action<long, string>? GroupMessageSent;

    public event Action<long, string>? PrivateMessageSent;

    private static int _messageIdCounter = -1000;

    private static int NextMessageId() => Interlocked.Decrement(ref _messageIdCounter);

    // ── Send ────────────────────────────────────────────────

    public int SendGroupMessage(long groupId, string message)
    {
        var id = NextMessageId();
        GroupMessageSent?.Invoke(groupId, message);
        return id;
    }

    public async Task<int> SendGroupMessageAsync(long groupId, string message)
    {
        var id = SendGroupMessage(groupId, message);
        return await Task.FromResult(id);
    }

    public int SendPrivateMessage(long userId, string message)
    {
        var id = NextMessageId();
        PrivateMessageSent?.Invoke(userId, message);
        return id;
    }

    public async Task<int> SendPrivateMessageAsync(long userId, string message)
    {
        var id = SendPrivateMessage(userId, message);
        return await Task.FromResult(id);
    }

    // ── Forward ─────────────────────────────────────────────

    public int SendGroupForwardMessage(long groupId, string[] messages) => 0;
    public Task<int> SendGroupForwardMessageAsync(long groupId, string[] messages) => Task.FromResult(0);

    public int SendPrivateForwardMessage(long userId, string[] messages) => 0;
    public Task<int> SendPrivateForwardMessageAsync(long userId, string[] messages) => Task.FromResult(0);

    // ── Delete ──────────────────────────────────────────────

    public bool DeleteMessage(long messageId) => true;
    public Task<bool> DeleteMessageAsync(long messageId) => Task.FromResult(true);

    // ── History ─────────────────────────────────────────────

    public List<ChatHistory> GetChatHistories(long groupId, long qq, int count) => [];
    public Task<List<ChatHistory>> GetChatHistoriesAsync(long groupId, long qq, int count) => Task.FromResult(new List<ChatHistory>());

    public ChatHistory? GetChatHistoryById(long id, bool isGroup, int count) => null;
    public Task<ChatHistory?> GetChatHistoryByIdAsync(long id, bool isGroup, int count) => Task.FromResult<ChatHistory?>(null);

    // ── Image ───────────────────────────────────────────────

    public (bool Success, string FilePath) TryGetImageByHash(string hash) => (false, "");
    public Task<(bool Success, string FilePath)> TryGetImageByHashAsync(string hash) => Task.FromResult((false, ""));
}
