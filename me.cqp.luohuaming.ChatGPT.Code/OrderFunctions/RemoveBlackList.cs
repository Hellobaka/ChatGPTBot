using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;
using me.cqp.luohuaming.ChatGPT.PublicInfos;

namespace me.cqp.luohuaming.ChatGPT.Code.OrderFunctions
{
    public class RemoveBlackList : IOrderModel
    {
        public bool ImplementFlag { get; set; } = true;

        public int Priority { get; set; } = 100;

        public string GetOrderStr() => AppConfig.RemoveBlackListOrder;

        public bool Judge(string destStr) => destStr.Replace("＃", "#").StartsWith(GetOrderStr());

        public FunctionResult Progress(CQGroupMessageEventArgs e)
        {
            return new FunctionResult
            {
                Result = false,
                SendFlag = false
            };
        }

        public FunctionResult Progress(CQPrivateMessageEventArgs e)
        {
            if (e.FromQQ != AppConfig.MasterQQ)
            {
                return new FunctionResult
                {
                    Result = false,
                    SendFlag = false
                };
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

            string[] cmd = e.Message.Text.Split(' ');
            if (cmd.Length != 2 || long.TryParse(cmd[1], out long qq) is false)
            {
                sendText.MsgToSend.Add($"格式不正确: {AppConfig.RemoveBlackListOrder} QQ");
                return result;
            }
            if (!AppConfig.BlackList.Contains(qq))
            {
                sendText.MsgToSend.Add($"目标不存在于黑名单中，目前数量: {AppConfig.BlackList.Count}");
                return result;
            }
            AppConfig.BlackList.Remove(qq);
            ConfigHelper.SetConfig("BlackList", AppConfig.BlackList);
            sendText.MsgToSend.Add($"移除成功，目前黑名单数量: {AppConfig.BlackList.Count}");
            return result;
        }
    }
}
