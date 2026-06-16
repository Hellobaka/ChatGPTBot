using System.Text.RegularExpressions;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Api;

/// <summary>
/// Message splitting for natural pacing. Supports LLM-based and regex-based splitting,
/// typing speed simulation, and random delays.
/// </summary>
public class Spliter
{
    private readonly string _message;
    private static readonly Regex RegexSplit = new(
        @"(?<=[。？！?!]|\.(?!\d|\.))", RegexOptions.Compiled);

    public Spliter(string message)
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
        var prompt = $"请将后续输入的一段话，按符合正常人节奏与习惯，" +
                     $"最大分段不能超过{AppConfig.SplitterMaxLines}段。" +
                     "分段拆分成Json数组，示例格式：['语句1', '语句2']。" +
                     "注意一定不要有影响到json格式的其他内容输出。" +
                     "上下文相关性很强的内容，一定要单独占一段，不得分开。" +
                     "不得精简我提供的内容，一定不得更改我的输入文本。" +
                     "每个分段结尾只能有问号、叹号或者省略号，逗号句号都不要。";

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
        var parts = RegexSplit.Split(_message)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        if (parts.Length <= AppConfig.SplitterMaxLines)
            return parts;

        var result = parts.Take(AppConfig.SplitterMaxLines - 1).ToList();
        result.Add(string.Join("", parts.Skip(AppConfig.SplitterMaxLines - 1)));
        return result.ToArray();
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
