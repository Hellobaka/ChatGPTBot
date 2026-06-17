using Another_Mirai_Native.Abstractions;
using Another_Mirai_Native.Abstractions.Attributes;
using Another_Mirai_Native.Abstractions.Context;
using Another_Mirai_Native.Abstractions.Enums;
using Another_Mirai_Native.Abstractions.Handlers;

namespace ChatGPTv3.Core.Commands;

/// <summary>
/// Sole message handler — AMN2 only dispatches to ONE handler per interface type.
/// Group messages → middleware pipeline   Private messages → private pipeline
/// </summary>
[EventPriority(PluginEventType.GroupMsg, 100)]
[EventPriority(PluginEventType.PrivateMsg, 100)]
public class ChatCommands : CommandHandlerBase
{
    public static Func<ChatContext, Task>? GroupPipeline { get; set; }
    public static Func<ChatContext, Task>? PrivatePipeline { get; set; }

    protected override async Task<EventHandleResult> OnNoMatchAsync(
        GroupMessageContext e, CancellationToken ct)
    {
        if (GroupPipeline == null) return EventHandleResult.Pass;
        var ctx = new ChatContext
        {
            GroupCtx = e,
            MessageText = e.Message.Text ?? string.Empty,
            CancellationToken = ct,
            SendFunc = async msg => await e.SendMessageAsync(msg)
        };
        await GroupPipeline(ctx);
        return ctx.Result;
    }

    protected override async Task<EventHandleResult> OnNoMatchAsync(
        PrivateMessageContext e, CancellationToken ct)
    {
        if (PrivatePipeline == null) return EventHandleResult.Pass;
        var ctx = new ChatContext
        {
            PrivateCtx = e,
            MessageText = e.Message.Text ?? string.Empty,
            CancellationToken = ct,
            SendFunc = async msg => await e.SendMessageAsync(msg)
        };
        await PrivatePipeline(ctx);
        return ctx.Result;
    }
}
