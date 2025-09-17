using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenAI;
using System;
using System.ClientModel;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public class Chat
    {
        public enum Purpose
        {
            聊天,
            图片描述,
            日程获取,
            分段,
            表情包推荐,
        }

        public const string ErrorMessage = "连接发生问题，查看日志排查问题";

        private static Regex ThinkBlockRegex { get; set; } = new Regex(@"<think>[\s\S]*?</think>");

        private static IDistributedCache ChatCache { get; set; } = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

        public static string GetChatResult(List<APIKeyPurpose> key, List<ChatMessage> chatMessages, Purpose purpose, bool jsonMode = false, int timeout = 10000, MCPClientManager mcp = null)
        {
            return GetChatResult(key.OrderBy(x => Guid.NewGuid()).FirstOrDefault(), chatMessages, purpose, jsonMode, timeout, mcp);
        }

        public static string GetChatResult(APIKeyPurpose? key, List<ChatMessage> chatMessages, Purpose purpose, bool jsonMode = false, int timeout = 10000, MCPClientManager mcp = null)
        {
            if (key == null)
            {
                MainSave.CQLog?.Info("GetChatResult", "Key 为 null");
                return ErrorMessage;
            }
            return GetChatResult(key.Key.EndPoint, key.Key.APIKey, key.ModelName, chatMessages, purpose, jsonMode, timeout, mcp);
        }

        public static string GetChatResult(string baseUrl, string apiKey, string modelName, List<ChatMessage> chatMessages, Purpose purpose, bool jsonMode = false, int timeout = 10000, MCPClientManager mcp = null)
        {
            baseUrl = baseUrl.Replace("/chat/completions", "");
            string msg = "";
            string reasoning = "";

            var c = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions() { Endpoint = new(baseUrl), NetworkTimeout = TimeSpan.FromMilliseconds(timeout), }).GetChatClient(modelName);
            var client = c.AsIChatClient()
                            .AsBuilder()
                            .UseDistributedCache(ChatCache)
                            .UseFunctionInvocation()
                            .UseLogging()
                            .Build();
            var option = new ChatOptions
            {
                MaxOutputTokens = AppConfig.ChatMaxTokens,
                Temperature = AppConfig.ChatTemperature,
                ResponseFormat = jsonMode ? ChatResponseFormat.Text : ChatResponseFormat.Json,
            };
            if (mcp != null)
            {
                option.Tools = mcp.GetAIFunctions();
            }
            try
            {
                UsageDetails? usage = null;
                if (AppConfig.StreamMode)
                {
                    Task.Run(async () =>
                    {
                        await foreach (var chatUpdate in client.GetStreamingResponseAsync(chatMessages, option))
                        {
                            if (chatUpdate.RawRepresentation is OpenAI.Chat.StreamingChatCompletionUpdate openAIUpdate)
                            {
                                msg += AppendContentToMessage(openAIUpdate.ContentUpdate);
                                reasoning += GetReasoningContent(openAIUpdate);
                            }
                            var usageDetail = chatUpdate.Contents.OfType<UsageContent>().FirstOrDefault()?.Details;
                            if (usageDetail != null)
                            {
                                usage = usageDetail;
                            }
                        }
                    }).Wait();
                }
                else
                {
                    Task.Run(async () =>
                    {
                        var response = await client.GetResponseAsync(chatMessages, option);
                        if (response.RawRepresentation is OpenAI.Chat.StreamingChatCompletionUpdate openAIUpdate)
                        {
                            msg += AppendContentToMessage(openAIUpdate.ContentUpdate);
                            reasoning += GetReasoningContent(openAIUpdate);
                        }
                        usage = response.Usage;
                    }).Wait();
                }
                if (usage != null)
                {
                    Usage.Insert(baseUrl, modelName, purpose.ToString(), usage.InputTokenCount.Value, usage.OutputTokenCount.Value);
                    APIKeys.UpdateTokenConsume(apiKey, usage.TotalTokenCount.Value);
                }
                chatMessages.Add(new(ChatRole.Assistant, msg));

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
            }
            catch (Exception ex)
            {
                MainSave.CQLog?.Info("OpenAI_ChatCompletions失败", ex);
                msg = ErrorMessage;
            }
            return msg;
        }

        private static string GetReasoningContent(OpenAI.Chat.StreamingChatCompletionUpdate chatUpdate)
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

        private static string AppendContentToMessage(OpenAI.Chat.ChatMessageContent contents)
        {
            string msg = "";
            foreach (OpenAI.Chat.ChatMessageContentPart contentPart in contents)
            {
                if (string.IsNullOrEmpty(contentPart.Text) && contentPart.ImageBytes != null && !contentPart.ImageBytes.IsEmpty)
                {
                    Directory.CreateDirectory(Path.Combine(MainSave.ImageDirectory, "ChatGPT"));
                    string filePath = Path.Combine(MainSave.ImageDirectory, "ChatGPT", $"{Guid.NewGuid()}.jpg");
                    File.WriteAllBytes(filePath, contentPart.ImageBytes.ToArray());
                    msg += $"[CQ:image,file=ChatGPT\\{Path.GetFileName(filePath)}]";
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