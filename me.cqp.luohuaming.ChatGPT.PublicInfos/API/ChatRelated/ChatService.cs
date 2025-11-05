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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public class ChatService
    {
        private readonly IDistributedCache _cache;
        private readonly JsonSerializerOptions _disableEscapingSerializerOptions;
        private readonly ToolCallService _toolCallService;
        private readonly ResponseProcessor _responseProcessor;
        private readonly UsageTracker _usageTracker;

        public ChatService()
        {
            _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
            _disableEscapingSerializerOptions = new JsonSerializerOptions()
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false,
            };
            _toolCallService = new ToolCallService(_disableEscapingSerializerOptions);
            _responseProcessor = new ResponseProcessor();
            _usageTracker = new UsageTracker();
        }

        public string GetChatResult(List<APIKeyPurpose> key, List<ChatMessage> chatMessages, Chat.Purpose purpose, bool jsonMode = false, int timeout = 10000, MCPClientManager? mcp = null, string? identity = null)
        {
            return GetChatResult(key.OrderBy(x => Guid.NewGuid()).FirstOrDefault(), chatMessages, purpose, jsonMode, timeout, mcp, identity);
        }

        public string GetChatResult(APIKeyPurpose? key, List<ChatMessage> chatMessages, Chat.Purpose purpose, bool jsonMode = false, int timeout = 10000, MCPClientManager? mcp = null, string? identity = null)
        {
            if (key == null)
            {
                MainSave.CQLog?.Info("GetChatResult", "Key 为 null");
                return Chat.ErrorMessage;
            }
            return GetChatResult(key.Key.EndPoint, key.Key.APIKey, key.Model, chatMessages, purpose, jsonMode, timeout, mcp, identity);
        }

        public string GetChatResult(string baseUrl,
                                   string apiKey,
                                   LLMModel model,
                                   List<ChatMessage> chatMessages,
                                   Chat.Purpose purpose,
                                   bool jsonMode = false,
                                   int timeout = 10000,
                                   MCPClientManager mcp = null,
                                   string? identity = null)
        {
            baseUrl = baseUrl.Replace("/chat/completions", "");
            string msg = "";

            try
            {
                var client = CreateChatClient(baseUrl, apiKey, model.Name, timeout, mcp, identity);
                var option = CreateChatOptions(jsonMode, mcp);

                _toolCallService.ResetToolCallState(identity);

                UsageDetails? usage = null;
                if (AppConfig.StreamMode)
                {
                    (msg, usage) = ProcessStreamingResponse(client, chatMessages, option, identity);
                }
                else
                {
                    (msg, usage) = ProcessNonStreamingResponse(client, chatMessages, option);
                }

                if (usage != null)
                {
                    _usageTracker.TrackUsage(baseUrl, model, purpose.ToString(), usage, apiKey);
                }

                chatMessages.Add(new(ChatRole.Assistant, msg));

                msg = _responseProcessor.ProcessResponse(msg, identity);

                return HandlePendingPictures(baseUrl, apiKey, model, chatMessages, purpose, jsonMode, timeout, mcp, identity, msg);
            }
            catch (Exception ex)
            {
                MainSave.CQLog?.Info("OpenAI_ChatCompletions失败", ex);
                return Chat.ErrorMessage;
            }
            finally
            {
                _toolCallService.CleanupToolCallState(identity);
            }
        }

        private IChatClient CreateChatClient(string baseUrl, string apiKey, string modelName, int timeout, MCPClientManager mcp, string identity)
        {
            var c = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions() { Endpoint = new(baseUrl), NetworkTimeout = TimeSpan.FromMilliseconds(timeout), }).GetChatClient(modelName);
            return c.AsIChatClient()
                    .AsBuilder()
                    .UseDistributedCache(_cache)
                    .UseFunctionInvocation(configure: (client) =>
                    {
                        client.AllowConcurrentInvocation = true;
                        client.IncludeDetailedErrors = true;
                        client.FunctionInvoker = new Func<FunctionInvocationContext, System.Threading.CancellationToken, ValueTask<object?>>(async (context, token) =>
                        {
                            return await _toolCallService.LogToolCall(context, token, identity);
                        });
                    })
                    .Build();
        }

        private ChatOptions CreateChatOptions(bool jsonMode, MCPClientManager mcp)
        {
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
            return option;
        }

        private (string msg, UsageDetails? usage) ProcessStreamingResponse(IChatClient client, List<ChatMessage> chatMessages, ChatOptions option, string identity)
        {
            string msg = "";
            string reasoning = "";
            UsageDetails? usage = null;

            Task.Run(async () =>
            {
                await foreach (var chatUpdate in client.GetStreamingResponseAsync(chatMessages, option))
                {
                    var usageDetails = chatUpdate.Contents.OfType<UsageContent>()
                        .FirstOrDefault()?.Details;
                    if (usageDetails != null)
                    {
                        usage = usageDetails;
                    }

                    if (chatUpdate.RawRepresentation is OpenAI.Chat.StreamingChatCompletionUpdate openAIUpdate)
                    {
                        msg += _responseProcessor.AppendContentToMessage(openAIUpdate.ContentUpdate);
                        reasoning += _responseProcessor.GetReasoningContent(openAIUpdate);
                    }
                    else
                    {
                        foreach (var content in chatUpdate.Contents)
                        {
                            if (content is TextContent text)
                            {
                                msg += text.Text;
                            }
                            else if (content is DataContent dataContent)
                            {
                                try
                                {
                                    Directory.CreateDirectory(Path.Combine(MainSave.ImageDirectory, "ChatGPT"));
                                    string filePath = Path.Combine(MainSave.ImageDirectory, "ChatGPT", $"{Guid.NewGuid()}.jpg");
                                    File.WriteAllBytes(filePath, dataContent.Data.ToArray());
                                    msg += $"[CQ:image,file=ChatGPT\\{Path.GetFileName(filePath)}]";
                                }
                                catch (Exception ex)
                                {
                                    MainSave.CQLog?.Warning("图片处理", $"保存图片失败: {ex.Message}");
                                    msg += "[图片处理失败]";
                                }
                            }
                        }
                        if (chatUpdate.FinishReason == ChatFinishReason.ToolCalls
                               && AppConfig.EnableMCP
                               && !string.IsNullOrEmpty(msg))
                        {
                            CommonHelper.DebugLog("Tool_消息切片", msg);
                            Chat.TriggerOnToolCall(identity, msg);
                            msg = "";
                            if (_toolCallService.ShouldAbortConversation(identity))
                            {
                                MainSave.CQLog?.Info("发起对话", $"由于 ToolCall 次数超限，会话强制终止");
                                break;
                            }
                        }
                    }
                }
            }).Wait();

            return (msg, usage);
        }

        private (string msg, UsageDetails? usage) ProcessNonStreamingResponse(IChatClient client, List<ChatMessage> chatMessages, ChatOptions option)
        {
            string msg = "";
            UsageDetails? usage = null;

            Task.Run(async () =>
            {
                var response = await client.GetResponseAsync(chatMessages, option);
                if (response.RawRepresentation is OpenAI.Chat.StreamingChatCompletionUpdate openAIUpdate)
                {
                    msg += _responseProcessor.AppendContentToMessage(openAIUpdate.ContentUpdate);
                    msg += _responseProcessor.GetReasoningContent(openAIUpdate);
                }
                else
                {
                    msg += response.Text;
                }
                usage = response.Usage;
            }).Wait();

            return (msg, usage);
        }

        private string HandlePendingPictures(string baseUrl, string apiKey, LLMModel model, List<ChatMessage> chatMessages, Chat.Purpose purpose, bool jsonMode, int timeout, MCPClientManager mcp, string identity, string currentMsg)
        {
            var pendingPictures = PictureContextManager.GetAndClearPictures(identity);
            if (pendingPictures != null && pendingPictures.Count > 0)
            {
                bool success = false;
                foreach (var hash in pendingPictures)
                {
                    if (Picture.Cache.TryGetValue(hash, out var picture) && picture != null)
                    {
                        bool absolute = File.Exists(picture.FilePath);
                        bool relative = File.Exists(Path.Combine(MainSave.ImageDirectory, picture.FilePath));
                        if (absolute || relative)
                        {
                            // TODO: 调查为什么失败
                            MainSave.CQLog.Info("获取表情包", $"表情包获取成功，为 {picture.FilePath}");
                            try
                            {
                                MainSave.CQLog?.Info("附加图片", $"向对话 {identity} 附加图片 {hash}，路径 {picture.FilePath}");
                                var imageData = File.ReadAllBytes(absolute ? CommonHelper.GetRelativePath(picture.FilePath, MainSave.ImageDirectory) : picture.FilePath);
                                if (imageData.Length > 0)
                                {
                                    chatMessages.Add(new(ChatRole.User, [new DataContent(imageData, "image/jpg")]));
                                    success = true;
                                }
                                else
                                {
                                    MainSave.CQLog?.Warning("附加图片", $"图片文件为空: {picture.FilePath}");
                                }
                            }
                            catch (Exception ex)
                            {
                                MainSave.CQLog?.Warning("附加图片", $"读取图片失败: {ex.Message}");
                            }
                        }
                        else
                        {
                            MainSave.CQLog?.Warning("附加图片", $"未能找到图片路径: {picture.FilePath}");
                        }
                    }
                }
                if (!success)
                {
                    MainSave.CQLog?.Info("附加图片", $"向对话 {identity} 附加图片失败，可能是由于图片不存在");
                }
                else
                {
                    return GetChatResult(baseUrl, apiKey, model, chatMessages, purpose, jsonMode, timeout, mcp, identity);
                }
            }
            return currentMsg;
        }
    }
}