namespace ChatGPTv3.Core.Commands;

/// <summary>
/// ASP.NET-style middleware pipeline builder for chat message processing.
///
/// Usage (in Entry.OnEnableAsync):
///   var pipeline = new ChatPipelineBuilder()
///       .UseAccessControl()
///       .UseMessageFilter()
///       .UseConcurrencyGate()
///       .UseReplyDecision()
///       .UseMessageImageResolver()
///       .UseMessageReferenceResolver()
///       .UseMessageRecorder()
///       .UseBackgroundCounters()
///       .UseChatExecutor()
///       .Build();
///
/// Execution:
///   var ctx = new ChatContext { GroupCtx = e, ... };
///   await pipeline(ctx);
///   return ctx.Result;
/// </summary>
public class ChatPipelineBuilder
{
    private readonly List<Func<ChatContext, Func<Task>, Task>> _middlewares = [];

    /// <summary>Add a middleware to the pipeline.</summary>
    public ChatPipelineBuilder Use(Func<ChatContext, Func<Task>, Task> middleware)
    {
        _middlewares.Add(middleware);
        return this;
    }

    /// <summary>Build the composed pipeline delegate.</summary>
    public Func<ChatContext, Task> Build()
    {
        return ctx =>
        {
            // Build the innermost terminal for THIS execution
            Func<Task> next = () => Task.CompletedTask;

            for (int i = _middlewares.Count - 1; i >= 0; i--)
            {
                var mw = _middlewares[i];
                var prev = next;
                next = () => mw(ctx, prev);
            }

            return next();
        };
    }
}