using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using Microsoft.Extensions.AI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.Model
{
    public class ReplyManager
    {
        public static Dictionary<long, ReplyManager> ReplyManagers { get; set; } = [];

        public bool HighReplyWilling { get; set; }

        public int MessageHoldCount { get; set; }

        public DateTime WillingLastChangeTime { get; set; } = DateTime.Now;

        public DateTime LastReplyTime { get; set; } = new DateTime();

        public long LastReplyQQ { get; set; }

        public bool ContextMode { get; set; }

        public double ReplyWilling { get; set; }

        public TimeSpan CurrentModeKeepInterval { get; set; }

        private Timer Timer { get; set; }

        private int TimerCount { get; set; } = 0;

        public ReplyManager(long id)
        {
            ReplyManagers.Add(id, this);
            EnableTimer();
        }

        public static ReplyManager GetReplyManager(long id)
        {
            if (ReplyManagers.TryGetValue(id, out ReplyManager replyManager))
            {
                return replyManager;
            }
            else
            {
                return new ReplyManager(id);
            }
        }

        public static (bool shouldResponse, double confidence) CheckShouldResponseByLLM(List<ChatRecord> chatRecords)
        {
            string prompt = $$"""
                你是一个群聊助手，你的昵称是:{{AppConfig.BotName}}，或者这些非常用称呼: {{string.Join(",", AppConfig.BotNicknames)}},{{AppConfig.GroupPrompt}}，需要判断当前是否应该回应最新消息。
                规则：
                - 如果用户明显在和你对话（延续你之前的话题、问你问题、提到你），应回应；
                - 如果话题已切换到与你无关的内容，不应回应；
                - 即使没被 @，只要上下文显示你在被“对话中”，就应回应；
                - 避免打扰：多人闲聊、表情包、玩笑话通常不应回应。
                最近对话（按时间倒序）：
                {{string.Join("\n", chatRecords.Skip(1).Select(x => x.ParsedMessage))}}

                最新消息：
                {{chatRecords.FirstOrDefault()?.ParsedMessage}}

                请仅输出 JSON：{"should_respond": true/false, "confidence": 0.0~1.0}
                """;

            var response = Chat.GetChatResult(AppConfig.SplitterApiKeyId, [
                    new(ChatRole.System, prompt),
                    new(ChatRole.User, "请回复")
                ], Chat.Purpose.回复意愿, timeout: AppConfig.SplitterTimeout);
            if (response == Chat.ErrorMessage)
            {
                MainSave.CQLog?.Error("回复意愿计算", "调用接口失败");
                return (true, 0);
            }
            response = response.Replace("`", "");
            try
            {
                var json = JObject.Parse(response);
                return (((bool)json["should_respond"]), ((double)json["confidence"]));
            }
            catch
            {
                MainSave.CQLog?.Error("回复意愿计算", $"Json解析失败：{response}");
                return (true, 0);
            }
        }

        private void EnableTimer()
        {
            Timer = new()
            {
                AutoReset = true,
                Interval = 5000
            };
            Timer.Elapsed += Timer_Elapsed;
            Timer.Start();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (TimerCount == int.MaxValue - 1)
            {
                TimerCount = 0;
            }
            TimerCount++;
            if (HighReplyWilling)
            {
                ReplyWilling = Math.Max(0.5, ReplyWilling * 0.95);
            }
            else
            {
                ReplyWilling = Math.Max(0, ReplyWilling * 0.8);
            }

            if (DateTime.Now - WillingLastChangeTime > CurrentModeKeepInterval
                || (!HighReplyWilling && CommonHelper.NextDouble() < 0.1))
            {
                if (HighReplyWilling)
                {
                    HighReplyWilling = false;
                    ReplyWilling = 0.01;
                    CurrentModeKeepInterval = TimeSpan.FromMinutes(CommonHelper.Next(10, 20));
                }
                else
                {
                    HighReplyWilling = true;
                    ReplyWilling = 1;
                    CurrentModeKeepInterval = TimeSpan.FromMinutes(CommonHelper.Next(3, 5));
                }
                WillingLastChangeTime = DateTime.Now;
                MessageHoldCount = 0;
            }

            if ((DateTime.Now - LastReplyTime).TotalMinutes >= 5)
            {
                ContextMode = false;
            }
            //CommonHelper.DebugLog("回复意愿定时更新", $"定时更新后的回复意愿为：{ReplyWilling}，会话模式：{ContextMode}，是否高回复模式：{HighReplyWilling}");
        }

        public double ChangeReplyWilling(bool emoji, bool at, bool contain, long qq)
        {
            CommonHelper.DebugLog("回复意愿更新", $"emoji={emoji}，at={at}，contain={contain}");

            MessageHoldCount++;
            if (qq == LastReplyQQ && (DateTime.Now - LastReplyTime).TotalMinutes < 2 && MessageHoldCount <= 5)
            {
                ContextMode = true;
                ReplyWilling += 0.3;
            }

            if (at)
            {
                ContextMode = true;
                LastReplyQQ = qq;
                return 1;
            }

            if (contain)
            {
                ContextMode = true;
                ReplyWilling += 0.8;
            }

            if (emoji)
            {
                ReplyWilling *= 0.1;
            }

            double baseProbability = 0;
            if (ContextMode)
            {
                baseProbability = HighReplyWilling ? 0.5 : 0.25;
            }
            else if (HighReplyWilling)
            {
                baseProbability = (MessageHoldCount >= 4 && MessageHoldCount <= 8) ? 0.5 : 0.2;
            }
            else
            {
                baseProbability = MessageHoldCount > 15 ? 0.3 : (0.03 * Math.Min(MessageHoldCount, 10));
            }

            ReplyWilling = Math.Min(3, Math.Max(0, ReplyWilling));
            LastReplyQQ = qq;

            CommonHelper.DebugLog("回复意愿更新", $"更新后的回复意愿为：{ReplyWilling}，回复倍率：{AppConfig.ReplyWillingAmplifier}，额外倍率：{baseProbability}，最终计算结果：{ReplyWilling * baseProbability * AppConfig.ReplyWillingAmplifier}");

            return ReplyWilling * baseProbability * AppConfig.ReplyWillingAmplifier;
        }

        public void ChangeReplyWillingAfterSendingMessage()
        {
            ReplyWilling -= 0.6;
            ContextMode = true;
            ReplyWilling = Math.Min(3, Math.Max(0, ReplyWilling));
            LastReplyTime = DateTime.Now;
            MessageHoldCount = 0;
            CommonHelper.DebugLog("发送后回复意愿更新", $"更新后的回复意愿为：{ReplyWilling}");
        }

        public void ChangeReplyWillingAfterNotSendingMessage()
        {
            if (ContextMode)
            {
                ReplyWilling += 0.15;
            }
            else if (HighReplyWilling)
            {
                ReplyWilling += 0.1;
            }
            else
            {
                ReplyWilling += CommonHelper.NextDouble(0.05, 0.1);
            }

            ReplyWilling = Math.Min(3, Math.Max(0, ReplyWilling));
            CommonHelper.DebugLog("不发送后回复意愿更新", $"更新后的回复意愿为：{ReplyWilling}");
        }
    }
}
