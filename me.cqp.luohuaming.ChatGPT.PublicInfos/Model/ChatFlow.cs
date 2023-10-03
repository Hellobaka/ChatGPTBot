using Azure.AI.OpenAI;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using System.Collections.Generic;
using System.Threading;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.Model
{
    public class ChatFlow
    {
        public ChatFlow()
        {
            RemoveTimeout = 0;
            new Thread(() =>
            {
                while (RemoveTimeout < 10 * 60)
                {
                    RemoveTimeout++;
                    Thread.Sleep(1000);
                }
                RemoveFromFlows();
            }).Start();
        }

        public int RemoveTimeout { get; set; }

        public long QQ { get; set; }

        public bool ContinuedMode { get; set; }

        public List<ConversationItem> Conversations { get; set; } = new List<ConversationItem>();

        public class ConversationItem
        {
            public string Role { get; set; }

            public string Content { get; set; }
        }

        public ChatCompletionsOptions BuildMessages()
        {
            var result = new ChatCompletionsOptions();
            foreach (var item in Conversations)
            {
                result.Messages.Add(new ChatMessage { Role = item.Role, Content = item.Content });
            }
            return result;
        }

        public void RemoveFromFlows()
        {
            Chat.ChatFlows.Remove(this);
            MainSave.CQLog.Debug("超时移除", "达到预定的10分钟超时时长");
        }
    }
}