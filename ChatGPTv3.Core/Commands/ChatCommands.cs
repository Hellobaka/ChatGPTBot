using Another_Mirai_Native.Abstractions;
using Another_Mirai_Native.Abstractions.Attributes;
using Another_Mirai_Native.Abstractions.Context;
using Another_Mirai_Native.Abstractions.Enums;
using Another_Mirai_Native.Abstractions.Handlers;

namespace ChatGPTv3.Core.Commands;

/// <summary>
/// Sole message handler — AMN2 only dispatches to ONE handler per interface type.
/// Explicit [Command] methods matched first; non-matching messages fall through
/// to OnNoMatchAsync which runs the full ChatPipeline.
/// </summary>
[EventPriority(PluginEventType.GroupMsg, 100)]
[EventPriority(PluginEventType.PrivateMsg, 100)]
public class ChatCommands : CommandHandlerBase
{
    // TODO: [Command] methods for admin commands

    protected override async Task<EventHandleResult> OnNoMatchAsync(
        GroupMessageContext e, CancellationToken ct)
        => await ChatPipeline.ProcessGroupAsync(e, ct);

    protected override async Task<EventHandleResult> OnNoMatchAsync(
        PrivateMessageContext e, CancellationToken ct)
        => await ChatPipeline.ProcessPrivateAsync(e, ct);
}
