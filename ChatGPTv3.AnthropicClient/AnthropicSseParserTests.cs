using System.Text;

namespace ChatGPTv3.AnthropicClient;

/// <summary>
/// Validation tests for the Anthropic SSE parser using realistic Anthropic streaming data.
/// Run with: dotnet test or by calling RunAll() from a console app.
/// </summary>
public static class AnthropicSseParserTests
{
    public static async Task<bool> RunAll()
    {
        var results = new[]
        {
            await TestBasicTextStreaming(),
            await TestToolUseStreaming(),
            await TestUsageMerging(),
            await TestThinkingDelta(),
            await TestMalformedLines(),
            await TestErrorEvent(),
        };

        var passed = results.Count(r => r);
        Console.WriteLine($"\n=== Anthropic SSE Parser Tests: {passed}/{results.Length} passed ===");
        return passed == results.Length;
    }

    /// <summary>
    /// Test 1: Basic text streaming with message_start / content blocks / message_stop.
    /// </summary>
    public static async Task<bool> TestBasicTextStreaming()
    {
        var sseData = """
            event: message_start
            data: {"type":"message_start","message":{"id":"msg_01","type":"message","role":"assistant","model":"claude-sonnet-4-5","content":[],"stop_reason":null,"usage":{"input_tokens":12,"output_tokens":1}}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"Hello"}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":" world"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"output_tokens":4}}

            event: message_stop
            data: {"type":"message_stop"}
            """;

        var updates = await ParseSseString(sseData);
        var text = string.Concat(updates.Select(u => u.TextDelta ?? ""));
        var stopReason = updates.LastOrDefault(u => u.StopReason != null)?.StopReason;

        Console.WriteLine($"  Basic text: '{text}' stop={stopReason}");
        return text == "Hello world" && stopReason == "end_turn";
    }

    /// <summary>
    /// Test 2: Tool use with incremental input_json_delta fragments.
    /// </summary>
    public static async Task<bool> TestToolUseStreaming()
    {
        var sseData = """
            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"tool_use","id":"toolu_01","name":"get_weather","input":{}}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"{\"location\":"}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"input_json_delta","partial_json":"\"Boston\"}"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"tool_use","stop_sequence":null},"usage":{"output_tokens":20}}

            event: message_stop
            data: {"type":"message_stop"}
            """;

        var updates = await ParseSseString(sseData);
        var finished = updates.FirstOrDefault(u => u.IsToolUseFinish);

        var success = finished != null
            && finished.ToolUseId == "toolu_01"
            && finished.ToolUseName == "get_weather"
            && finished.ToolUseInputJson == "{\"location\":\"Boston\"}";

        Console.WriteLine($"  Tool use: id={finished?.ToolUseId} name={finished?.ToolUseName} args={finished?.ToolUseInputJson}");
        return success;
    }

    /// <summary>
    /// Test 3: Usage merging — input from message_start, output from message_delta.
    /// </summary>
    public static async Task<bool> TestUsageMerging()
    {
        var sseData = """
            event: message_start
            data: {"type":"message_start","message":{"id":"msg_02","type":"message","role":"assistant","model":"claude-sonnet-4-5","content":[],"stop_reason":null,"usage":{"input_tokens":50,"output_tokens":1,"cache_creation_input_tokens":0,"cache_read_input_tokens":30}}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"ok"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn","stop_sequence":null},"usage":{"output_tokens":7,"cache_read_input_tokens":30}}

            event: message_stop
            data: {"type":"message_stop"}
            """;

        var updates = await ParseSseString(sseData);
        var finalUsage = updates.LastOrDefault(u => u.Usage != null)?.Usage;

        Console.WriteLine($"  Usage: input={finalUsage?.InputTokens} output={finalUsage?.OutputTokens} cached={finalUsage?.CacheReadInputTokens}");
        return finalUsage?.InputTokens == 50
            && finalUsage.OutputTokens == 7
            && finalUsage.CacheReadInputTokens == 30;
    }

    /// <summary>
    /// Test 4: Extended thinking deltas.
    /// </summary>
    public static async Task<bool> TestThinkingDelta()
    {
        var sseData = """
            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":""}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"Let me "}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"think"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}
            """;

        var updates = await ParseSseString(sseData);
        var thinking = string.Concat(updates.Select(u => u.ThinkingDelta ?? ""));

        Console.WriteLine($"  Thinking: '{thinking}'");
        return thinking == "Let me think";
    }

    /// <summary>
    /// Test 5: Skip non-data lines and malformed data gracefully.
    /// </summary>
    public static async Task<bool> TestMalformedLines()
    {
        var sseData = """
            : this is a comment

            event: message_start
            data: {"type":"message_start","message":{"id":"m1","type":"message","role":"assistant","model":"claude-sonnet-4-5","content":[],"stop_reason":null,"usage":{"input_tokens":5,"output_tokens":1}}}

            data: {invalid json}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"ok"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}
            """;

        var updates = await ParseSseString(sseData);
        var text = string.Concat(updates.Select(u => u.TextDelta ?? ""));

        Console.WriteLine($"  Malformed handling: got {updates.Count} updates, text='{text}'");
        return updates.Count == 4 && text == "ok";
    }

    /// <summary>
    /// Test 6: In-stream error event throws AnthropicApiException.
    /// </summary>
    public static async Task<bool> TestErrorEvent()
    {
        var sseData = """
            event: error
            data: {"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}
            """;

        try
        {
            await ParseSseString(sseData);
            Console.WriteLine("  Error event: no exception thrown (FAIL)");
            return false;
        }
        catch (AnthropicApiException ex)
        {
            Console.WriteLine($"  Error event: caught '{ex.Message}' type={ex.ErrorType}");
            return ex.ErrorType == "overloaded_error";
        }
    }

    // ── Helper ───────────────────────────────────────────

    private static async Task<List<AnthropicStreamingUpdate>> ParseSseString(string sseData)
    {
        var bytes = Encoding.UTF8.GetBytes(sseData);
        using var stream = new MemoryStream(bytes);
        var updates = new List<AnthropicStreamingUpdate>();
        await foreach (var update in AnthropicSseResponseParser.ParseStreamAsync(stream))
        {
            updates.Add(update);
        }
        return updates;
    }
}
