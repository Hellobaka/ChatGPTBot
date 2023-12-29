using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;

namespace me.cqp.luohuaming.ChatGPT.Code.OrderFunctions
{
    public class DisableChat : IOrderModel
    {
        public bool ImplementFlag { get; set; } = true;

        public string GetOrderStr() => AppConfig.DisableChatOrder;

        public bool Judge(string destStr) => destStr.Replace("＃", "#").StartsWith(GetOrderStr());//这里判断是否能触发指令

        public FunctionResult Progress(CQGroupMessageEventArgs e)//群聊处理
        {
            if (AppConfig.MasterQQ != e.FromQQ)
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

            AppConfig.GroupList.Remove(e.FromGroup);
            ConfigHelper.SetConfig("GroupList", AppConfig.GroupList);

            sendText.MsgToSend.Add("关闭成功");
            result.SendObject.Add(sendText);
            return result;
        }

        public FunctionResult Progress(CQPrivateMessageEventArgs e)//私聊处理
        {
            return new FunctionResult();
        }
    }
}
