using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model.CustomTools;
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
using System.Security.Policy;
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
            回复意愿,
            记忆提取,
        }

        public const string ErrorMessage = "连接发生问题，查看日志排查问题";

        private static Regex ThinkBlockRegex { get; set; } = new Regex(@"<think>[\s\S]*?</think>");

        private static IDistributedCache ChatCache { get; set; } = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

        private static JsonSerializerOptions DisableEscapingSerializerOptions { get; set; } = new JsonSerializerOptions()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false,
        };

        private static Dictionary<string, int> ToolCallCount { get; set; } = [];

        public static event Action<string, string>? OnToolCall;

        public static string GetChatResult(List<APIKeyPurpose> key, List<ChatMessage> chatMessages, Purpose purpose, bool jsonMode = false, int timeout = 10000, MCPClientManager? mcp = null, string? identity = null)
        {
            return GetChatResult(key.OrderBy(x => Guid.NewGuid()).FirstOrDefault(), chatMessages, purpose, jsonMode, timeout, mcp, identity);
        }

        public static string GetChatResult(APIKeyPurpose? key, List<ChatMessage> chatMessages, Purpose purpose, bool jsonMode = false, int timeout = 10000, MCPClientManager? mcp = null, string? identity = null)
        {
            if (key == null)
            {
                MainSave.CQLog?.Info("GetChatResult", "Key 为 null");
                return ErrorMessage;
            }
            return GetChatResult(key.Key.EndPoint, key.Key.APIKey, key.ModelName, chatMessages, purpose, jsonMode, timeout, mcp, identity);
        }

        public static string GetChatResult(string baseUrl, string apiKey, string modelName, List<ChatMessage> chatMessages, Purpose purpose, bool jsonMode = false, int timeout = 10000, MCPClientManager mcp = null, string? identity = null)
        {
            baseUrl = baseUrl.Replace("/chat/completions", "");
            string msg = "";
            string reasoning = "";

            var c = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions() { Endpoint = new(baseUrl), NetworkTimeout = TimeSpan.FromMilliseconds(timeout), }).GetChatClient(modelName);
            var client = c.AsIChatClient()
                            .AsBuilder()
                            .UseDistributedCache(ChatCache)
                            .UseFunctionInvocation(configure: (client) =>
                            {
                                client.AllowConcurrentInvocation = true;
                                client.IncludeDetailedErrors = true;
                                client.FunctionInvoker = new Func<FunctionInvocationContext, System.Threading.CancellationToken, ValueTask<object?>>(async (context, token) =>
                                {
                                    string id = identity;
                                    return await LogToolCall(context, token, identity);
                                });
                            })
                            .Build();
            var option = new ChatOptions
            {
                MaxOutputTokens = AppConfig.ChatMaxTokens,
                Temperature = AppConfig.ChatTemperature,
                ResponseFormat = jsonMode ? ChatResponseFormat.Json : ChatResponseFormat.Text,
            };
            if (mcp != null && AppConfig.EnableMCP)
            {
                option.Tools = mcp.GetAIFunctions();
            }
            try
            {
                if (identity != null)
                {
                    ToolCallCount[identity] = 0;
                }
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
                                if (openAIUpdate.Usage != null)
                                {
                                    usage = new UsageDetails()
                                    {
                                        InputTokenCount = openAIUpdate.Usage.InputTokenCount,
                                        OutputTokenCount = openAIUpdate.Usage.OutputTokenCount,
                                        TotalTokenCount = openAIUpdate.Usage.TotalTokenCount,
                                    };
                                }
                            }
                            else
                            {
                                foreach (var content in chatUpdate.Contents)
                                {
                                    if (content is UsageContent usageContent)
                                    {
                                        usage = usageContent?.Details;
                                    }
                                    else if (content is TextContent text)
                                    {
                                        msg += text.Text;
                                    }
                                    else if (content is DataContent dataContent)
                                    {
                                        Directory.CreateDirectory(Path.Combine(MainSave.ImageDirectory, "ChatGPT"));
                                        string filePath = Path.Combine(MainSave.ImageDirectory, "ChatGPT", $"{Guid.NewGuid()}.jpg");
                                        File.WriteAllBytes(filePath, dataContent.Data.ToArray());
                                        msg += $"[CQ:image,file=ChatGPT\\{Path.GetFileName(filePath)}]";
                                    }
                                }
                                if (chatUpdate.FinishReason == ChatFinishReason.ToolCalls
                                       && AppConfig.EnableMCP
                                       && !string.IsNullOrEmpty(msg))
                                {
                                    CommonHelper.DebugLog("Tool_消息切片", msg);
                                    OnToolCall?.Invoke(identity, msg);
                                    msg = "";
                                }
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
                        else
                        {
                            msg += response.Text;
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

                var pendingPictures = PictureContextManager.GetAndClearPictures(identity);
                if (pendingPictures != null && pendingPictures.Count> 0)
                {
                    bool success = false;
                    foreach (var hash in pendingPictures)
                    {
                        if (Picture.Cache.TryGetValue(hash, out var picture) && picture != null
                            && File.Exists(picture.FilePath))
                        {
                            MainSave.CQLog?.Info("附加图片", $"向对话 {identity} 附加图片 {hash}，路径 {picture.FilePath}");
                            chatMessages.Add(new(ChatRole.User, [new DataContent(File.ReadAllBytes(picture.FilePath), "image/jpg")]));
                            success = true;
                        }
                    }
                    if (!success)
                    {
                        MainSave.CQLog?.Info("附加图片", $"向对话 {identity} 附加图片失败，可能是由于图片不存在");
                    }
                    else
                    {
                        return GetChatResult(baseUrl, apiKey, modelName, chatMessages, purpose, jsonMode, timeout, mcp, identity);
                    }
                }
            }
            catch (Exception ex)
            {
                MainSave.CQLog?.Info("OpenAI_ChatCompletions失败", ex);
                msg = ErrorMessage;
            }
            finally
            {
                if (identity != null)
                {
                    ToolCallCount.Remove(identity);
                }
            }
            return msg;
        }

        private static async Task<object> LogToolCall(FunctionInvocationContext context, System.Threading.CancellationToken token, string identity)
        {
            CommonHelper.DebugLog("ToolCall追踪", $"调用函数 {context.Function.Name}，参数 {JsonSerializer.Serialize(context.Arguments, DisableEscapingSerializerOptions)}");
            try
            {
                if (ToolCallCount.TryGetValue(identity, out int count))
                {
                    if (count >= AppConfig.MaxToolCallCountEachTurn)
                    {
                        MainSave.CQLog?.Warning("ToolCall追踪", $"本轮对话已调用 {count} 次Tool，无法再调用");
                        return "The maximum tool call limit for this turn has been reached, you cannot do tool calls any more.";
                    }
                    ToolCallCount[identity]++;
                }
                var result = await context.Function.InvokeAsync(context.Arguments, token);
                CommonHelper.DebugLog("ToolCall追踪", $"函数 {context.Function.Name} 调用完成，结果 {JsonSerializer.Serialize(result, DisableEscapingSerializerOptions)}");
                return result;
            }
            catch (Exception e)
            {
                CommonHelper.DebugLog("ToolCall追踪", $"函数 {context.Function.Name} 调用失败，错误信息 {e.Message}");
                return $"Exception when call tool {context.Function.Name}, {e}";
            }
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