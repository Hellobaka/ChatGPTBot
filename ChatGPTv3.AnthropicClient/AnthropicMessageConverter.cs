using System.Text.Json;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.AnthropicClient;

/// <summary>
/// Converts the shared OpenAI-style ChatMessage conversation history into
/// Anthropic Messages API requests:
/// - system messages → top-level "system" string;
/// - assistant ToolCalls → tool_use blocks (arguments JSON string → input object);
/// - "tool" messages → user messages with tool_result blocks;
/// - consecutive same-role turns are merged (the API combines them anyway).
/// </summary>
public static class AnthropicMessageConverter
{
    public static AnthropicChatRequest CreateRequest(
        List<ChatMessage> history,
        string model,
        int maxTokens,
        float? temperature,
        bool stream,
        List<AnthropicTool>? tools = null,
        bool jsonMode = false,
        bool enableWebSearch = false)
    {
        var systemParts = new List<string>();
        var messages = new List<AnthropicMessage>();

        foreach (var msg in history)
        {
            if (msg.Role == "system")
            {
                var text = GetText(msg);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    systemParts.Add(text);
                }
                continue;
            }

            if (msg.Role is "user" or "assistant")
            {
                messages.Add(ConvertUserOrAssistant(msg));
            }
            else if (msg.Role == "tool")
            {
                messages.Add(new AnthropicMessage
                {
                    Role = "user",
                    Content =
                    [
                        new AnthropicContentBlock
                        {
                            Type = "tool_result",
                            ToolUseId = msg.ToolCallId ?? string.Empty,
                            Content = GetText(msg) ?? string.Empty,
                            IsError = false
                        }
                    ]
                });
            }
        }

        if (jsonMode)
        {
            systemParts.Add("You must respond with a single valid JSON object only, with no extra text.");
        }

        var requestTools = tools != null ? new List<AnthropicTool>(tools) : [];
        if (enableWebSearch)
        {
            requestTools.Add(AnthropicTool.WebSearch());
        }

        return new AnthropicChatRequest
        {
            Model = model,
            MaxTokens = maxTokens,
            Temperature = temperature,
            Stream = stream,
            System = systemParts.Count > 0 ? string.Join("\n\n", systemParts) : null,
            Messages = MergeConsecutive(messages),
            Tools = requestTools.Count > 0 ? requestTools : null
        };
    }

    public static List<AnthropicTool> ToTools(List<ToolDefinition> definitions) =>
        definitions.Select(AnthropicTool.FromToolDefinition).ToList();

    private static AnthropicMessage ConvertUserOrAssistant(ChatMessage msg)
    {
        var blocks = new List<AnthropicContentBlock>();

        if (msg.Parts is { Count: > 0 })
        {
            foreach (var part in msg.Parts)
            {
                if (part.Type == "text")
                {
                    blocks.Add(new AnthropicContentBlock { Type = "text", Text = part.Text ?? string.Empty });
                }
                else if (part.Type == "image_url" && part.ImageUrl != null)
                {
                    var source = CreateImageSource(part.ImageUrl.Url);
                    if (source != null)
                    {
                        blocks.Add(new AnthropicContentBlock { Type = "image", Source = source });
                    }
                }
            }
        }
        else
        {
            var text = GetText(msg);
            if (!string.IsNullOrEmpty(text))
            {
                blocks.Add(new AnthropicContentBlock { Type = "text", Text = text });
            }
        }

        if (msg.Role == "assistant" && msg.ToolCalls is { Count: > 0 })
        {
            foreach (var tc in msg.ToolCalls)
            {
                blocks.Add(new AnthropicContentBlock
                {
                    Type = "tool_use",
                    Id = tc.Id,
                    Name = tc.Function.Name,
                    Input = ParseArgumentsToElement(tc.Function.Arguments)
                });
            }
        }

        if (blocks.Count == 0)
        {
            blocks.Add(new AnthropicContentBlock { Type = "text", Text = string.Empty });
        }

        return new AnthropicMessage
        {
            Role = msg.Role == "assistant" ? "assistant" : "user",
            Content = blocks
        };
    }

    private static AnthropicImageSource? CreateImageSource(string url)
    {
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = url.IndexOf(',');
            if (comma <= 0)
            {
                return null;
            }

            var header = url[5..comma];
            var semi = header.IndexOf(';');
            var mediaType = semi > 0 ? header[..semi] : "image/jpeg";
            return new AnthropicImageSource
            {
                Type = "base64",
                MediaType = mediaType,
                Data = url[(comma + 1)..]
            };
        }

        return new AnthropicImageSource
        {
            Type = "url",
            Url = url
        };
    }

    private static JsonElement ParseArgumentsToElement(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return JsonSerializer.SerializeToElement(new Dictionary<string, object?>());
        }

        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement.Clone();
            if (root.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                return root;
            }

            return JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["value"] = root });
        }
        catch
        {
            return JsonSerializer.SerializeToElement(new Dictionary<string, object?> { ["value"] = arguments });
        }
    }

    private static List<AnthropicMessage> MergeConsecutive(List<AnthropicMessage> messages)
    {
        var merged = new List<AnthropicMessage>();
        foreach (var message in messages)
        {
            if (merged.Count > 0 && merged[^1].Role == message.Role)
            {
                merged[^1].Content.AddRange(message.Content);
            }
            else
            {
                merged.Add(message);
            }
        }

        return merged;
    }

    private static string? GetText(ChatMessage msg)
    {
        if (msg.Content is string s)
        {
            return s;
        }

        if (msg.Content is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.String)
            {
                return je.GetString();
            }

            if (je.ValueKind == JsonValueKind.Array)
            {
                var texts = new List<string>();
                foreach (var part in je.EnumerateArray())
                {
                    if (part.TryGetProperty("type", out var type) &&
                        type.GetString() == "text" &&
                        part.TryGetProperty("text", out var text))
                    {
                        texts.Add(text.GetString() ?? string.Empty);
                    }
                }

                return string.Join("", texts);
            }
        }

        return msg.Content?.ToString();
    }
}
