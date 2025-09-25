using Microsoft.Extensions.AI;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public class Splitter
    {
        public Splitter(string message)
        {
            Message = message;
        }

        public string Message { get; private set; }

        private Regex LineSplitRegex { get; set; } = new Regex(@"(?<=[。？！?.!])");

        private static Regex EmojiSplitRegex { get; set; } = new Regex(@"<@Emoji(.*?)>");

        private static string Prompt { get; set; } = "请将后续输入的一段话，按符合正常人节奏与习惯，最大分段不能超过$MaxLines$段。分段拆分成Json数组，示例格式：['语句1', '语句2']。注意一定不要有影响到json格式的其他内容输出。上下文相关性很强的内容，一定要单独占一段，不得分开。不得精简我提供的内容，一定不得更改我的输入文本。每个分段结尾只能有问号、叹号或者省略号，逗号句号都不要";

        public string[] Split()
        {
            CommonHelper.DebugLog("消息分行", $"开始进行消息分行：{Message}");
            if (Message.Length <= AppConfig.SplitterMinLength)
            {
                return [Message];
            }
            if (AppConfig.SplitterRegexFirst)
            {
                return RegexSplit();
            }
            string prompt = Prompt.Replace("$MaxLines$", AppConfig.SplitterMaxLines.ToString());
            string result = Chat.GetChatResult(AppConfig.SplitterApiKeyId, new List<ChatMessage>
            {
                new(ChatRole.System, prompt),
                new(ChatRole.User, Message)
            }, Chat.Purpose.分段, jsonMode: false, timeout: AppConfig.SplitterTimeout);
            if (result != Chat.ErrorMessage)
            {
                try
                {
                    result = result.Trim().TrimStart('\n').TrimEnd('\n');
                    var arr = JArray.Parse(result);
                    List<string> lines = new();
                    foreach (var line in arr)
                    {
                        var str = line.ToString();
                        if (AppConfig.SplitterRegexRemovePunctuation && (str.EndsWith("。") || str.EndsWith(".") || str.EndsWith("，") || str.EndsWith(",")))
                        {
                            str = str.Substring(0, str.Length - 1);
                        }
                        if (lines.Count < AppConfig.SplitterMaxLines)
                        {
                            lines.Add(str);
                        }
                        else
                        {
                            lines[lines.Count - 1] += str;
                        }
                    }
                    return lines.ToArray();
                }
                catch
                {
                    System.Diagnostics.Debugger.Break();
                    MainSave.CQLog?.Info("消息分行", $"进行拆分时，Json解析错误\n{result}");
                    return RegexSplit();
                }
            }
            else
            {
                System.Diagnostics.Debugger.Break();
                MainSave.CQLog?.Info("消息分行", "进行拆分时，大模型返回结果错误");
                return RegexSplit();
            }
        }

        public static (bool isEmoji, string content)[] SplitEmoji(string input)
        {
            (bool isEmoji, string content)[] values = [];
            foreach(var item in input.SplitV2("<@Emoji.*?>"))
            {
                if (item.StartsWith("<@Emoji"))
                {
                    var emotion = EmojiSplitRegex.Match(item).Groups[1].Value.Replace("{", "").Replace("}", "");
                    values = [.. values, (true, emotion)];
                }
                else
                {
                    values = [.. values, (false, item)];
                }
            }

            return values;
        }

        private string[] RegexSplit()
        {
            string[] sentences = LineSplitRegex.Split(Message);

            if (sentences.Length <= AppConfig.SplitterMaxLines)
            {
                return sentences;
            }

            string[] limitedArray = new string[AppConfig.SplitterMaxLines];
            for (int i = 0; i < AppConfig.SplitterMaxLines; i++)
            {
                limitedArray[i] = sentences[i];
            }
            for (int i = 0; i < limitedArray.Length; i++)
            {
                string str = limitedArray[i];
                if (AppConfig.SplitterRegexRemovePunctuation && (str.EndsWith("。") || str.EndsWith(".") || str.EndsWith("，") || str.EndsWith(",")))
                {
                    str = str.Substring(0, str.Length - 1);
                }
                limitedArray[i] = str;
            }

            string overflow = string.Join("", sentences, AppConfig.SplitterMaxLines - 1, sentences.Length - (AppConfig.SplitterMaxLines - 1));
            limitedArray[AppConfig.SplitterMaxLines - 1] = overflow;

            return limitedArray;
        }
    }
}
