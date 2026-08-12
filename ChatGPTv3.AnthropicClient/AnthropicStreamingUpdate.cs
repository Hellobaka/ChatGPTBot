namespace ChatGPTv3.AnthropicClient;

/// <summary>
/// A single parsed Anthropic Messages SSE event.
/// </summary>
public class AnthropicStreamingUpdate
{
    /// <summary>Anthropic event type: message_start, content_block_start, content_block_delta, content_block_stop, message_delta, message_stop, ping.</summary>
    public string EventType { get; set; } = string.Empty;

    public string? MessageId { get; set; }

    public string? Model { get; set; }

    public int? ContentBlockIndex { get; set; }

    public string? ContentBlockType { get; set; }

    /// <summary>Incremental text (delta.type == "text_delta").</summary>
    public string? TextDelta { get; set; }

    /// <summary>Incremental tool-use arguments (delta.type == "input_json_delta").</summary>
    public string? InputJsonDelta { get; set; }

    /// <summary>Incremental extended-thinking text (delta.type == "thinking_delta").</summary>
    public string? ThinkingDelta { get; set; }

    public string? ToolUseId { get; set; }

    public string? ToolUseName { get; set; }

    /// <summary>Accumulated JSON arguments when a tool_use block finishes.</summary>
    public string? ToolUseInputJson { get; set; }

    /// <summary>True on content_block_stop for a tool_use block.</summary>
    public bool IsToolUseFinish { get; set; }

    public string? StopReason { get; set; }

    /// <summary>Accumulated usage (input from message_start + output from message_delta).</summary>
    public AnthropicUsage? Usage { get; set; }
}
