using System.Text;

namespace ChatGPTv3.HttpSse;

/// <summary>
/// Validation tests for the SSE parser using real-world OpenAI streaming data.
/// Run with: dotnet test or by calling RunAll() from a console app.
/// </summary>
public static class SseParserTests
{
    public static async Task<bool> RunAll()
    {
        var results = new[]
        {
            await TestBasicTextStreaming(),
            await TestToolCallStreaming(),
            await TestUsageChunk(),
            await TestDeepSeekReasoning(),
            await TestEmptyDeltaFinish(),
            await TestMalformedLines(),
        };

        var passed = results.Count(r => r);
        Console.WriteLine($"\n=== SSE Parser Tests: {passed}/{results.Length} passed ===");
        return passed == results.Length;
    }

    /// <summary>
    /// Test 1: Basic text streaming with standard finish_reason="stop".
    /// Simulates a typical "Hello, how are you?" → "I'm fine, thanks!" stream.
    /// </summary>
    public static async Task<bool> TestBasicTextStreaming()
    {
        var sseData = """
            data: {"id":"chatcmpl-123","object":"chat.completion.chunk","created":1717500000,"model":"gpt-4o","choices":[{"index":0,"delta":{"role":"assistant","content":""},"finish_reason":null}]}

            data: {"id":"chatcmpl-123","object":"chat.completion.chunk","created":1717500000,"model":"gpt-4o","choices":[{"index":0,"delta":{"content":"I'm"},"finish_reason":null}]}

            data: {"id":"chatcmpl-123","object":"chat.completion.chunk","created":1717500000,"model":"gpt-4o","choices":[{"index":0,"delta":{"content":" fine"},"finish_reason":null}]}

            data: {"id":"chatcmpl-123","object":"chat.completion.chunk","created":1717500000,"model":"gpt-4o","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

            data: [DONE]
            """;

        var updates = await ParseSseString(sseData);
        var text = string.Concat(updates.Select(u => u.GetDeltaContent() ?? ""));
        var finishReason = updates.LastOrDefault()?.GetFinishReason();

        Console.WriteLine($"  Basic text: '{text}' finish={finishReason}");
        return text == "I'm fine" && finishReason == "stop";
    }

    /// <summary>
    /// Test 2: Tool call streaming with incremental arguments across multiple chunks.
    /// Simulates a function calling weather API scenario.
    /// </summary>
    public static async Task<bool> TestToolCallStreaming()
    {
        var sseData = """
            data: {"id":"chatcmpl-456","object":"chat.completion.chunk","created":1717500000,"model":"gpt-4o","choices":[{"index":0,"delta":{"role":"assistant","content":null},"finish_reason":null}]}

            data: {"id":"chatcmpl-456","object":"chat.completion.chunk","created":1717500000,"model":"gpt-4o","choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"id":"call_abc","type":"function","function":{"name":"get_weather","arguments":"{\"location\":\""}}]},"finish_reason":null}]}

            data: {"id":"chatcmpl-456","object":"chat.completion.chunk","created":1717500000,"model":"gpt-4o","choices":[{"index":0,"delta":{"tool_calls":[{"index":0,"function":{"arguments":"Boston\"}"}}]},"finish_reason":null}]}

            data: {"id":"chatcmpl-456","object":"chat.completion.chunk","created":1717500000,"model":"gpt-4o","choices":[{"index":0,"delta":{},"finish_reason":"tool_calls"}]}

            data: [DONE]
            """;

        var updates = await ParseSseString(sseData);
        var lastToolUpdate = updates.LastOrDefault(u => u.IsToolCallFinish());
        var toolCalls = lastToolUpdate?.GetToolCalls();

        var success = toolCalls != null
            && toolCalls.Count == 1
            && toolCalls[0].Function.Name == "get_weather"
            && toolCalls[0].Function.Arguments.Contains("Boston");

        Console.WriteLine($"  Tool calls: name={toolCalls?[0].Function.Name} args={toolCalls?[0].Function.Arguments}");
        return success;
    }

    /// <summary>
    /// Test 3: stream_options.include_usage chunk with empty choices.
    /// </summary>
    public static async Task<bool> TestUsageChunk()
    {
        var sseData = """
            data: {"id":"chatcmpl-789","object":"chat.completion.chunk","created":1717500000,"model":"gpt-4o","choices":[{"index":0,"delta":{"content":"Hi"},"finish_reason":null}]}

            data: {"id":"chatcmpl-789","object":"chat.completion.chunk","created":1717500000,"model":"gpt-4o","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

            data: {"id":"chatcmpl-789","object":"chat.completion.chunk","created":1717500000,"model":"gpt-4o","system_fingerprint":"fp_abc","choices":[],"usage":{"prompt_tokens":15,"completion_tokens":1,"total_tokens":16}}

            data: [DONE]
            """;

        var updates = await ParseSseString(sseData);
        var usageUpdate = updates.FirstOrDefault(u => u.IsUsageChunk());
        var systemFingerprint = usageUpdate?.SystemFingerprint;

        Console.WriteLine($"  Usage chunk: prompt={usageUpdate?.Usage?.PromptTokens} completion={usageUpdate?.Usage?.CompletionTokens} fp={systemFingerprint}");
        return usageUpdate?.Usage?.TotalTokens == 16 && systemFingerprint == "fp_abc";
    }

    /// <summary>
    /// Test 4: DeepSeek R1 style reasoning_content in streaming delta.
    /// </summary>
    public static async Task<bool> TestDeepSeekReasoning()
    {
        var sseData = """
            data: {"id":"ds-001","object":"chat.completion.chunk","created":1717500000,"model":"deepseek-chat","choices":[{"index":0,"delta":{"role":"assistant","reasoning_content":"Let me think about this...","content":""},"finish_reason":null}]}

            data: {"id":"ds-001","object":"chat.completion.chunk","created":1717500000,"model":"deepseek-chat","choices":[{"index":0,"delta":{"content":"The answer is 42."},"finish_reason":null}]}

            data: {"id":"ds-001","object":"chat.completion.chunk","created":1717500000,"model":"deepseek-chat","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

            data: [DONE]
            """;

        var updates = await ParseSseString(sseData);
        var reasoning = string.Concat(updates.Select(u => u.GetReasoningContent() ?? ""));
        var content = string.Concat(updates.Select(u => u.GetDeltaContent() ?? ""));

        Console.WriteLine($"  Reasoning: '{reasoning}' Content: '{content}'");
        return reasoning == "Let me think about this..." && content == "The answer is 42.";
    }

    /// <summary>
    /// Test 5: Empty delta with finish_reason (common in last chunk).
    /// </summary>
    public static async Task<bool> TestEmptyDeltaFinish()
    {
        var sseData = """
            data: {"id":"e-001","object":"chat.completion.chunk","created":1717500000,"model":"gpt-4o","choices":[{"index":0,"delta":{},"finish_reason":"length"}]}

            data: [DONE]
            """;

        var updates = await ParseSseString(sseData);
        var finish = updates[0].GetFinishReason();
        var content = updates[0].GetDeltaContent();

        Console.WriteLine($"  Empty delta: finish={finish} content='{content ?? "null"}'");
        return finish == "length" && content == null;
    }

    /// <summary>
    /// Test 6: Skip non-data lines and malformed data gracefully.
    /// </summary>
    public static async Task<bool> TestMalformedLines()
    {
        var sseData = """
            event: ping

            : this is a comment

            data: {"id":"m-001","object":"chat.completion.chunk","created":1717500000,"model":"gpt-4o","choices":[{"index":0,"delta":{"content":"ok"},"finish_reason":"stop"}]}

            data: {invalid json}

            data: [DONE]
            """;

        var updates = await ParseSseString(sseData);
        var content = string.Concat(updates.Select(u => u.GetDeltaContent() ?? ""));

        Console.WriteLine($"  Malformed handling: got {updates.Count} updates, content='{content}'");
        return updates.Count == 1 && content == "ok";
    }

    // ── Helper ───────────────────────────────────────────

    private static async Task<List<StreamingUpdate>> ParseSseString(string sseData)
    {
        var bytes = Encoding.UTF8.GetBytes(sseData);
        using var stream = new MemoryStream(bytes);
        var updates = new List<StreamingUpdate>();
        await foreach (var update in SseResponseParser.ParseStreamAsync(stream))
        {
            updates.Add(update);
        }
        return updates;
    }
}
