using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public class Chat
    {
        public static List<ChatFlow> ChatFlows { get; set; } = new List<ChatFlow>();

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
                result = "连接发生问题，查看日志排查问题";
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
            flow.Conversations.Add(new ChatFlow.ConversationItem
            {
                Role = "user",
                Content = CommonHelper.TextTemplateParse(question, qq)
            });

            var chatMessages = flow.BuildMessages();
            var msg = GetChatResult(chatMessages);
            msg = CommonHelper.TextTemplateParse(msg, isGroup ? groupId : qq);
            flow.Conversations.Add(new ChatFlow.ConversationItem
            {
                Role = "assistant",
                Content = msg
            });
            if (msg.StartsWith("Error"))
            {
                flow.Conversations.RemoveAt(flow.Conversations.Count - 1);
            }
            return msg;
        }

        public static string GetChatResult(List<ChatRecords> chatMessages)
        {
            ChatFlow messages = new();
            messages.Init();
            foreach(var item in chatMessages)
            {
                messages.Conversations.Add(new ChatFlow.ConversationItem
                {
                    Role = "user",
                    Content = AppConfig.AppendGroupNick ? ((MainSave.CQApi.GetGroupMemberInfo(item.GroupID, item.QQ)?.Card ?? "未获取到昵称") + $"[{item.QQ}]" + ": " + item.Message) : item.Message,
                });
            }
            return GetChatResult(messages.BuildMessages());
        }

        private static string GetChatResult(List<ChatMessage> chatMessages)
        {
            string msg = "";
            var c = new OpenAIClient(AppConfig.APIKey, new OpenAIClientOptions() { Endpoint = new(AppConfig.BaseURL), NetworkTimeout = TimeSpan.FromSeconds(300), });
            var client = c.GetChatClient(AppConfig.ModelName);
            try
            {
                if (AppConfig.StreamMode)
                {
                    foreach (StreamingChatCompletionUpdate chatUpdate in client.CompleteChatStreaming(chatMessages, options: new ChatCompletionOptions { MaxTokens = AppConfig.ChatMaxTokens, }))
                    {
                        foreach (ChatMessageContentPart contentPart in chatUpdate.ContentUpdate)
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
                    }
                }
                else
                {
                    msg += client.CompleteChat(chatMessages);
                }                
            }
            catch (Exception ex)
            {
                MainSave.CQLog.Info("OpenAI_ChatCompletions失败", ex.Message + ex.StackTrace);
                msg = "Error, 连接发生问题，查看日志排查问题";
            }
            return msg;
        }
    }
}