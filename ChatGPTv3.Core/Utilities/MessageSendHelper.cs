using ChatGPTv3.Core.Config;

namespace ChatGPTv3.Core.Utilities;

/// <summary>
/// Central send gate for outgoing messages. When <see cref="AppConfig.MockSendMessage"/>
/// is enabled, messages are logged instead of delivered and every send reports success.
/// All real send paths (chat pipeline + scheduled tasks) must go through this helper
/// so the mock switch covers the whole bot.
/// </summary>
public static class MessageSendHelper
{
    private static int _mockMessageId = -1000;

    public static Task SendGroupAsync(long groupId, string message, Func<string, Task> realSend)
    {
        if (AppConfig.MockSendMessage)
        {
            LogMock("模拟群聊发送", groupId, message);
            return Task.CompletedTask;
        }

        return realSend(message);
    }

    public static Task SendPrivateAsync(long userId, string message, Func<string, Task> realSend)
    {
        if (AppConfig.MockSendMessage)
        {
            LogMock("模拟私聊发送", userId, message);
            return Task.CompletedTask;
        }

        return realSend(message);
    }

    private static void LogMock(string targetKind, long targetId, string message)
    {
        var id = Interlocked.Decrement(ref _mockMessageId);
        Entry.LoggerApi?.Info(targetKind, $"{(targetKind.Contains("群聊") ? "群聊" : "私聊")}：{targetId} 消息：{message}");
    }
}
