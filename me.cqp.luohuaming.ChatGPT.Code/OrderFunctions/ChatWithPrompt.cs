using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;
using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;

namespace me.cqp.luohuaming.ChatGPT.Code.OrderFunctions
{
    public class ChatWithPrompt : IOrderModel
    {
        public bool ImplementFlag { get; set; } = true;

        public int Priority { get; set; } = 100;

        public string GetOrderStr() => AppConfig.ChatPromptOrder;

        public bool Judge(string destStr) => destStr.Replace("＃", "#").StartsWith(GetOrderStr());

        public FunctionResult Progress(CQGroupMessageEventArgs e)
        {
            if (AppConfig.GroupList.Contains(e.FromGroup) is false)
            {
                return new FunctionResult();
            }
            FunctionResult result = new FunctionResult
            {
                Result = true,
                SendFlag = true,
            };
            SendText sendText = new SendText
            {
                SendID = e.FromGroup,
                Reply = true
            };
            result.SendObject.Add(sendText);

            var flow = Chat.ChatFlows.FirstOrDefault(x => x.QQ == e.FromQQ);
            flow?.RemoveFromFlows();

            string key = e.Message.Text.Replace(GetOrderStr(), "").Trim();
            AddChatFlowWithPrompt(e.FromQQ, sendText, key);

            return result;
        }

        public FunctionResult Progress(CQPrivateMessageEventArgs e)//私聊处理
        {
            if (AppConfig.PersonList.Contains(e.FromQQ) is false)
            {
                return new FunctionResult();
            }
            FunctionResult result = new FunctionResult
            {
                Result = true,
                SendFlag = true,
            };
            SendText sendText = new SendText
            {
                SendID = e.FromQQ,
            };
            result.SendObject.Add(sendText);

            var flow = Chat.ChatFlows.FirstOrDefault(x => x.QQ == e.FromQQ);
            flow?.RemoveFromFlows();

            string key = e.Message.Text.Replace(GetOrderStr(), "").Trim();
            AddChatFlowWithPrompt(e.FromQQ, sendText, key);
            return result;
        }

        private static void AddChatFlowWithPrompt(long qq, SendText sendText, string key)
        {
            if (MainSave.Prompts.ContainsKey(key))
            {
                string filePath = MainSave.Prompts[key];
                if (System.IO.File.Exists(filePath))
                {
                    string prompt = System.IO.File.ReadAllText(filePath);
                    ChatFlow chatFlow = new ChatFlow()
                    {
                        QQ = qq
                    };
                    chatFlow.Conversations.Add(new ChatFlow.ConversationItem()
                    {
                        ContainImage = false,
                        Role = "Prompt",
                        Content = prompt
                    });
                    Chat.ChatFlows.Add(chatFlow);
                    sendText.MsgToSend.Add($"预设 {key} 已启用，请继续聊天");
                }
                else
                {
                    sendText.MsgToSend.Add("预设文本不存在");
                }
            }
            else
            {
                sendText.MsgToSend.Add($"不存在该触发词，请使用 {AppConfig.ListPromptOrder} 指令查询现有预设");
            }
        }
    }
}
