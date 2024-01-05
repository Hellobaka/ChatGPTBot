using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;
using System.Linq;

namespace me.cqp.luohuaming.ChatGPT.Code.OrderFunctions
{
    public class ContinueMode : IOrderModel
    {
        public bool ImplementFlag { get; set; } = true;

        public int Priority { get; set; } = 100;

        public string GetOrderStr() => AppConfig.ContinueModeOrder;

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
            ChatFlow flow = Chat.ChatFlows.FirstOrDefault(x => x.QQ == e.FromQQ);
            if (flow != null)
            {
                flow.ContinuedMode = !flow.ContinuedMode;
            }
            else
            {
                flow = new ChatFlow
                {
                    ContinuedMode = true,
                    QQ = e.FromQQ,
                };
                Chat.ChatFlows.Add(flow);
            }
            sendText.MsgToSend.Add($"已{(flow.ContinuedMode ? "开启" : "关闭")}连续聊天");
            result.SendObject.Add(sendText);
            return result;
        }

        public FunctionResult Progress(CQPrivateMessageEventArgs e)
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
            ChatFlow flow = Chat.ChatFlows.FirstOrDefault(x => x.QQ == e.FromQQ);
            if (flow != null)
            {
                flow.ContinuedMode = !flow.ContinuedMode;
            }
            else
            {
                flow = new ChatFlow
                {
                    ContinuedMode = true,
                    QQ = e.FromQQ,
                };
                Chat.ChatFlows.Add(flow);
            }
            sendText.MsgToSend.Add($"已{(flow.ContinuedMode ? "开启" : "关闭")}连续聊天");
            result.SendObject.Add(sendText);
            return result;
        }
    }
}