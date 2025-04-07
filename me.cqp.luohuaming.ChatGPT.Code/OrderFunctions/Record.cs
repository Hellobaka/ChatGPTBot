using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Timers;

namespace me.cqp.luohuaming.ChatGPT.Code.OrderFunctions
{
    public class Record : IOrderModel
    {
        public bool ImplementFlag { get; set; } = true;

        public int Priority { get; set; } = 1;

        public string GetOrderStr() => "";

        public bool Judge(string destStr) => true;

        public static List<(long groupId, DateTime lastReplyTime)> Cooldowns { get; set; } = [];

        public static List<ChatRecords> Records { get; set; } = [];

        private static Timer CleanRecordTimer { get; set; }

        private bool InProgress { get; set; }

        public FunctionResult Progress(CQGroupMessageEventArgs e)
        {
            StartCleanRecord();
            FunctionResult result = new()
            {
                Result = false,
                SendFlag = false,
            };

            if (!AppConfig.EnableVision && CQCode.Parse(e.Message).Any(x => x.IsImageCQCode))
            {
                return result;
            }
            Records.Add(new ChatRecords
            {
                MessageId = e.Message.Id,
                GroupID = e.FromGroup,
                QQ = e.FromQQ,
                Message = e.Message.Text,
                ReceiveTime = DateTime.Now,
            });

            if (AppConfig.GroupList.Contains(e.FromGroup) is false || !AppConfig.RandomReply)
            {
                return new FunctionResult();
            }

            if (InProgress)
            {
                return new FunctionResult { Result = false, SendFlag = false };
            }
            var search = Records.Where(x => x.GroupID == e.FromGroup && !x.Used).ToList();
            for (int i = 0; i < search.Count; i++)
            {
                var item = search[i];
                if ((DateTime.Now - item.ReceiveTime).TotalMinutes > AppConfig.RandomReplyMinuteInterval)
                {
                    item.Used = true;
                }
            }
            bool group = Records.Count(x => x.GroupID == e.FromGroup && !x.Used) >= AppConfig.RandomReplyConversationCount;
            bool person = Records.Count(x => x.GroupID == e.FromGroup && x.QQ == e.FromQQ && !x.Used) >= AppConfig.RandomReplyPersonalConversationCount;
            try
            {
                if (group || person)
                {
                    InProgress = true;
                    var filterRecords = Records.Where(x => group ? x.GroupID == e.FromGroup : x.GroupID == e.FromGroup && x.QQ == e.FromQQ).ToList();
                    MainSave.CQLog.Info("随机聊天", $"{(group ? "群组" : "个人")}单位时间内聊天次数达到设置，触发功能");
                    RemoveRecords(e.FromGroup);

                    var index = Cooldowns.FindIndex(x => x.groupId == e.FromGroup);
                    if (index >= 0)
                    {
                        var cd = Cooldowns[index];
                        if ((DateTime.Now - cd.lastReplyTime) < TimeSpan.FromMilliseconds(AppConfig.RandomReplyCoolDown))
                        {
                            MainSave.CQLog.Info("随机聊天", $"{e.FromGroup} 冷却中，下次可用时间：{(cd.lastReplyTime + TimeSpan.FromMilliseconds(AppConfig.RandomReplyCoolDown)):G}");
                            return result;
                        }
                        else
                        {
                            Cooldowns[index] = (e.FromGroup, DateTime.Now);
                        }
                    }
                    else 
                    {
                        Cooldowns.Add((e.FromGroup, DateTime.Now));
                    }

                    result.Result = true;
                    result.SendFlag = true;
                    SendText sendText = new()
                    {
                        SendID = e.FromGroup,
                        Reply = true
                    };
                    Stopwatch sw = Stopwatch.StartNew();
                    string gptResult = Chat.GetChatResult(filterRecords, AppConfig.ModelName, AppConfig.GroupPrompt);
                    sw.Stop();
                    if (gptResult == Chat.ErrorMessage)
                    {
                        return new();
                    }
                    double ms = sw.ElapsedMilliseconds;

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
                            RecordSelfMessage(e.FromGroup, e.FromGroup.SendGroupMessage(CQApi.CQCode_Record(@$"ChatGPT-TTS\{fileName}").ToSendString()));
                        }
                        else if (AppConfig.SendErrorTextWhenTTSFail)
                        {
                            sendText.MsgToSend.Add("语音合成失败");
                        }
                    }
                    else
                    {
                        if (AppConfig.EnableSpliter)
                        {
                            var lines = new Spliter(gptResult).Split();
                            foreach (var line in lines.Where(x => !string.IsNullOrWhiteSpace(x)))
                            {
                                if (AppConfig.EnableSpliterRandomDelay)
                                {
                                    double typeSpeed = AppConfig.SpliterSimulateTypeSpeed / 60;
                                    double typeTime = line.Length * typeSpeed;
                                    int randomSleep = MainSave.Random.Next(AppConfig.SpliterRandomDelayMin, AppConfig.SpliterRandomDelayMax);
                                    System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(typeTime + randomSleep));
                                }
                                RecordSelfMessage(e.FromGroup, e.FromGroup.SendGroupMessage(line));
                            }
                        }
                        else
                        {
                            sendText.MsgToSend.Add(gptResult + (AppConfig.AppendExecuteTime ? $"({ms / 1000.0:f2}s)" : ""));
                        }
                    }
                    result.SendObject.Add(sendText);
                }
                return result;
            }
            catch (Exception ex)
            {
                e.CQLog.Warning("随机回复", $"方法发生异常：{ex.Message}\n{ex.StackTrace}");
                return new FunctionResult { Result = false, SendFlag = false };
            }
            finally
            {
                InProgress = false;
            }
        }

        private void StartCleanRecord()
        {
            if (CleanRecordTimer == null)
            {
                CleanRecordTimer = new Timer();
                CleanRecordTimer.Interval = TimeSpan.FromDays(0.5).TotalMilliseconds;
                CleanRecordTimer.Elapsed += (_, _) =>
                {
                    var search = Records.Where(x => x.ReceiveTime.Date != DateTime.Now.Date).ToList();
                    for (int i = 0; i < search.Count; i++)
                    {
                        Records.Remove(search[i]);
                    }
                };
                CleanRecordTimer.Start();
            }
        }

        public FunctionResult Progress(CQPrivateMessageEventArgs e)
        {
            return new FunctionResult();
        }

        public static void RemoveRecords(long groupId)
        {
            var search = Records.Where(x => x.GroupID == groupId && !x.Used).ToList();
            for (int i = 0; i < search.Count; i++)
            {
                var item = search[i];
                if (item.GroupID == groupId)
                {
                    item.Used = true;
                }
            }
        }

        public static string? GetMessageContentById(int messageId)
        {
            var search = Records.FirstOrDefault(x => x.MessageId == messageId);
            if (search == null)
            {
                return null;
            }
            return search.Message;
        }

        public static void RecordSelfMessage(long group, QQMessage msg)
        {
            Records.Add(new ChatRecords
            {
                GroupID = group,
                Message = msg.Text,
                MessageId = msg.Id,
                QQ = MainSave.CurrentQQ,
                ReceiveTime = DateTime.Now,
                Used = false
            });
        }
    }
}