using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace ChatGPTv3.AnthropicClient;

/// <summary>
/// Parses Anthropic Messages SSE streams.
///
/// Key behaviors per the Anthropic API spec:
/// - Each SSE event has an "event:" name and a "data:" JSON payload, and the JSON
///   payload itself carries a "type" discriminator.
/// - text deltas arrive as content_block_delta with delta.type == "text_delta".
/// - tool-use arguments arrive incrementally as input_json_delta fragments keyed
///   by content block index and are accumulated here.
/// - usage is split across message_start (input) and message_delta (output), so
///   both are merged into a single AnthropicUsage instance.
/// - the stream ends with message_stop (no [DONE] sentinel).
/// </summary>
public static class AnthropicSseResponseParser
{
    /// <summary>Parses an Anthropic SSE response stream into a sequence of updates.</summary>
    public static async IAsyncEnumerable<AnthropicStreamingUpdate> ParseStreamAsync(
        Stream responseStream,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var reader = new StreamReader(responseStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
        var dataLines = new List<string>();
        var eventName = string.Empty;
        var usage = new AnthropicUsage();
        var toolUses = new Dictionary<int, ToolUseMergeState>();

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null)
            {
                // End of stream — flush a final event that has no trailing blank line.
                if (dataLines.Count > 0)
                {
                    var json = string.Concat(dataLines);
                    var update = ParseEvent(json, eventName, usage, toolUses);
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

                var update = ParseEvent(json, eventName, usage, toolUses);
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

    private static AnthropicStreamingUpdate? ParseEvent(
        string json,
        string eventName,
        AnthropicUsage usage,
        Dictionary<int, ToolUseMergeState> toolUses)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.TryGetProperty("type", out var typeEl)
                ? typeEl.GetString()
                : (string.IsNullOrEmpty(eventName) ? null : eventName);

            var update = new AnthropicStreamingUpdate { EventType = type ?? "unknown" };

            switch (type)
            {
                case "message_start":
                    if (root.TryGetProperty("message", out var message))
                    {
                        update.MessageId = message.TryGetProperty("id", out var id) ? id.GetString() : null;
                        update.Model = message.TryGetProperty("model", out var model) ? model.GetString() : null;
                        if (message.TryGetProperty("usage", out var u))
                        {
                            MergeUsage(usage, u);
                        }
                    }
                    update.Usage = usage;
                    break;

                case "content_block_start":
                    if (root.TryGetProperty("index", out var indexEl) && indexEl.TryGetInt32(out var index))
                    {
                        update.ContentBlockIndex = index;
                        if (root.TryGetProperty("content_block", out var block))
                        {
                            update.ContentBlockType = block.TryGetProperty("type", out var blockType)
                                ? blockType.GetString()
                                : null;

                            if (update.ContentBlockType == "tool_use")
                            {
                                var state = new ToolUseMergeState
                                {
                                    Id = block.TryGetProperty("id", out var bid) ? bid.GetString() : null,
                                    Name = block.TryGetProperty("name", out var bname) ? bname.GetString() : null
                                };
                                toolUses[index] = state;
                                update.ToolUseId = state.Id;
                                update.ToolUseName = state.Name;
                            }
                        }
                    }
                    break;

                case "content_block_delta":
                    if (root.TryGetProperty("index", out var deltaIndexEl) && deltaIndexEl.TryGetInt32(out var deltaIndex))
                    {
                        update.ContentBlockIndex = deltaIndex;
                        if (root.TryGetProperty("delta", out var delta))
                        {
                            var deltaType = delta.TryGetProperty("type", out var dt) ? dt.GetString() : null;
                            switch (deltaType)
                            {
                                case "text_delta":
                                    update.TextDelta = delta.TryGetProperty("text", out var text) ? text.GetString() : null;
                                    break;

                                case "input_json_delta":
                                    update.InputJsonDelta = delta.TryGetProperty("partial_json", out var partialJson)
                                        ? partialJson.GetString()
                                        : null;
                                    if (update.InputJsonDelta != null &&
                                        toolUses.TryGetValue(deltaIndex, out var state))
                                    {
                                        state.Arguments.Append(update.InputJsonDelta);
                                    }
                                    break;

                                case "thinking_delta":
                                    update.ThinkingDelta = delta.TryGetProperty("thinking", out var thinking)
                                        ? thinking.GetString()
                                        : null;
                                    break;
                            }
                        }
                    }
                    break;

                case "content_block_stop":
                    if (root.TryGetProperty("index", out var stopIndexEl) && stopIndexEl.TryGetInt32(out var stopIndex))
                    {
                        update.ContentBlockIndex = stopIndex;
                        if (toolUses.Remove(stopIndex, out var finished))
                        {
                            update.IsToolUseFinish = true;
                            update.ToolUseId = finished.Id;
                            update.ToolUseName = finished.Name;
                            update.ToolUseInputJson = finished.Arguments.ToString();
                        }
                    }
                    break;

                case "message_delta":
                    if (root.TryGetProperty("delta", out var messageDelta))
                    {
                        update.StopReason = messageDelta.TryGetProperty("stop_reason", out var stopReason)
                            ? stopReason.GetString()
                            : null;
                    }
                    if (root.TryGetProperty("usage", out var deltaUsage))
                    {
                        MergeUsage(usage, deltaUsage);
                    }
                    update.Usage = usage;
                    break;

                case "message_stop":
                case "ping":
                    break;

                case "error":
                    var errorMessage = root.TryGetProperty("error", out var errorEl) &&
                                       errorEl.TryGetProperty("message", out var errorMsgEl)
                        ? errorMsgEl.GetString()
                        : "Anthropic API stream error";
                    var errorType = root.TryGetProperty("error", out var errorTypeEl) &&
                                    errorTypeEl.TryGetProperty("type", out var errorTypeValue)
                        ? errorTypeValue.GetString()
                        : null;
                    throw new AnthropicApiException(
                        0,
                        errorMessage ?? "Anthropic API stream error",
                        errorType);
            }

            return update;
        }
        catch (JsonException)
        {
            return null; // Skip malformed chunks
        }
    }

    private static void MergeUsage(AnthropicUsage usage, JsonElement element)
    {
        if (element.TryGetProperty("input_tokens", out var input) && input.TryGetInt32(out var inputTokens))
        {
            usage.InputTokens = inputTokens;
        }

        if (element.TryGetProperty("output_tokens", out var output) && output.TryGetInt32(out var outputTokens))
        {
            usage.OutputTokens = outputTokens;
        }

        if (element.TryGetProperty("cache_creation_input_tokens", out var cacheCreation) &&
            cacheCreation.TryGetInt32(out var cacheCreationTokens))
        {
            usage.CacheCreationInputTokens = cacheCreationTokens;
        }

        if (element.TryGetProperty("cache_read_input_tokens", out var cacheRead) &&
            cacheRead.TryGetInt32(out var cacheReadTokens))
        {
            usage.CacheReadInputTokens = cacheReadTokens;
        }
    }

    private class ToolUseMergeState
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public StringBuilder Arguments { get; } = new();
    }
}
