using Azure.AI.OpenAI;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.Model
{
    public class ChatFlow
    {
        public ChatFlow()
        {
            RemoveTimeout = 0;
        }

        public int RemoveTimeout { get; set; }

        public long ParentId { get; set; }

        public long Id { get; set; }

        public bool IsGroup { get; set; }

        public bool ContinuedMode { get; set; }

        public List<ConversationItem> Conversations { get; set; } = new List<ConversationItem>();

        public class ConversationItem
        {
            public string Role { get; set; }

            public string Content { get; set; }

            public ChatMessage Build()
            {
                if (Role == "assistant")
                {
                    return new AssistantChatMessage(Content);
                }
                if (Role == "Prompt")
                {
                    return new SystemChatMessage(Content);
                }
                var cqCodes = CQCode.Parse(Content);
                List<ChatMessageContentPart> items = new()
                {
                    ChatMessageContentPart.CreateTextMessageContentPart(Regex.Replace(Content, "\\[CQ:.*?\\]", ""))
                };
                foreach (var item in cqCodes)
                {
                    if (item.Function == Sdk.Cqp.Enum.CQFunction.Image)
                    {
                        Directory.CreateDirectory(Path.Combine(MainSave.ImageDirectory, "ChatGPT"));
                        string filePath = Path.Combine(MainSave.ImageDirectory, "ChatGPT", $"{Guid.NewGuid()}.jpg");
                        try
                        {
                            if (item.Items.TryGetValue("file", out string fileName))
                            {
                                string path = MainSave.CQApi.ReceiveImage(item);
                                File.Move(path, filePath);
                                if (File.Exists(filePath))
                                {
                                    items.Add(ChatMessageContentPart.CreateImageMessageContentPart(BinaryData.FromBytes(File.ReadAllBytes(filePath)), "image/jpg"));
                                    MainSave.CQLog.Debug("视觉", "向消息序列中添加了图片元素");
                                }
                                else
                                {
                                    MainSave.CQLog.Info("视觉", "指定的图片不存在，无法发送");
                                }
                            }
                            else
                            {
                                MainSave.CQLog.Info("视觉", "指定的图片不存在，无法发送");
                            }
                        }
                        catch (Exception e)
                        {
                            MainSave.CQLog.Warning("保存图片", $"ReceiveImage: {e.Message}\r\n{e.StackTrace}");
                        }
                    }
                }
                UserChatMessage message = new(items);
                return message;
            }
        }

        public void Init()
        {
            string systemHint = IsGroup ? AppConfig.GroupPrompt : AppConfig.PrivatePrompt;
            systemHint = CommonHelper.TextTemplateParse(systemHint, IsGroup ? ParentId : Id);
            Conversations.Insert(0, new()
            {
                Content = systemHint,
                Role = "system"
            });
        }

        public List<ChatMessage> BuildMessages()
        {
            List<ChatMessage> messages = new();
            foreach (var item in Conversations)
            {
                messages.Add(item.Build());
            }
            return messages;
        }

        public void RemoveFromFlows()
        {
            Chat.ChatFlows.Remove(this);
            MainSave.CQLog.Debug("超时移除", "达到预定的10分钟超时时长");
        }
    }
}