using Microsoft.Extensions.AI;
using OpenAI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public class ResponseProcessor
    {
        private static Regex ThinkBlockRegex { get; set; } = new Regex(@"<think>[\s\S]*?</think>");

        public string ProcessResponse(string msg, string identity)
        {
            string reasoning = "";

            if (AppConfig.RemoveThinkBlock)
            {
                if (string.IsNullOrEmpty(reasoning))
                {
                    reasoning = ThinkBlockRegex.Match(msg).Value;
                }
                msg = ThinkBlockRegex.Replace(msg, "");
                while (msg.StartsWith("\n") || msg.StartsWith("\r") || msg.StartsWith(" "))
                {
                    msg = msg.Remove(0, 1);
                }
            }
            if (AppConfig.LogThinkBlock && !string.IsNullOrEmpty(reasoning))
            {
                MainSave.CQLog.Info("发起对话", "思考内容：" + reasoning);
            }

            return msg;
        }

        public string GetReasoningContent(OpenAI.Chat.StreamingChatCompletionUpdate chatUpdate)
        {
            var choicesProp = chatUpdate?.GetType().GetProperty(
                "Choices",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            );
            if (choicesProp == null)
            {
                return "";
            }

            if (choicesProp.GetValue(chatUpdate) is not IEnumerable choices)
            {
                return "";
            }

            var result = new List<string>();

            foreach (var choice in choices)
            {
                if (choice == null)
                {
                    continue;
                }

                var deltaProp = choice.GetType().GetProperty(
                    "Delta",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );
                if (deltaProp == null)
                {
                    continue;
                }

                var delta = deltaProp.GetValue(choice);
                if (delta == null)
                {
                    continue;
                }

                var rawDataProp = delta.GetType().GetProperty(
                    "SerializedAdditionalRawData",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );
                if (rawDataProp == null)
                {
                    continue;
                }

                if (rawDataProp.GetValue(delta) is not IDictionary<string, BinaryData> rawData)
                {
                    continue;
                }

                foreach (var kvp in rawData)
                {
                    if (kvp.Key == "reasoning_content")
                    {
                        return JsonSerializer.Deserialize<string>(Encoding.UTF8.GetString(kvp.Value.ToArray())) ?? "";
                    }
                }
            }

            return string.Join("\n", result);
        }

        public string AppendContentToMessage(OpenAI.Chat.ChatMessageContent contents)
        {
            string msg = "";
            foreach (OpenAI.Chat.ChatMessageContentPart contentPart in contents)
            {
                if (string.IsNullOrEmpty(contentPart.Text) && contentPart.ImageBytes != null && !contentPart.ImageBytes.IsEmpty)
                {
                    try
                    {
                        Directory.CreateDirectory(Path.Combine(MainSave.ImageDirectory, "ChatGPT"));
                        string filePath = Path.Combine(MainSave.ImageDirectory, "ChatGPT", $"{Guid.NewGuid()}.jpg");
                        File.WriteAllBytes(filePath, contentPart.ImageBytes.ToArray());
                        msg += $"[CQ:image,file=ChatGPT\\{Path.GetFileName(filePath)}]";
                    }
                    catch (Exception ex)
                    {
                        MainSave.CQLog?.Warning("图片处理", $"保存图片失败: {ex.Message}");
                        msg += "[图片处理失败]";
                    }
                }
                else if (!string.IsNullOrEmpty(contentPart.Text))
                {
                    msg += contentPart.Text;
                }
            }
            return msg;
        }
    }
}