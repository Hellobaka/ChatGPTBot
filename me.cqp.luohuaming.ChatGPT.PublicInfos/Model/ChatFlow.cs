using Azure.AI.OpenAI;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model;
using System;
using System.Collections.Generic;
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

            public ChatRequestMessage Build()
            {
                if (Role == "assistant")
                {
                    return new ChatRequestAssistantMessage(Content);
                }
                if (Role == "Prompt")
                {
                    return new ChatRequestSystemMessage(Content);
                }
                var cqCodes = CQCode.Parse(Content);
                List<ChatMessageContentItem> items = new();
                items.Add(new ChatMessageTextContentItem(Regex.Replace(Content, "\\[CQ:.*?\\]", "")));
                foreach (var item in cqCodes)
                {
                    if (item.Function == Sdk.Cqp.Enum.CQFunction.Image)
                    {
                        ContainImage = true;

                        #region 当SDK支持时 改为以下方式
                        //Directory.CreateDirectory(Path.Combine(MainSave.ImageDirectory, "ChatGPT"));
                        //string filePath = Path.Combine(MainSave.ImageDirectory, "ChatGPT", $"{Guid.NewGuid()}.jpg");
                        //try
                        //{
                        //    string path = MainSave.CQApi.ReceiveImage(item);
                        //    File.Move(path, filePath);
                        //    message.MultimodalContentItems.Add(new ChatMessageImageContentItem(new Uri($"data:image/jpeg;base64,{CommonHelper.ParsePic2Base64(filePath)}")));
                        //}
                        //catch(Exception e)
                        //{
                        //    MainSave.CQLog.Warning("保存图片", $"ReceiveImage: {e.Message}\r\n{e.StackTrace}");
                        //}
                        #endregion

                        var url = CommonHelper.GetImageURL(item);
                        if (string.IsNullOrEmpty(url) is false)
                        {
                            items.Add(new ChatMessageImageContentItem(new Uri(url)));
                        }
                    }
                }
                ChatRequestUserMessage message = new(items);
                return message;
            }

            public bool ContainImage { get; set; }
        }

        public void Init()
        {
            string systemHint = IsGroup ? AppConfig.GroupPrompt : AppConfig.PrivatePrompt;
            systemHint = CommonHelper.TextTemplateParse(systemHint, IsGroup ? ParentId : Id);
            Conversations.Insert(0, new()
            {
                ContainImage = false,
                Content = systemHint,
                Role = "Prompt"
            });
        }

        public ChatCompletionsOptions BuildMessages()
        {
            List<ChatRequestMessage> messages = new();
            foreach (var item in Conversations)
            {
                messages.Add(item.Build());
            }
            string model = Conversations.Any(x => x.ContainImage) && AppConfig.EnableVision ? "gpt-4-vision-preview" : AppConfig.ModelName;
            var completionsOptions = new ChatCompletionsOptions(model, messages)
            {
                MaxTokens = AppConfig.ChatMaxTokens,
            };
            return completionsOptions;
        }

        public void RemoveFromFlows()
        {
            Chat.ChatFlows.Remove(this);
            MainSave.CQLog.Debug("超时移除", "达到预定的10分钟超时时长");
        }
    }
}