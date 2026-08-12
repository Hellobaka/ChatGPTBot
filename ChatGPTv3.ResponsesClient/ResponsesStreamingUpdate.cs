using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.ResponsesClient;

/// <summary>
/// A single parsed OpenAI Responses SSE event.
/// </summary>
public class ResponsesStreamingUpdate
{
    /// <summary>Event type, e.g. response.created, response.output_text.delta, response.completed.</summary>
    public string EventType { get; set; } = string.Empty;

    public string? ItemId { get; set; }

    public string? ItemType { get; set; }

    public int? OutputIndex { get; set; }

    public int? ContentIndex { get; set; }

    /// <summary>Incremental assistant text (response.output_text.delta).</summary>
    public string? TextDelta { get; set; }

    /// <summary>Finalized text for one content part (response.output_text.done).</summary>
    public string? TextDone { get; set; }

    /// <summary>Incremental function-call arguments (response.function_call_arguments.delta).</summary>
    public string? ArgumentsDelta { get; set; }

    /// <summary>Finalized function-call arguments (response.function_call_arguments.done).</summary>
    public string? ArgumentsDone { get; set; }

    public string? FunctionName { get; set; }

    /// <summary>Incremental reasoning text (response.reasoning_text.delta).</summary>
    public string? ReasoningDelta { get; set; }

    /// <summary>Full response payload for response.completed / response.failed / response.created.</summary>
    public ResponsesCreateResponse? Response { get; set; }

    /// <summary>Completed function calls (from response.output_item.done).</summary>
    public List<ToolCallRequest>? ToolCalls { get; set; }

    public ResponsesApiError? Error { get; set; }
}
