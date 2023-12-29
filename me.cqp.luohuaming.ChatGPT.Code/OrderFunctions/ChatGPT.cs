using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;
using System.Linq;

namespace me.cqp.luohuaming.ChatGPT.Code.OrderFunctions
{
    public class ChatGPT : IOrderModel
    {
        public bool ImplementFlag { get; set; } = true;

        public string GetOrderStr() => AppConfig.ResponsePrefix;

        public bool Judge(string destStr) => true;

        public FunctionResult Progress(CQGroupMessageEventArgs e)
        {
            string message = e.Message;
            if (AppConfig.GroupList.Contains(e.FromGroup) is false
                || (!message.Replace("＃", "#").StartsWith(GetOrderStr()) && !Chat.ChatFlows.Any(x => x.QQ == e.FromQQ && x.ContinuedMode))
                || (AppConfig.AtResponse && message.StartsWith(CQApi.CQCode_At(MainSave.CurrentQQ).ToString())))
            {
                return new FunctionResult();
            }
            message = message.Replace("＃", "#").Replace(GetOrderStr(), "").Replace(CQApi.CQCode_At(MainSave.CurrentQQ).ToString(), "");
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

            sendText.MsgToSend.Add(Chat.GetChatResult(message, e.FromQQ));
            result.SendObject.Add(sendText);
            return result;
        }

        public FunctionResult Progress(CQPrivateMessageEventArgs e)
        {
            string message = e.Message;
            if (AppConfig.PersonList.Contains(e.FromQQ) is false
                || !message.Replace("＃", "#").StartsWith(GetOrderStr())
                || !Chat.ChatFlows.Any(x => x.QQ == e.FromQQ && x.ContinuedMode))
            {
                return new FunctionResult();
            }
            message = message.Replace("＃", "#").Replace(GetOrderStr(), "");
            FunctionResult result = new FunctionResult
            {
                Result = true,
                SendFlag = true,
            };
            SendText sendText = new SendText
            {
                SendID = e.FromQQ,
            };

            sendText.MsgToSend.Add(Chat.GetChatResult(message, e.FromQQ));
            result.SendObject.Add(sendText);
            return result;
        }
    }
}