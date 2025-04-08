using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public class Spliter
    {
        public Spliter(string message)
        {
            Message = message;
        }

        public string Message { get; private set; }

        private Regex LineSplitRegex { get; set; } = new Regex(@"(?<=[。？！?.!])");

        public string[] Split()
        {
            MainSave.CQLog?.Debug("消息分行", $"开始进行消息分行：{Message}");

            if (AppConfig.SpliterRegexFirst || Message.Length <= AppConfig.SpliterMinLength)
            {
                return RegexSplit();
            }
            string prompt = AppConfig.SpliterPrompt.Replace("$MaxLines$", AppConfig.SpliterMaxLines.ToString());
            string result = Chat.GetChatResult(AppConfig.SpliterUrl, AppConfig.SpliterApiKey, new List<ChatMessage>
            {
                new SystemChatMessage(prompt),
                new UserChatMessage(Message)
            }, AppConfig.SpliterModelName, Chat.Purpose.分段);
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
                        if (AppConfig.SpliterRegexRemovePunctuation && (str.EndsWith("。") || str.EndsWith(".") || str.EndsWith("，") || str.EndsWith(",")))
                        {
                            str = str.Substring(0, str.Length - 1);
                        }
                        if (lines.Count < AppConfig.SpliterMaxLines)
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

        private string[] RegexSplit()
        {
            string[] sentences = LineSplitRegex.Split(Message);

            if (sentences.Length <= AppConfig.SpliterMaxLines)
            {
                return sentences;
            }

            string[] limitedArray = new string[AppConfig.SpliterMaxLines];
            for (int i = 0; i < AppConfig.SpliterMaxLines; i++)
            {
                limitedArray[i] = sentences[i];
            }
            for (int i = 0; i < limitedArray.Length; i++)
            {
                string str = limitedArray[i];
                if (AppConfig.SpliterRegexRemovePunctuation && (str.EndsWith("。") || str.EndsWith(".") || str.EndsWith("，") || str.EndsWith(",")))
                {
                    str = str.Substring(0, str.Length - 1);
                }
                limitedArray[i] = str;
            }

            string overflow = string.Join("", sentences, AppConfig.SpliterMaxLines - 1, sentences.Length - (AppConfig.SpliterMaxLines - 1));
            limitedArray[AppConfig.SpliterMaxLines - 1] = overflow;

            return limitedArray;
        }
    }
}
