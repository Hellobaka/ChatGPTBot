using System.Text.Json;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.ResponsesClient;

/// <summary>
/// Converts the shared OpenAI-style ChatMessage conversation history into
/// Responses API input items:
/// - system messages → "instructions";
/// - user/assistant messages → message items (input_text for user, output_text for assistant);
/// - assistant ToolCalls → function_call items (arguments stay a JSON string);
/// - "tool" messages → function_call_output items.
/// </summary>
public static class ResponsesMessageConverter
{
    public static ResponsesCreateRequest CreateRequest(
        List<ChatMessage> history,
        string model,
        int? maxOutputTokens,
        float? temperature,
        bool stream,
        List<ResponsesTool>? tools = null,
        bool jsonMode = false,
        bool enableWebSearch = false)
    {
        var input = new List<object>();
        var instructions = new List<string>();

        foreach (var msg in history)
        {
            var text = GetText(msg);

            switch (msg.Role)
            {
                case "system":
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        instructions.Add(text);
                    }
                    break;

                case "user":
                    input.Add(CreateMessageItem("user", msg));
                    break;

                case "assistant":
                    if (msg.ToolCalls is { Count: > 0 })
                    {
                        if (!string.IsNullOrEmpty(text))
                        {
                            input.Add(new
                            {
                                type = "message",
                                role = "assistant",
                                content = new[]
                                {
                                    new { type = "output_text", text }
                                }
                            });
                        }

                        foreach (var tc in msg.ToolCalls)
                        {
                            input.Add(new
                            {
                                type = "function_call",
                                call_id = tc.Id,
                                name = tc.Function.Name,
                                arguments = tc.Function.Arguments ?? "{}"
                            });
                        }
                    }
                    else
                    {
                        input.Add(new
                        {
                            type = "message",
                            role = "assistant",
                            content = new[]
                            {
                                new { type = "output_text", text = text ?? string.Empty }
                            }
                        });
                    }
                    break;

                case "tool":
                    input.Add(new
                    {
                        type = "function_call_output",
                        call_id = msg.ToolCallId,
                        output = text ?? string.Empty
                    });
                    break;
            }
        }

        if (jsonMode)
        {
            instructions.Add("You must respond with a single valid JSON object only, with no extra text.");
        }

        var requestTools = tools != null ? new List<ResponsesTool>(tools) : [];
        if (enableWebSearch)
        {
            requestTools.Add(ResponsesTool.WebSearch());
        }

        var request = new ResponsesCreateRequest
        {
            Model = model,
            Instructions = instructions.Count > 0 ? string.Join("\n\n", instructions) : null,
            Input = input,
            MaxOutputTokens = maxOutputTokens,
            Temperature = temperature,
            Stream = stream,
            Tools = requestTools.Count > 0 ? requestTools : null
        };

        if (jsonMode)
        {
            request.EnableJsonMode();
        }

        return request;
    }

    public static List<ResponsesTool> ToTools(List<ToolDefinition> definitions) =>
        definitions.Select(ResponsesTool.FromToolDefinition).ToList();

    private static object CreateMessageItem(string role, ChatMessage msg)
    {
        var content = new List<object>();

        if (msg.Parts is { Count: > 0 })
        {
            foreach (var part in msg.Parts)
            {
                if (part.Type == "text")
                {
                    content.Add(new { type = "input_text", text = part.Text ?? string.Empty });
                }
                else if (part.Type == "image_url" && part.ImageUrl != null)
                {
                    content.Add(new { type = "input_image", image_url = part.ImageUrl.Url });
                }
            }
        }
        else
        {
            content.Add(new { type = "input_text", text = GetText(msg) ?? string.Empty });
        }

        return new { type = "message", role, content };
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
