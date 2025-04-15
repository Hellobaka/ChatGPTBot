using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using OpenAI;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

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
            获取心情
        }

        public const string ErrorMessage = "连接发生问题，查看日志排查问题";

        private static Regex ThinkBlockRegex { get; set; } = new Regex(@"<think>[\s\S]*?</think>");

        /// <summary>
        /// 最底层对话调用方法
        /// </summary>
        /// <param name="chatMessages"></param>
        /// <param name="modelName"></param>
        /// <returns></returns>
        public static string GetChatResult(string baseUrl, string apiKey, List<ChatMessage> chatMessages, string modelName, Purpose purpose, bool useSearch = false)
        {
            string AppendContentToMessage(ChatMessageContent contentes)
            {
                string msg = "";
                foreach (ChatMessageContentPart contentPart in contentes)
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

            string msg = "";
            var c = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions() { Endpoint = new(baseUrl), NetworkTimeout = TimeSpan.FromMilliseconds(AppConfig.ChatTimeout), });
            var client = c.GetChatClient(modelName);
            var option = new ChatCompletionOptions
            {
                MaxOutputTokenCount = AppConfig.ChatMaxTokens,
                Temperature = AppConfig.ChatTemperature
            };
            if (useSearch)
            {
                option.Tools.Add(ChatTool.CreateFunctionTool(nameof(BochaSearch.Search), "从近百亿网页和生态内容源中搜索高质量世界知识，例如新闻、图片、百科、文库等。", BinaryData.FromString(BochaSearch.ToolParameters), true));
            }
            try
            {
                bool requiresAction;
                do
                {
                    requiresAction = false;
                    List<ChatToolCall> toolcall = [];
                    ChatFinishReason finishReason = ChatFinishReason.Stop;
                    int inputToken = 0, outputToken = 0;
                    if (AppConfig.StreamMode)
                    {
                        foreach (StreamingChatCompletionUpdate chatUpdate in client.CompleteChatStreaming(chatMessages, option))
                        {
                            msg += AppendContentToMessage(chatUpdate.ContentUpdate);
                            // TODO: tool stream update
                            finishReason = chatUpdate.FinishReason ?? ChatFinishReason.Stop;
                            inputToken += (chatUpdate.Usage?.InputTokenCount ?? 0);
                            outputToken += (chatUpdate.Usage?.OutputTokenCount ?? 0);
                        }
                    }
                    else
                    {
                        var completion = client.CompleteChat(chatMessages, option);
                        msg += AppendContentToMessage(completion.Value.Content);
                        toolcall = [.. toolcall, .. completion.Value.ToolCalls];
                        finishReason = completion.Value.FinishReason;

                        inputToken = completion.Value.Usage.InputTokenCount;
                        outputToken = completion.Value.Usage.OutputTokenCount;
                    }
                    Usage.Insert(baseUrl, modelName, purpose.ToString(), inputToken, outputToken);

                    switch (finishReason)
                    {
                        case ChatFinishReason.Stop:
                            chatMessages.Add(new AssistantChatMessage(msg));
                            break;

                        case ChatFinishReason.ToolCalls:
                            chatMessages.Add(new AssistantChatMessage(toolcall));
                            foreach (var tool in toolcall)
                            {
                                switch (tool.FunctionName)
                                {
                                    case nameof(BochaSearch.Search):
                                        chatMessages.Add(new ToolChatMessage(tool.Id, BochaSearch.HandleToolCall(tool.FunctionArguments)));
                                        break;
                                }
                            }
                            requiresAction = true;
                            break;

                        case ChatFinishReason.ContentFilter:
                            MainSave.CQLog.Info("发起对话", "触发内容过滤，返回空回复");
                            return AppConfig.ChatEmptyResponse;
                    }
                } while (requiresAction);
            }
            catch (Exception ex)
            {
                MainSave.CQLog?.Info("OpenAI_ChatCompletions失败", ex.Message + ex.StackTrace);
                msg = ErrorMessage;
            }
            return msg;
        }
    }
}