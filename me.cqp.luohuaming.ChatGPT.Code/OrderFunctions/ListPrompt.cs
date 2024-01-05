using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;
using me.cqp.luohuaming.ChatGPT.PublicInfos;

namespace me.cqp.luohuaming.ChatGPT.Code.OrderFunctions
{
    public class ListPrompt : IOrderModel
    {
        public bool ImplementFlag { get; set; } = true;

        public int Priority { get; set; } = 100;

        public string GetOrderStr() => AppConfig.ListPromptOrder;

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
                Reply = true,
                SendID = e.FromGroup,
            };

            StringBuilder stringBuilder = new StringBuilder();
            int index = 1;
            foreach(var item in MainSave.Prompts)
            {
                stringBuilder.AppendLine($"{index}. {item.Key}");
                index++;
            }
            if(stringBuilder.Length == 0)
            {
                stringBuilder.AppendLine($"暂无预设，可使用 {AppConfig.AddPromptOrder} 指令添加预设");
            }
            stringBuilder.Remove(stringBuilder.Length - 2, 2);
            sendText.MsgToSend.Add(stringBuilder.ToString());
            result.SendObject.Add(sendText);
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

            StringBuilder stringBuilder = new StringBuilder();
            int index = 1;
            foreach (var item in MainSave.Prompts)
            {
                stringBuilder.AppendLine($"{index}. {item.Key}");
                index++;
            }
            stringBuilder.Remove(stringBuilder.Length - 2, 2);
            sendText.MsgToSend.Add(stringBuilder.ToString());
            result.SendObject.Add(sendText);
            return result;
        }
    }
}
