using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;

namespace me.cqp.luohuaming.ChatGPT.Code.OrderFunctions
{
    public class EnableChat : IOrderModel
    {
        public bool ImplementFlag { get; set; } = true;

        public int Priority { get; set; } = 100;

        public string GetOrderStr() => AppConfig.EnableChatOrder;

        public bool Judge(string destStr) => destStr.Replace("＃", "#").StartsWith(GetOrderStr());

        public FunctionResult Progress(CQGroupMessageEventArgs e)
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

            if (AppConfig.GroupList.Contains(e.FromGroup) is false)
            {
                AppConfig.GroupList.Add(e.FromGroup);
                ConfigHelper.SetConfig("GroupList", AppConfig.GroupList);
            }

            sendText.MsgToSend.Add("开启成功");
            result.SendObject.Add(sendText);
            return result;
        }

        public FunctionResult Progress(CQPrivateMessageEventArgs e)
        {
            return new FunctionResult();
        }
    }
}