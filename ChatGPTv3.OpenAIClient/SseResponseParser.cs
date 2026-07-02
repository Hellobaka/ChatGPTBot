using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace ChatGPTv3.OpenAIClient;

/// <summary>
/// Parses Server-Sent Events (SSE) streams from OpenAI-compatible APIs.
///
/// Key behaviors per the OpenAI API spec:
/// - Each SSE event is terminated by a blank line (\n\n).
/// - Data lines start with "data: " prefix.
/// - The stream ends with "data: [DONE]".
/// - Tool call arguments arrive incrementally across multiple SSE events,
///   keyed by an "index" field that identifies which parallel tool call they belong to.
/// - When stream_options.include_usage=true, a final chunk with empty choices
///   and full usage stats arrives before [DONE].
/// </summary>
public static class SseResponseParser
{
    /// <summary>
    /// Parses an SSE response stream into a sequence of StreamingUpdate objects.
    /// </summary>
    public static async IAsyncEnumerable<StreamingUpdate> ParseStreamAsync(
        Stream responseStream,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var reader = new StreamReader(responseStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
        var accum = new ToolCallAccumulator();
        var dataLines = new List<string>();

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);

            if (line == null)
            {
                break; // End of stream
            }

            // SSE event boundary: blank line means end of current event
            if (string.IsNullOrEmpty(line))
            {
                if (dataLines.Count == 0)
                {
                    continue;
                }

                var json = string.Concat(dataLines);
                dataLines.Clear();

                if (json == "[DONE]")
                {
                    yield break;
                }

                var update = ParseChunk(json, accum);
                if (update != null)
                {
                    // After processing a usage chunk, reset accumulator state
                    if (update.IsUsageChunk())
                    {
                        accum.Reset();
                    }
                    yield return update;
                }
                continue;
            }

            // Only process "data:" lines; ignore event:, id:, retry:, and comments
            if (line.StartsWith("data: "))
            {
                var data = line.Substring(6); // Remove "data: " prefix
                dataLines.Add(data);
            }
        }
    }

    private static StreamingUpdate? ParseChunk(string json, ToolCallAccumulator accum)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var update = new StreamingUpdate();

            // Parse all standard fields
            if (root.TryGetProperty("id", out var id))
            {
                update.Id = id.GetString();
            }

            if (root.TryGetProperty("object", out var obj))
            {
                update.Object = obj.GetString();
            }

            if (root.TryGetProperty("created", out var created) && created.TryGetInt64(out var c))
            {
                update.Created = c;
            }

            if (root.TryGetProperty("model", out var model))
            {
                update.Model = model.GetString();
            }

            if (root.TryGetProperty("system_fingerprint", out var sf))
            {
                update.SystemFingerprint = sf.GetString();
            }

            if (root.TryGetProperty("service_tier", out var st))
            {
                update.ServiceTier = st.GetString();
            }

            // Parse usage (may appear in usage-only chunk or final chunk)
            if (root.TryGetProperty("usage", out var usageEl) && usageEl.ValueKind != JsonValueKind.Null)
            {
                update.Usage = JsonSerializer.Deserialize<TokenUsageInfo>(usageEl.GetRawText());
            }

            // Parse choices
            if (root.TryGetProperty("choices", out var choicesEl) && choicesEl.ValueKind == JsonValueKind.Array)
            {
                var choices = new List<StreamingChoice>();
                foreach (var choiceEl in choicesEl.EnumerateArray())
                {
                    var choice = ParseStreamingChoice(choiceEl, accum);
                    choices.Add(choice);
                }
                update.Choices = choices;
            }

            return update;
        }
        catch (JsonException)
        {
            return null; // Skip malformed chunks
        }
    }

    private static StreamingChoice ParseStreamingChoice(JsonElement choiceEl, ToolCallAccumulator accum)
    {
        var choice = new StreamingChoice();

        if (choiceEl.TryGetProperty("index", out var idx) && idx.TryGetInt32(out var i))
        {
            choice.Index = i;
        }

        if (choiceEl.TryGetProperty("finish_reason", out var fr) && fr.ValueKind != JsonValueKind.Null)
        {
            choice.FinishReason = fr.GetString();
        }

        if (choiceEl.TryGetProperty("delta", out var deltaEl) && deltaEl.ValueKind == JsonValueKind.Object)
        {
            choice.Delta = ParseDelta(deltaEl, choice.Index, accum);
        }

        return choice;
    }

    private static DeltaContent ParseDelta(JsonElement deltaEl, int choiceIndex, ToolCallAccumulator accum)
    {
        var delta = new DeltaContent();

        if (deltaEl.TryGetProperty("role", out var role))
        {
            delta.Role = role.GetString();
        }

        if (deltaEl.TryGetProperty("content", out var content) && content.ValueKind != JsonValueKind.Null)
        {
            delta.Content = content.GetString();
        }

        if (deltaEl.TryGetProperty("reasoning_content", out var rc) && rc.ValueKind != JsonValueKind.Null)
        {
            delta.ReasoningContent = rc.GetString();
        }

        if (deltaEl.TryGetProperty("refusal", out var refusal) && refusal.ValueKind != JsonValueKind.Null)
        {
            delta.Refusal = refusal.GetString();
        }

        // Parse tool_calls with index-based accumulation
        if (deltaEl.TryGetProperty("tool_calls", out var toolCallsEl) && toolCallsEl.ValueKind == JsonValueKind.Array)
        {
            var rawToolCalls = ParseRawToolCallDeltas(toolCallsEl);
            accum.MergeRawDeltas(rawToolCalls, choiceIndex);
            delta.ToolCalls = accum.GetMergedToolCalls(choiceIndex);
        }
        else
        {
            // Even without new tool_calls, return current merged state so callers see consistent data
            var merged = accum.GetMergedToolCalls(choiceIndex);
            if (merged.Count > 0)
            {
                delta.ToolCalls = merged;
            }
        }

        return delta;
    }

    /// <summary>
    /// Parses raw tool call deltas from JSON, extracting the "index" field from each element.
    /// The "index" field on streaming tool call deltas is critical — it identifies which
    /// parallel tool call this delta belongs to (0, 1, 2, etc.).
    /// </summary>
    private static List<RawToolCallDelta> ParseRawToolCallDeltas(JsonElement toolCallsEl)
    {
        var result = new List<RawToolCallDelta>();
        foreach (var tcEl in toolCallsEl.EnumerateArray())
        {
            var raw = new RawToolCallDelta();

            if (tcEl.TryGetProperty("index", out var idx) && idx.TryGetInt32(out var i))
            {
                raw.Index = i;
            }

            if (tcEl.TryGetProperty("id", out var id) && id.ValueKind != JsonValueKind.Null)
            {
                raw.Id = id.GetString();
            }

            if (tcEl.TryGetProperty("type", out var type))
            {
                raw.Type = type.GetString();
            }

            if (tcEl.TryGetProperty("function", out var funcEl) && funcEl.ValueKind == JsonValueKind.Object)
            {
                if (funcEl.TryGetProperty("name", out var name) && name.ValueKind != JsonValueKind.Null)
                {
                    raw.FunctionName = name.GetString();
                }

                if (funcEl.TryGetProperty("arguments", out var args) && args.ValueKind != JsonValueKind.Null)
                {
                    raw.Arguments = args.GetString();
                }
            }

            result.Add(raw);
        }
        return result;
    }

    /// <summary>
    /// Accumulates incremental tool call fields across multiple SSE events.
    /// OpenAI streams tool calls by index: name appears once, arguments arrive across multiple chunks.
    /// </summary>
    private class ToolCallAccumulator
    {
        // Key: (choiceIndex, toolIndex)
        private readonly Dictionary<(int choiceIndex, int toolIndex), MergeState> _states = new();

        public void MergeRawDeltas(List<RawToolCallDelta> deltas, int choiceIndex)
        {
            foreach (var d in deltas)
            {
                var key = (choiceIndex, d.Index);
                if (!_states.TryGetValue(key, out var state))
                {
                    state = new MergeState();
                    _states[key] = state;
                }

                if (d.Id != null)
                {
                    state.Id = d.Id;
                }

                if (d.FunctionName != null)
                {
                    state.Name = d.FunctionName;
                }

                if (d.Arguments != null)
                {
                    state.ArgsBuilder.Append(d.Arguments);
                }
            }
        }

        /// <summary>
        /// Returns all merged tool calls for a given choice index.
        /// Only returns tool calls that have at least a function name.
        /// </summary>
        public List<ToolCallRequest> GetMergedToolCalls(int choiceIndex)
        {
            var result = new List<ToolCallRequest>();
            foreach (var ((ci, _), state) in _states)
            {
                if (ci != choiceIndex)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(state.Name))
                {
                    continue;
                }

                result.Add(new ToolCallRequest
                {
                    Id = state.Id,
                    Type = "function",
                    Index = 0, // Index not needed after merging
                    Function = new FunctionCall
                    {
                        Name = state.Name,
                        Arguments = state.ArgsBuilder.ToString()
                    }
                });
            }
            return result;
        }

        public void Reset()
        {
            _states.Clear();
        }

        private class MergeState
        {
            public string Id { get; set; } = string.Empty;

            public string Name { get; set; } = string.Empty;

            public StringBuilder ArgsBuilder { get; } = new();
        }
    }

    private class RawToolCallDelta
    {
        public int Index { get; set; }

        public string? Id { get; set; }

        public string? Type { get; set; }

        public string? FunctionName { get; set; }

        public string? Arguments { get; set; }
    }
}