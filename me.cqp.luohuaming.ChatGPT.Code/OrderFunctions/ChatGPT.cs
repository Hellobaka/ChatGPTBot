using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;
using System;
using System.IO;
using System.Linq;

namespace me.cqp.luohuaming.ChatGPT.Code.OrderFunctions
{
    public class ChatGPT : IOrderModel
    {
        public bool ImplementFlag { get; set; } = true;

        public int Priority { get; set; } = 1;

        public string GetOrderStr() => AppConfig.ResponsePrefix;

        public bool Judge(string destStr) => true;

        public FunctionResult Progress(CQGroupMessageEventArgs e)
        {
            string message = e.Message.Text;
            if (AppConfig.GroupList.Contains(e.FromGroup) is false)
            {
                return new FunctionResult();
            }

            if (Chat.ChatFlows.Any(x => x.Id == e.FromQQ && x.ContinuedMode)
                || (message.Replace("＃", "#").StartsWith(GetOrderStr()))
                || (AppConfig.AtResponse && message.StartsWith(CQApi.CQCode_At(MainSave.CurrentQQ).ToString())))
            {
                e.FromGroup.SendGroupMessage(AppConfig.WelcomeText);
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

                string gptResult = Chat.GetChatResult(message, e.FromQQ, e.FromGroup, true, out long ms);
                if (TTSHelper.Enabled)
                {
                    string dir = Path.Combine(MainSave.RecordDirectory, "ChatGPT-TTS");
                    Directory.CreateDirectory(dir);
                    string fileName = $"{DateTime.Now:yyyyMMddHHmmss}.mp3";
                    if (AppConfig.SendTextBeforeTTS)
                    {
                        e.FromGroup.SendGroupMessage(gptResult + (AppConfig.AppendExecuteTime ? $"({ms / 1000.0:f2}s)" : ""));
                    }
                    if (TTSHelper.TTS(gptResult, Path.Combine(dir, fileName), AppConfig.TTSVoice))
                    {
                        sendText.MsgToSend.Add(CQApi.CQCode_Record(@$"ChatGPT-TTS\{fileName}").ToSendString());
                    }
                    else if (AppConfig.SendErrorTextWhenTTSFail)
                    {
                        sendText.MsgToSend.Add("语音合成失败");
                    }
                }
                else
                {
                    sendText.MsgToSend.Add(gptResult + (AppConfig.AppendExecuteTime ? $"({ms / 1000.0:f2}s)" : ""));
                }
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
            string message = e.Message.Text;
            if (AppConfig.PersonList.Contains(e.FromQQ) is false)
            {
                return new FunctionResult();
            }
            if (!Chat.ChatFlows.Any(x => x.Id == e.FromQQ && x.ContinuedMode)
                && !message.Replace("＃", "#").StartsWith(GetOrderStr()))
            {
                return new FunctionResult();
            }
            e.FromQQ.SendPrivateMessage(AppConfig.WelcomeText);
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
            string gptResult = Chat.GetChatResult(message, e.FromQQ, 0, false, out long ms);
            if (TTSHelper.Enabled)
            {
                string dir = Path.Combine(MainSave.RecordDirectory, "ChatGPT-TTS");
                Directory.CreateDirectory(dir);
                string fileName = $"{DateTime.Now:yyyyMMddHHmmss}.mp3";
                if (AppConfig.SendTextBeforeTTS)
                {
                    e.FromQQ.SendPrivateMessage(gptResult + (AppConfig.AppendExecuteTime ? $"({ms / 1000.0:f2}s)" : ""));
                }
                if (TTSHelper.TTS(gptResult, Path.Combine(dir, fileName), AppConfig.TTSVoice))
                {
                    sendText.MsgToSend.Add(CQApi.CQCode_Record(@$"ChatGPT-TTS\{fileName}").ToSendString());
                }
                else if (AppConfig.SendErrorTextWhenTTSFail)
                {
                    sendText.MsgToSend.Add("语音合成失败");
                }
            }
            else
            {
                sendText.MsgToSend.Add(gptResult + (AppConfig.AppendExecuteTime ? $"({ms / 1000.0:f2}s)" : ""));
            }
            result.SendObject.Add(sendText);
            return result;
        }
    }
}