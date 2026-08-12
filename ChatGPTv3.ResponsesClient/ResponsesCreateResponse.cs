using System.Text.Json;
using System.Text.Json.Serialization;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.ResponsesClient;

/// <summary>
/// Full response object from the OpenAI Responses API (non-streaming, or the
/// "response" payload inside response.completed / response.failed stream events).
/// </summary>
public class ResponsesCreateResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public double CreatedAt { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public ResponsesApiError? Error { get; set; }

    [JsonPropertyName("incomplete_details")]
    public ResponsesIncompleteDetails? IncompleteDetails { get; set; }

    [JsonPropertyName("instructions")]
    public string? Instructions { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("output")]
    public List<ResponseOutputItem> Output { get; set; } = [];

    [JsonPropertyName("usage")]
    public ResponsesUsage? Usage { get; set; }

    /// <summary>Aggregates all output_text parts from message items, mirroring SDK's output_text.</summary>
    public string? GetText()
    {
        var texts = new List<string>();
        foreach (var item in Output.Where(i => i.Type == "message"))
        {
            foreach (var part in item.Content ?? [])
            {
                if (part.Type == "output_text" && part.Text != null)
                {
                    texts.Add(part.Text);
                }
            }
        }

        return string.Concat(texts);
    }

    /// <summary>Extracts function_call items as OpenAI-style ToolCallRequest items.</summary>
    public List<ToolCallRequest>? GetToolCalls()
    {
        var calls = Output
            .Where(i => i.Type == "function_call" && i.CallId != null)
            .Select(i => new ToolCallRequest
            {
                Id = i.CallId ?? string.Empty,
                Type = "function",
                Function = new FunctionCall
                {
                    Name = i.Name ?? string.Empty,
                    Arguments = i.Arguments ?? "{}"
                }
            })
            .ToList();

        return calls.Count > 0 ? calls : null;
    }

    /// <summary>
    /// Maps the response status to a chat-completion style finish reason:
    /// completed→stop, incomplete(max_output_tokens)→length, incomplete(content_filter)→content_filter,
    /// failed→failed, otherwise the raw status.
    /// </summary>
    public string? GetFinishReason()
    {
        if (Status == "completed")
        {
            return "stop";
        }

        if (Status == "incomplete")
        {
            return IncompleteDetails?.Reason switch
            {
                "max_output_tokens" => "length",
                "content_filter" => "content_filter",
                _ => "incomplete"
            };
        }

        if (Status == "failed")
        {
            return "failed";
        }

        return Status;
    }

    /// <summary>Converts Responses usage to the unified TokenUsageInfo used by UsageTracker.</summary>
    public TokenUsageInfo? GetTokenUsage()
    {
        return Usage?.ToTokenUsageInfo();
    }
}

/// <summary>Why a response was incomplete.</summary>
public class ResponsesIncompleteDetails
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

/// <summary>
/// One output item: message (assistant text), function_call, function_call_output, reasoning, etc.
/// </summary>
public class ResponseOutputItem
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>Present on message items ("assistant").</summary>
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public List<ResponseContentPart>? Content { get; set; }

    /// <summary>Present on function_call items.</summary>
    [JsonPropertyName("call_id")]
    public string? CallId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }

    /// <summary>Present on function_call_output items.</summary>
    [JsonPropertyName("output")]
    public object? Output { get; set; }
}

/// <summary>A content part inside a message output item (output_text, refusal, ...).</summary>
public class ResponseContentPart
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

/// <summary>Token usage reported by the Responses API.</summary>
public class ResponsesUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("input_tokens_details")]
    public ResponsesInputTokensDetails? InputTokensDetails { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("output_tokens_details")]
    public ResponsesOutputTokensDetails? OutputTokensDetails { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }

    public TokenUsageInfo ToTokenUsageInfo() => new()
    {
        PromptTokens = InputTokens,
        CompletionTokens = OutputTokens,
        TotalTokens = TotalTokens,
        PromptTokensDetails = new PromptTokensDetails
        {
            CachedTokens = InputTokensDetails?.CachedTokens ?? 0
        },
        CompletionTokensDetails = new CompletionTokensDetails
        {
            ReasoningTokens = OutputTokensDetails?.ReasoningTokens ?? 0
        }
    };
}

/// <summary>Input token breakdown.</summary>
public class ResponsesInputTokensDetails
{
    [JsonPropertyName("cached_tokens")]
    public int CachedTokens { get; set; }

    [JsonPropertyName("cache_write_tokens")]
    public int CacheWriteTokens { get; set; }
}

/// <summary>Output token breakdown.</summary>
public class ResponsesOutputTokensDetails
{
    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; set; }
}

/// <summary>Error object returned by the Responses API.</summary>
public class ResponsesApiError
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("param")]
    public string? Param { get; set; }
}
