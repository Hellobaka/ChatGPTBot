using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;
using System.IO;

namespace me.cqp.luohuaming.ChatGPT.Code.OrderFunctions
{
    public class AddPrompt : IOrderModel
    {
        public bool ImplementFlag { get; set; } = true;

        public int Priority { get; set; } = 100;

        public string GetOrderStr()
        {
            return AppConfig.AddPromptOrder;
        }

        public bool Judge(string destStr)
        {
            return destStr.Replace("＃", "#").StartsWith(GetOrderStr());
        }

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

            string message = e.Message.Text.Replace(GetOrderStr(), "").Trim();
            string[] arg = message.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (arg.Length != 2)
            {
                sendText.MsgToSend.Add("指令参数不正确：触发词 预设文本");
                return result;
            }
            string key = arg[0];
            string prompt = arg[1];
            if (MainSave.Prompts.ContainsKey(key))
            {
                sendText.MsgToSend.Add("添加失败，已存在该触发词");
                return result;
            }
            string path = Path.Combine(MainSave.AppDirectory, "Prompts");
            Directory.CreateDirectory(path);
            path = Path.Combine(path, $"{key}.txt");
            File.WriteAllText(path, prompt);
            MainSave.Prompts.Add(key, path);
            sendText.MsgToSend.Add("添加成功");

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

            string message = e.Message.Text.Replace(GetOrderStr(), "").Trim();
            string[] arg = message.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (arg.Length != 2)
            {
                sendText.MsgToSend.Add("指令参数不正确：触发词 预设文本");
                return result;
            }
            string key = arg[0];
            string prompt = arg[1];
            if (MainSave.Prompts.ContainsKey(key))
            {
                sendText.MsgToSend.Add("添加失败，已存在该触发词");
                return result;
            }
            string path = Path.Combine(MainSave.AppDirectory, "Prompts");
            Directory.CreateDirectory(path);
            path = Path.Combine(path, $"{key}.txt");
            File.WriteAllText(path, prompt);
            MainSave.Prompts.Add(key, path);
            sendText.MsgToSend.Add("添加成功");

            return result;
        }
    }
}
