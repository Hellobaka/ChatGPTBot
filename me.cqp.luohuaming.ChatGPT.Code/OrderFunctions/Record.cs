using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace me.cqp.luohuaming.ChatGPT.Code.OrderFunctions
{
    public class Record : IOrderModel
    {
        public bool ImplementFlag { get; set; } = true;

        public int Priority { get; set; } = 1;

        public string GetOrderStr() => "";

        public bool Judge(string destStr) => true;

        private static List<ChatRecords> Records { get; set; } = [];

        public FunctionResult Progress(CQGroupMessageEventArgs e)
        {
            if (AppConfig.GroupList.Contains(e.FromGroup) is false || !AppConfig.RandomReply)
            {
                return new FunctionResult();
            }

            FunctionResult result = new()
            {
                Result = false,
                SendFlag = false,
            };

            Records.Add(new ChatRecords
            {
                GroupID = e.FromGroup,
                QQ = e.FromQQ,
                Message = e.Message,
                ReceiveTime = DateTime.Now,
            });
            var search = Records.Where(x => x.GroupID == e.FromGroup).ToList();
            for (int i = 0; i < search.Count; i++)
            {
                var item = search[i];
                if ((item.ReceiveTime - DateTime.Now).TotalMinutes > AppConfig.RandomReplyMinuteInterval)
                {
                    Records.Remove(item);
                }
            }

            if (Records.Count(x => x.GroupID == e.FromGroup) >= AppConfig.RandomReplyConversationCount)
            {
                MainSave.CQLog.Info("�������", "Ⱥ�鵥λʱ������������ﵽ���ã���������");
                result.Result = true;
                result.SendFlag = true;
                SendText sendText = new()
                {
                    SendID = e.FromGroup,
                    Reply = true
                };
                Stopwatch sw = Stopwatch.StartNew();
                string gptResult = Chat.GetChatResult(Records.Where(x => x.GroupID == e.FromGroup).ToList());
                sw.Stop();
                double ms = sw.ElapsedMilliseconds;

                if (TTSHelper.Enabled)
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
                        e.FromGroup.SendGroupMessage(CQApi.CQCode_Record(@$"ChatGPT-TTS\{fileName}").ToSendString());
                    }
                    else if (AppConfig.SendErrorTextWhenTTSFail)
                    {
                        sendText.MsgToSend.Add("�����ϳ�ʧ��");
                    }
                }
                else
                {
                    sendText.MsgToSend.Add(gptResult + (AppConfig.AppendExecuteTime ? $"({ms / 1000.0:f2}s)" : ""));
                }
                RemoveRecords(e.FromGroup);
                result.SendObject.Add(sendText);
            }
            return result;
        }

        public FunctionResult Progress(CQPrivateMessageEventArgs e)
        {
            return new FunctionResult();
        }

        public static void RemoveRecords(long groupId)
        {
            var search = Records.Where(x => x.GroupID == groupId).ToList();
            for (int i = 0; i < search.Count; i++)
            {
                var item = search[i];
                if (item.GroupID == groupId)
                {
                    Records.Remove(item);
                }
            }
        }
    }
}
