using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;
using System.Linq;

namespace me.cqp.luohuaming.ChatGPT.Code.OrderFunctions
{
    public class ResetChat : IOrderModel
    {
        public bool ImplementFlag { get; set; } = true;

        public int Priority { get; set; } = 100;

        public string GetOrderStr() => AppConfig.ResetChatOrder;

        public bool Judge(string destStr) => destStr.Replace("＃", "#").StartsWith(GetOrderStr());

        public FunctionResult Progress(CQGroupMessageEventArgs e)
        {
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
            var chat = Chat.ChatFlows.FirstOrDefault(x => x.ParentId == e.FromGroup && x.IsGroup);
            if(chat != null)
            {
                chat.RemoveTimeout = 0;
                chat.Conversations.Clear();
                Record.RemoveRecords(e.FromGroup);
                sendText.MsgToSend.Add("已重置聊天");
                result.SendObject.Add(sendText);
                return result;
            }
            else
            {
                return new FunctionResult();
            }
        }

        public FunctionResult Progress(CQPrivateMessageEventArgs e)
        {
            FunctionResult result = new FunctionResult
            {
                Result = true,
                SendFlag = true,
            };
            SendText sendText = new SendText
            {
                SendID = e.FromQQ,
            };

            var chat = Chat.ChatFlows.First(x => x.Id == e.FromQQ && !x.IsGroup);
            if (chat != null)
            {
                chat.RemoveTimeout = 0;
                chat.Conversations.Clear();

                sendText.MsgToSend.Add("已重置聊天");
                result.SendObject.Add(sendText);
                return result;
            }
            else
            {
                return new FunctionResult();
            }
        }
    }
}