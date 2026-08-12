using System.Text;

namespace ChatGPTv3.ResponsesClient;

/// <summary>
/// Validation tests for the Responses SSE parser using realistic OpenAI streaming data.
/// Run with: dotnet test or by calling RunAll() from a console app.
/// </summary>
public static class ResponsesSseParserTests
{
    public static async Task<bool> RunAll()
    {
        var results = new[]
        {
            await TestBasicTextStreaming(),
            await TestFunctionCallStreaming(),
            await TestCompletedResponseUsage(),
            await TestReasoningDelta(),
            await TestMalformedLines(),
            await TestErrorEvent(),
        };

        var passed = results.Count(r => r);
        Console.WriteLine($"\n=== Responses SSE Parser Tests: {passed}/{results.Length} passed ===");
        return passed == results.Length;
    }

    /// <summary>
    /// Test 1: Basic text streaming with output_text deltas and a completed response.
    /// </summary>
    public static async Task<bool> TestBasicTextStreaming()
    {
        var sseData = """
            event: response.created
            data: {"type":"response.created","response":{"id":"resp_01","object":"response","created_at":1717500000,"status":"in_progress","model":"gpt-4o","output":[],"parallel_tool_calls":true,"tools":[],"tool_choice":"auto"}}

            event: response.output_item.added
            data: {"type":"response.output_item.added","output_index":0,"item":{"id":"msg_01","type":"message","status":"in_progress","role":"assistant","content":[]}}

            event: response.content_part.added
            data: {"type":"response.content_part.added","item_id":"msg_01","output_index":0,"content_index":0,"part":{"type":"output_text","text":"","annotations":[]}}

            event: response.output_text.delta
            data: {"type":"response.output_text.delta","item_id":"msg_01","output_index":0,"content_index":0,"delta":"Hello"}

            event: response.output_text.delta
            data: {"type":"response.output_text.delta","item_id":"msg_01","output_index":0,"content_index":0,"delta":" world"}

            event: response.output_text.done
            data: {"type":"response.output_text.done","item_id":"msg_01","output_index":0,"content_index":0,"text":"Hello world"}

            event: response.output_item.done
            data: {"type":"response.output_item.done","output_index":0,"item":{"id":"msg_01","type":"message","status":"completed","role":"assistant","content":[{"type":"output_text","text":"Hello world","annotations":[]}]}}

            event: response.completed
            data: {"type":"response.completed","response":{"id":"resp_01","object":"response","created_at":1717500000,"status":"completed","model":"gpt-4o","output":[{"id":"msg_01","type":"message","status":"completed","role":"assistant","content":[{"type":"output_text","text":"Hello world","annotations":[]}]}],"parallel_tool_calls":true,"tools":[],"tool_choice":"auto","usage":{"input_tokens":10,"input_tokens_details":{"cached_tokens":0,"cache_write_tokens":0},"output_tokens":3,"output_tokens_details":{"reasoning_tokens":0},"total_tokens":13}}}

            data: [DONE]
            """;

        var updates = await ParseSseString(sseData);
        var text = string.Concat(updates.Select(u => u.TextDelta ?? ""));
        var completed = updates.FirstOrDefault(u => u.EventType == "response.completed");

        Console.WriteLine($"  Basic text: '{text}' status={completed?.Response?.Status}");
        return text == "Hello world" && completed?.Response?.Status == "completed";
    }

    /// <summary>
    /// Test 2: Function call streaming — output_item.done exposes the completed ToolCallRequest.
    /// </summary>
    public static async Task<bool> TestFunctionCallStreaming()
    {
        var sseData = """
            event: response.created
            data: {"type":"response.created","response":{"id":"resp_02","object":"response","created_at":1717500000,"status":"in_progress","model":"gpt-4o","output":[],"parallel_tool_calls":true,"tools":[],"tool_choice":"auto"}}

            event: response.output_item.added
            data: {"type":"response.output_item.added","output_index":0,"item":{"id":"fc_01","type":"function_call","status":"in_progress","call_id":"call_123","name":"get_weather","arguments":""}}

            event: response.function_call_arguments.delta
            data: {"type":"response.function_call_arguments.delta","item_id":"fc_01","output_index":0,"delta":"{\"location\":"}

            event: response.function_call_arguments.delta
            data: {"type":"response.function_call_arguments.delta","item_id":"fc_01","output_index":0,"delta":"\"Boston\"}"}

            event: response.function_call_arguments.done
            data: {"type":"response.function_call_arguments.done","item_id":"fc_01","output_index":0,"name":"get_weather","arguments":"{\"location\":\"Boston\"}"}

            event: response.output_item.done
            data: {"type":"response.output_item.done","output_index":0,"item":{"id":"fc_01","type":"function_call","status":"completed","call_id":"call_123","name":"get_weather","arguments":"{\"location\":\"Boston\"}"}}

            event: response.completed
            data: {"type":"response.completed","response":{"id":"resp_02","object":"response","created_at":1717500000,"status":"completed","model":"gpt-4o","output":[{"id":"fc_01","type":"function_call","status":"completed","call_id":"call_123","name":"get_weather","arguments":"{\"location\":\"Boston\"}"}],"parallel_tool_calls":true,"tools":[],"tool_choice":"auto","usage":{"input_tokens":20,"input_tokens_details":{"cached_tokens":0,"cache_write_tokens":0},"output_tokens":9,"output_tokens_details":{"reasoning_tokens":0},"total_tokens":29}}}

            data: [DONE]
            """;

        var updates = await ParseSseString(sseData);
        var toolUpdate = updates.FirstOrDefault(u => u.ToolCalls is { Count: > 0 });
        var call = toolUpdate?.ToolCalls?[0];

        var success = call != null
            && call.Id == "call_123"
            && call.Function.Name == "get_weather"
            && call.Function.Arguments == "{\"location\":\"Boston\"}";

        Console.WriteLine($"  Function call: id={call?.Id} name={call?.Function.Name} args={call?.Function.Arguments}");
        return success;
    }

    /// <summary>
    /// Test 3: response.completed carries full usage (the Responses equivalent of stream usage).
    /// </summary>
    public static async Task<bool> TestCompletedResponseUsage()
    {
        var sseData = """
            event: response.completed
            data: {"type":"response.completed","response":{"id":"resp_03","object":"response","created_at":1717500000,"status":"completed","model":"gpt-4o","output":[],"parallel_tool_calls":true,"tools":[],"tool_choice":"auto","usage":{"input_tokens":50,"input_tokens_details":{"cached_tokens":20,"cache_write_tokens":0},"output_tokens":7,"output_tokens_details":{"reasoning_tokens":2},"total_tokens":57}}}

            data: [DONE]
            """;

        var updates = await ParseSseString(sseData);
        var usage = updates.FirstOrDefault(u => u.Response?.Usage != null)?.Response?.Usage;
        var info = usage?.ToTokenUsageInfo();

        Console.WriteLine($"  Usage: prompt={info?.PromptTokens} completion={info?.CompletionTokens} cached={info?.GetCachedPromptTokens()} reasoning={info?.GetReasoningTokens()}");
        return info?.TotalTokens == 57
            && info.GetCachedPromptTokens() == 20
            && info.GetReasoningTokens() == 2;
    }

    /// <summary>
    /// Test 4: Reasoning deltas for o-series models.
    /// </summary>
    public static async Task<bool> TestReasoningDelta()
    {
        var sseData = """
            event: response.reasoning_text.delta
            data: {"type":"response.reasoning_text.delta","item_id":"rs_01","output_index":0,"delta":"Hmm, "}

            event: response.reasoning_text.delta
            data: {"type":"response.reasoning_text.delta","item_id":"rs_01","output_index":0,"delta":"let me think"}
            """;

        var updates = await ParseSseString(sseData);
        var reasoning = string.Concat(updates.Select(u => u.ReasoningDelta ?? ""));

        Console.WriteLine($"  Reasoning: '{reasoning}'");
        return reasoning == "Hmm, let me think";
    }

    /// <summary>
    /// Test 5: Skip non-data lines and malformed data gracefully.
    /// </summary>
    public static async Task<bool> TestMalformedLines()
    {
        var sseData = """
            : this is a comment

            event: response.output_text.delta
            data: {"type":"response.output_text.delta","item_id":"m1","output_index":0,"content_index":0,"delta":"ok"}

            data: {invalid json}

            data: [DONE]
            """;

        var updates = await ParseSseString(sseData);
        var text = string.Concat(updates.Select(u => u.TextDelta ?? ""));

        Console.WriteLine($"  Malformed handling: got {updates.Count} updates, text='{text}'");
        return updates.Count == 1 && text == "ok";
    }

    /// <summary>
    /// Test 6: In-stream error event throws ResponsesApiException.
    /// </summary>
    public static async Task<bool> TestErrorEvent()
    {
        var sseData = """
            event: error
            data: {"type":"error","code":"rate_limit_exceeded","message":"Rate limit exceeded","param":null}
            """;

        try
        {
            await ParseSseString(sseData);
            Console.WriteLine("  Error event: no exception thrown (FAIL)");
            return false;
        }
        catch (ResponsesApiException ex)
        {
            Console.WriteLine($"  Error event: caught '{ex.Message}' code={ex.ErrorCode}");
            return ex.ErrorCode == "rate_limit_exceeded";
        }
    }

    // ── Helper ───────────────────────────────────────────

    private static async Task<List<ResponsesStreamingUpdate>> ParseSseString(string sseData)
    {
        var bytes = Encoding.UTF8.GetBytes(sseData);
        using var stream = new MemoryStream(bytes);
        var updates = new List<ResponsesStreamingUpdate>();
        await foreach (var update in ResponsesSseResponseParser.ParseStreamAsync(stream))
        {
            updates.Add(update);
        }
        return updates;
    }
}
