using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace me.cqp.luohuaming.ChatGPT.Code.OrderFunctions
{
    public class ChatGPT : IOrderModel
    {
        public bool ImplementFlag { get; set; } = true;

        public int Priority { get; set; } = 1;

        public string GetOrderStr() => AppConfig.ResponsePrefix;

        public bool Judge(string destStr) => true;

        private Regex ReplyPattern { get; set; } = new Regex(Regex.Escape("[CQ:reply,id=") + @"(\d+)\]");

        private Regex AtPattern { get; set; } = new Regex("\\[CQ:at,qq=(\\d*).*?\\]");

        public FunctionResult Progress(CQGroupMessageEventArgs e)
        {
            string message = e.Message.Text;
            if (AppConfig.GroupList.Contains(e.FromGroup) is false)
            {
                return new FunctionResult();
            }

            if (Chat.ChatFlows.Any(x => x.Id == e.FromQQ && x.ContinuedMode)
                || (message.Replace("＃", "#").StartsWith(GetOrderStr()))
                || (AppConfig.AtResponse && ((AppConfig.AtAnyPosition && CheckAt(e.Message.Text, false)) || CheckAt(e.Message.Text, true)))
                || (AppConfig.ReplyResponse && message.Contains("[CQ:reply,id=") && CheckAt(e.Message.Text, false)))
            {
                if (!string.IsNullOrWhiteSpace(AppConfig.WelcomeText))
                {
                    Record.RecordSelfMessage(e.FromGroup, e.FromGroup.SendGroupMessage(AppConfig.WelcomeText));
                }
                message = message.Replace("＃", "#").Replace(GetOrderStr(), "");
                message = AtPattern.Replace(message, "");
                if (AppConfig.ReplyResponse)
                {
                    foreach (Match item in ReplyPattern.Matches(message))
                    {
                        if (int.TryParse(item.Groups[1].Value, out int msgId))
                        {
                            var msg = Record.GetMessageContentById(msgId);
                            if (msg != null)
                            {
                                message = message.Replace(item.Value, msg);
                            }
                        }
                    }
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

                string gptResult = Chat.GetChatResult(message, e.FromQQ, e.FromGroup, true, out long ms);
                if (TTSHelper.Enabled && !string.IsNullOrWhiteSpace(gptResult))
                {
                    string dir = Path.Combine(MainSave.RecordDirectory, "ChatGPT-TTS");
                    Directory.CreateDirectory(dir);
                    string fileName = $"{DateTime.Now:yyyyMMddHHmmss}.mp3";
                    if (AppConfig.SendTextBeforeTTS)
                    {
                        sendText.MsgToSend.Add(gptResult + (AppConfig.AppendExecuteTime ? $"({ms / 1000.0:f2}s)" : ""));
                    }
                    if (TTSHelper.TTS(gptResult, Path.Combine(dir, fileName), AppConfig.TTSVoice))
                    {
                        Record.RecordSelfMessage(e.FromGroup, e.FromGroup.SendGroupMessage(CQApi.CQCode_Record(@$"ChatGPT-TTS\{fileName}").ToSendString()));
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
            Record.RecordSelfMessage(0, e.FromQQ.SendPrivateMessage(AppConfig.WelcomeText));
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
            if (TTSHelper.Enabled && !string.IsNullOrWhiteSpace(gptResult))
            {
                string dir = Path.Combine(MainSave.RecordDirectory, "ChatGPT-TTS");
                Directory.CreateDirectory(dir);
                string fileName = $"{DateTime.Now:yyyyMMddHHmmss}.mp3";
                if (AppConfig.SendTextBeforeTTS)
                {
                    Record.RecordSelfMessage(0, e.FromQQ.SendPrivateMessage(gptResult + (AppConfig.AppendExecuteTime ? $"({ms / 1000.0:f2}s)" : "")));
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

        private bool CheckAt(string input, bool forceBegin)
        {
            // 要求CQ码必须在开头, 所以只检查原始文本开头是否为At CQ码即可
            if (forceBegin && input.StartsWith("[CQ:at"))
            {
                return false;
            }

            var cqcodes = CQCode.Parse(input);
            // 获取所有At CQ码
            var atCode = cqcodes.Where(x => x.Function == Sdk.Cqp.Enum.CQFunction.At);
            // 未查询到返回false
            if (atCode == null || atCode.Count() == 0)
            {
                return false;
            }
            // 强制开头检查 取第一个CQ码 检查QQ是否为本机QQ
            if (forceBegin)
            {
                return atCode.FirstOrDefault()?.Items["qq"] == MainSave.CurrentQQ.ToString();
            }

            // 非强制开头检查 检查第一个QQ为本机QQ是否存在
            return atCode.Any(x => x.Items["qq"] == MainSave.CurrentQQ.ToString());
        }
    }
}