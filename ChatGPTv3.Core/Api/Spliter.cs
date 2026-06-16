using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;
using System.Text.RegularExpressions;

namespace ChatGPTv3.Core.Api;

/// <summary>
/// Message splitting for natural pacing. Supports LLM-based and regex-based splitting,
/// typing speed simulation, and random delays.
/// </summary>
public class Splitter
{
    private readonly string _message;
    private static readonly Regex RegexSplit = new(
        @"(?<=[。？！?!]|\.(?!\d|\.))", RegexOptions.Compiled);

    private static string Prompt { get; set; } = """
你是一个严格的文本切分器。你的唯一任务是将输入的完整文本，在不改变任何文字、标点、空格、换行的前提下，按语义完整度拆分成多段。

规则：
1. 输出的 JSON 数组中的各段按顺序拼接后，必须与输入文本**完全一致**（逐字符相同）。
2. 你只能决定在哪里分段，绝对不能添加、删除、修改任何一个字符（包括标点符号）。
3. 每个分段应尽量保持语义完整，但不得为了完整而修改原文。
4. 分段数量不能超过 $MaxLines$ 段。如果原文很短，分段数可以小于 $MaxLines$，甚至为 1 段。
5. 如果原文已经无法再拆分，直接返回包含整个原文的数组。

示例输入："今天天气真好。我们去公园吧！听说那里樱花开了，要不要一起？"
示例输出：["今天天气真好。","我们去公园吧！","听说那里樱花开了，要不要一起？"]

请直接输出 JSON 数组，不要包含任何其他文字。
""";

    private static string RemoveMarkdownPrompt { get; set; } = """
【预处理指令】
在开始分段之前，先对输入文本做以下清理：
- 移除所有 Markdown 标记，只保留纯文本。
- 删除所有空行，合并连续换行。
完成清理后，将得到的纯文本作为新输入，然后按下面的规则分段。
""";

    public Splitter(string message)
    {
        _message = message;
    }

    public string[] Split()
    {
        if (string.IsNullOrEmpty(_message)) return [_message];
        if (_message.Length <= AppConfig.SplitterMinLength) return [_message];

        if (AppConfig.SplitterRegexFirst)
            return RegexSplitFallback();

        try
        {
            return LlmSplit();
        }
        catch
        {
            return RegexSplitFallback();
        }
    }

    private string[] LlmSplit()
    {
        var prompt = Prompt.Replace("$MaxLines$", AppConfig.SplitterMaxLines.ToString());
        if (AppConfig.EnableSplitterRemoveMarkdown)
        {
            prompt = RemoveMarkdownPrompt + prompt;
        }

        var messages = new List<ChatMessage>
        {
            ChatMessage.System(prompt),
            ChatMessage.User(_message)
        };

        var chatService = new ChatService();
        var result = chatService.GetChatResultAsync(
            AppConfig.SplitterApiKeyId, messages,
            ChatService.Purpose.分段, jsonMode: true,
            timeout: AppConfig.SplitterTimeout).Result;

        if (result == ChatService.ErrorMessage)
            throw new Exception("Splitter API error");

        return ParseSplitResult(result);
    }

    private string[] ParseSplitResult(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var lines = new List<string>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var line = item.GetString() ?? string.Empty;
                if (AppConfig.SplitterRegexRemovePunctuation)
                {
                    line = line.TrimEnd('。', '.', '，', ',');
                }
                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line);
            }

            // Truncate to max lines, append overflow to last
            if (lines.Count > AppConfig.SplitterMaxLines)
            {
                var overflow = string.Join("", lines.Skip(AppConfig.SplitterMaxLines - 1));
                lines = lines.Take(AppConfig.SplitterMaxLines - 1).ToList();
                lines.Add(overflow);
            }

            return lines.ToArray();
        }
        catch
        {
            return RegexSplitFallback();
        }
    }

    private string[] RegexSplitFallback()
    {
        var text = AppConfig.EnableSplitterRemoveMarkdown ? RemoveMarkdownAndEmptyLines(_message) : _message;

        var parts = RegexSplit.Split(text)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        if (parts.Length <= AppConfig.SplitterMaxLines)
            return parts;

        var result = parts.Take(AppConfig.SplitterMaxLines - 1).ToList();
        result.Add(string.Join("", parts.Skip(AppConfig.SplitterMaxLines - 1)));
        return result.ToArray();
    }

    private static string RemoveMarkdownAndEmptyLines(string input)
    {
        // 移除常见 Markdown 标记（示例，可按需扩展）
        var text = Regex.Replace(input, @"\*\*(.*?)\*\*", "$1");          // 加粗
        text = Regex.Replace(text, @"\*(.*?)\*", "$1");                  // 斜体
        text = Regex.Replace(text, @"`(.*?)`", "$1");                    // 行内代码
        text = Regex.Replace(text, @"```[\s\S]*?```", m => m.Value.Replace("```", "").Trim()); // 代码块
        text = Regex.Replace(text, @"\[([^\]]+)\]\([^)]+\)", "$1");     // 链接
        text = Regex.Replace(text, @"^#{1,6}\s+", "", RegexOptions.Multiline); // 标题
        text = Regex.Replace(text, @"^>\s+", "", RegexOptions.Multiline);      // 引用
        text = Regex.Replace(text, @"^[-*+]\s+", "", RegexOptions.Multiline);  // 无序列表
        text = Regex.Replace(text, @"^\d+\.\s+", "", RegexOptions.Multiline);  // 有序列表
        text = Regex.Replace(text, @"~~(.*?)~~", "$1");                 // 删除线

        // 处理空行和连续换行
        text = Regex.Replace(text, @"\n\s*\n", "\n");                   // 空行变单换行
        text = Regex.Replace(text, @"\n{3,}", "\n");                    // 多个换行合并

        return text.Trim();
    }

    /// <summary>
    /// Splits message on emoji markers: <@Emoji{description}>
    /// Returns array of (isEmoji, content) tuples.
    /// </summary>
    public static (bool isEmoji, string content)[] SplitEmoji(string message)
    {
        var regex = new Regex(@"<@Emoji(.*?)>");
        var parts = new List<(bool, string)>();
        int lastIndex = 0;

        foreach (Match match in regex.Matches(message))
        {
            if (match.Index > lastIndex)
            {
                var text = message.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrWhiteSpace(text))
                    parts.Add((false, text));
            }
            parts.Add((true, match.Groups[1].Value.Trim()));
            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < message.Length)
        {
            var remaining = message.Substring(lastIndex);
            if (!string.IsNullOrWhiteSpace(remaining))
                parts.Add((false, remaining));
        }

        return parts.ToArray();
    }

    /// <summary>
    /// Calculates delay based on simulated typing speed + random jitter.
    /// </summary>
    public static async Task ApplyTypingDelay(string text, CancellationToken ct = default)
    {
        if (!AppConfig.EnableSplitterRandomDelay) return;

        var typingMs = (int)(text.Length / (AppConfig.SplitterSimulateTypeSpeed / 60.0) * 1000);
        var randomMs = CommonHelper.Next(AppConfig.SplitterRandomDelayMin, AppConfig.SplitterRandomDelayMax);
        var totalMs = typingMs + randomMs;

        if (totalMs > 0)
        {
            try { await Task.Delay(totalMs, ct); } catch (OperationCanceledException) { }
        }
    }
}
