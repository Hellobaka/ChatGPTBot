using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model;
using OpenAI;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public class Chat
    {
        public const string ErrorMessage = "连接发生问题，查看日志排查问题";

        public static List<ChatFlow> ChatFlows { get; set; } = new List<ChatFlow>();

        private static Regex ThinkBlockRegex { get; set; } = new Regex(@"<think>[\s\S]*?</think>");

        public static string GetChatResult(string question, long qq, long groupId, bool isGroup, out long ms)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            string result = "";
            try
            {
                result = CallChatGPT(question, qq, groupId, isGroup);
            }
            catch (Exception ex)
            {
                result = ErrorMessage;
                MainSave.CQLog.Info("调用ChatGPT", ex.Message + ex.StackTrace);
                if (ex.InnerException != null)
                {
                    MainSave.CQLog.Info("调用ChatGPT", ex.InnerException.Message + ex.InnerException.StackTrace);
                }
            }
            stopwatch.Stop();
            ms = stopwatch.ElapsedMilliseconds;
            return result;
        }

        /// <summary>
        /// ChatFlow管理，根据群/QQ进行上下文管理
        /// </summary>
        /// <param name="question"></param>
        /// <param name="qq"></param>
        /// <param name="groupId"></param>
        /// <param name="isGroup"></param>
        /// <returns></returns>
        private static string CallChatGPT(string question, long qq, long groupId, bool isGroup)
        {
            if (isGroup && AppConfig.AppendGroupNick)
            {
                question = (MainSave.CQApi.GetGroupMemberInfo(groupId, qq)?.Card ?? "未获取到昵称") + $"[{qq}]" + ": " + question;
            }
            ChatFlow flow = ChatFlows.FirstOrDefault(x => isGroup ? x.ParentId == groupId : x.Id == qq);
            if (flow == null)
            {
                flow = new()
                {
                    Id = qq,
                    ParentId = groupId,
                    IsGroup = isGroup,
                };
                flow.Init();
                ChatFlows.Add(flow);
            }
            flow.RemoveTimeout = 0;
            // 兼容deepseek不能连续发送user会话的问题
            if (flow.Conversations.Count > 0 && flow.Conversations.Last().Role == "user")
            {
                flow.Conversations.Last().Content = CommonHelper.TextTemplateParse(question, qq);
            }
            else
            {
                flow.Conversations.Add(new ChatFlow.ConversationItem
                {
                    Role = "user",
                    Content = CommonHelper.TextTemplateParse(question, qq)
                });
            }

            var chatMessages = flow.BuildMessages();
            var msg = GetChatResult(chatMessages, AppConfig.ModelName);
            msg = CommonHelper.TextTemplateParse(msg, isGroup ? groupId : qq);
            if (AppConfig.RemoveThinkBlock)
            {
                msg = ThinkBlockRegex.Replace(msg, "");
                while (msg.StartsWith("\n") || msg.StartsWith("\r") || msg.StartsWith(" "))
                {
                    msg = msg.Remove(0, 1);
                }
            }
            flow.Conversations.Add(new ChatFlow.ConversationItem
            {
                Role = "assistant",
                Content = msg
            });
            if (msg == ErrorMessage)
            {
                flow.Conversations.RemoveAt(flow.Conversations.Count - 1);
            }
            return msg;
        }

        /// <summary>
        /// 使用当前的一组ChatFlow进行对话
        /// </summary>
        /// <param name="chatFlow"></param>
        /// <returns></returns>
        public static string GetChatResult(ChatFlow chatFlow)
        {
            return GetChatResult(chatFlow.BuildMessages(), AppConfig.ModelName);
        }

        /// <summary>
        /// 使用一组消息记录开始对话
        /// <returns></returns>
        public static string GetChatResult(List<ChatRecords> chatMessages, string modelName, string prompt)
        {
            ChatFlow messages = new();
            prompt = CommonHelper.TextTemplateParse(prompt, chatMessages.First().GroupID);
            messages.Conversations.Insert(0, new()
            {
                Content = prompt,
                Role = "system"
            });
            foreach (var item in chatMessages)
            {
                messages.Conversations.Add(new ChatFlow.ConversationItem
                {
                    Role = "user",
                    Content = AppConfig.AppendGroupNick ? ((MainSave.CQApi.GetGroupMemberInfo(item.GroupID, item.QQ)?.Card ?? "未获取到昵称") + $"[{item.QQ}]" + ": " + item.Message) : item.Message,
                });
            }
            return GetChatResult(messages.BuildMessages(), modelName);
        }

        /// <summary>
        /// 最底层对话调用方法
        /// </summary>
        /// <param name="chatMessages"></param>
        /// <param name="modelName"></param>
        /// <returns></returns>
        public static string GetChatResult(List<ChatMessage> chatMessages, string modelName, bool useSearch = false)
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
            var c = new OpenAIClient(new ApiKeyCredential(AppConfig.APIKey), new OpenAIClientOptions() { Endpoint = new(AppConfig.BaseURL), NetworkTimeout = TimeSpan.FromSeconds(300), });
            var client = c.GetChatClient(modelName);
            var option = new ChatCompletionOptions
            {
                MaxOutputTokenCount = AppConfig.ChatMaxTokens,
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
                    if (AppConfig.StreamMode)
                    {
                        foreach (StreamingChatCompletionUpdate chatUpdate in client.CompleteChatStreaming(chatMessages, option))
                        {
                            msg += AppendContentToMessage(chatUpdate.ContentUpdate);
                            // TODO: tool stream update
                            finishReason = chatUpdate.FinishReason ?? ChatFinishReason.Stop;
                        }
                    }
                    else
                    {
                        var completion = client.CompleteChat(chatMessages, option);
                        msg += AppendContentToMessage(completion.Value.Content);
                        toolcall = [.. toolcall, .. completion.Value.ToolCalls];
                        finishReason = completion.Value.FinishReason;
                    }

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