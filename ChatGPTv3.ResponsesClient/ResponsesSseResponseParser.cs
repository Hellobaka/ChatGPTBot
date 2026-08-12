using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.ResponsesClient;

/// <summary>
/// Parses OpenAI Responses SSE streams.
///
/// Key behaviors per the Responses API spec:
/// - Each event has an "event:" name and a "data:" JSON payload; the JSON payload
///   also carries a "type" discriminator (fallback to the event name is supported).
/// - Assistant text arrives via response.output_text.delta events.
/// - Function-call items are finalized via response.output_item.done with
///   item.type == "function_call" (call_id, name, arguments as JSON string).
/// - The full response (including usage) arrives in response.completed.
/// - The stream ends with "data: [DONE]".
/// </summary>
public static class ResponsesSseResponseParser
{
    /// <summary>Parses an OpenAI Responses SSE response stream into a sequence of updates.</summary>
    public static async IAsyncEnumerable<ResponsesStreamingUpdate> ParseStreamAsync(
        Stream responseStream,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var reader = new StreamReader(responseStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
        var dataLines = new List<string>();
        var eventName = string.Empty;

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null)
            {
                // End of stream — flush a final event that has no trailing blank line.
                if (dataLines.Count > 0)
                {
                    var json = string.Concat(dataLines);
                    if (json == "[DONE]")
                    {
                        yield break;
                    }

                    var update = ParseEvent(json, eventName);
                    if (update != null)
                    {
                        yield return update;
                    }
                }
                break;
            }

            // SSE event boundary: blank line means end of current event
            if (string.IsNullOrEmpty(line))
            {
                if (dataLines.Count == 0)
                {
                    eventName = string.Empty;
                    continue;
                }

                var json = string.Concat(dataLines);
                dataLines.Clear();

                if (json == "[DONE]")
                {
                    yield break;
                }

                var update = ParseEvent(json, eventName);
                eventName = string.Empty;
                if (update != null)
                {
                    yield return update;
                }
                continue;
            }

            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
            {
                eventName = line.Substring(6).Trim();
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                dataLines.Add(line.Length > 5 ? line.Substring(5).TrimStart() : string.Empty);
            }
        }
    }

    private static ResponsesStreamingUpdate? ParseEvent(string json, string eventName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var typeEl)
                ? typeEl.GetString()
                : (string.IsNullOrEmpty(eventName) ? null : eventName);

            var update = new ResponsesStreamingUpdate { EventType = type ?? "unknown" };

            switch (type)
            {
                case "response.created":
                case "response.in_progress":
                case "response.completed":
                case "response.failed":
                case "response.incomplete":
                    if (root.TryGetProperty("response", out var responseEl))
                    {
                        update.Response = JsonSerializer.Deserialize<ResponsesCreateResponse>(
                            responseEl.GetRawText());
                    }
                    break;

                case "response.output_item.added":
                case "response.output_item.done":
                    if (root.TryGetProperty("output_index", out var outputIndexEl) &&
                        outputIndexEl.TryGetInt32(out var outputIndex))
                    {
                        update.OutputIndex = outputIndex;
                    }

                    if (root.TryGetProperty("item", out var itemEl))
                    {
                        var item = JsonSerializer.Deserialize<ResponseOutputItem>(itemEl.GetRawText());
                        update.ItemId = item?.Id;
                        update.ItemType = item?.Type;

                        if (type == "response.output_item.done" &&
                            item?.Type == "function_call" &&
                            item.CallId != null)
                        {
                            update.ToolCalls =
                            [
                                new ToolCallRequest
                                {
                                    Id = item.CallId,
                                    Type = "function",
                                    Function = new FunctionCall
                                    {
                                        Name = item.Name ?? string.Empty,
                                        Arguments = item.Arguments ?? "{}"
                                    }
                                }
                            ];
                        }
                    }
                    break;

                case "response.content_part.added":
                case "response.content_part.done":
                    if (root.TryGetProperty("output_index", out var partOutputIndexEl) &&
                        partOutputIndexEl.TryGetInt32(out var partOutputIndex))
                    {
                        update.OutputIndex = partOutputIndex;
                    }

                    if (root.TryGetProperty("content_index", out var contentIndexEl) &&
                        contentIndexEl.TryGetInt32(out var contentIndex))
                    {
                        update.ContentIndex = contentIndex;
                    }

                    update.ItemId = root.TryGetProperty("item_id", out var partItemId)
                        ? partItemId.GetString()
                        : null;

                    if (root.TryGetProperty("part", out var part) &&
                        part.TryGetProperty("type", out var partType))
                    {
                        update.ItemType = partType.GetString();
                    }
                    break;

                case "response.output_text.delta":
                    update.TextDelta = root.TryGetProperty("delta", out var textDelta)
                        ? textDelta.GetString()
                        : null;
                    update.ItemId = root.TryGetProperty("item_id", out var textItemId)
                        ? textItemId.GetString()
                        : null;
                    update.OutputIndex = root.TryGetProperty("output_index", out var textOutputIndexEl) &&
                                         textOutputIndexEl.TryGetInt32(out var textOutputIndex)
                        ? textOutputIndex
                        : null;
                    update.ContentIndex = root.TryGetProperty("content_index", out var textContentIndexEl) &&
                                          textContentIndexEl.TryGetInt32(out var textContentIndex)
                        ? textContentIndex
                        : null;
                    break;

                case "response.output_text.done":
                    update.TextDone = root.TryGetProperty("text", out var textDone)
                        ? textDone.GetString()
                        : null;
                    update.ItemId = root.TryGetProperty("item_id", out var doneItemId)
                        ? doneItemId.GetString()
                        : null;
                    break;

                case "response.function_call_arguments.delta":
                    update.ArgumentsDelta = root.TryGetProperty("delta", out var argsDelta)
                        ? argsDelta.GetString()
                        : null;
                    update.ItemId = root.TryGetProperty("item_id", out var argsItemId)
                        ? argsItemId.GetString()
                        : null;
                    update.OutputIndex = root.TryGetProperty("output_index", out var argsOutputIndexEl) &&
                                         argsOutputIndexEl.TryGetInt32(out var argsOutputIndex)
                        ? argsOutputIndex
                        : null;
                    break;

                case "response.function_call_arguments.done":
                    update.ArgumentsDone = root.TryGetProperty("arguments", out var argsDone)
                        ? argsDone.GetString()
                        : null;
                    update.FunctionName = root.TryGetProperty("name", out var functionName)
                        ? functionName.GetString()
                        : null;
                    update.ItemId = root.TryGetProperty("item_id", out var doneArgsItemId)
                        ? doneArgsItemId.GetString()
                        : null;
                    update.OutputIndex = root.TryGetProperty("output_index", out var doneArgsOutputIndexEl) &&
                                         doneArgsOutputIndexEl.TryGetInt32(out var doneArgsOutputIndex)
                        ? doneArgsOutputIndex
                        : null;
                    break;

                case "response.reasoning_text.delta":
                    update.ReasoningDelta = root.TryGetProperty("delta", out var reasoningDelta)
                        ? reasoningDelta.GetString()
                        : null;
                    break;

                case "error":
                    var code = root.TryGetProperty("code", out var errorCode)
                        ? errorCode.GetString()
                        : null;
                    var message = root.TryGetProperty("message", out var errorMessage)
                        ? errorMessage.GetString()
                        : "Responses API stream error";
                    throw new ResponsesApiException(0, message ?? "Responses API stream error", errorCode: code);
            }

            return update;
        }
        catch (JsonException)
        {
            return null; // Skip malformed chunks
        }
    }
}
