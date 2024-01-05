using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;
using System;
using System.IO;

namespace me.cqp.luohuaming.ChatGPT.Code.OrderFunctions
{
    public class RemovePrompt : IOrderModel
    {
        public bool ImplementFlag { get; set; } = true;

        public int Priority { get; set; } = 100;

        public string GetOrderStr()
        {
            return AppConfig.RemovePromptOrder;
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

            string key = e.Message.Text.Replace(GetOrderStr(), "");
            if (MainSave.Prompts.ContainsKey(key))
            {
                string filePath = MainSave.Prompts[key];
                if (File.Exists(filePath))
                {
                    string removedDir = Path.Combine(MainSave.AppDirectory, "Prompt", "Removed");
                    Directory.CreateDirectory(removedDir);
                    File.Move(filePath, Path.Combine(removedDir, $"{filePath}_{DateTime.Now:yyyyMMddHHmmss}.txt"));
                }
                MainSave.Prompts.Remove(key);
                sendText.MsgToSend.Add("删除成功");
            }
            else
            {
                sendText.MsgToSend.Add("删除失败，不存在该触发词");
            }

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
            string key = e.Message.Text.Replace(GetOrderStr(), "");
            if (MainSave.Prompts.ContainsKey(key))
            {
                string filePath = MainSave.Prompts[key];
                if (File.Exists(filePath))
                {
                    string removedDir = Path.Combine(MainSave.AppDirectory, "Prompt", "Removed");
                    Directory.CreateDirectory(removedDir);
                    File.Move(filePath, Path.Combine(removedDir, $"{filePath}_{DateTime.Now:yyyyMMddHHmmss}.txt"));
                }
                MainSave.Prompts.Remove(key);
                sendText.MsgToSend.Add("删除成功");
            }
            else
            {
                sendText.MsgToSend.Add("删除失败，不存在该触发词");
            }
            result.SendObject.Add(sendText);
            return result;
        }
    }
}
