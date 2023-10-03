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
            if (!(message.Replace("＃", "#").StartsWith(GetOrderStr())
                || (AppConfig.AtResponse && message.StartsWith(CQApi.CQCode_At(MainSave.CurrentQQ).ToString()))))
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
            };

            sendText.MsgToSend.Add(Chat.GetChatResult(message, e.FromQQ));
            result.SendObject.Add(sendText);
            return result;
        }

        public FunctionResult Progress(CQPrivateMessageEventArgs e)
        {
            string message = e.Message;
            if (!(message.Replace("＃", "#").StartsWith(GetOrderStr())
                || (AppConfig.AtResponse && message.StartsWith(CQApi.CQCode_At(MainSave.CurrentQQ).ToString()))
                || Chat.ChatFlows.Any(x => x.QQ == e.FromQQ && x.ContinuedMode)))
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