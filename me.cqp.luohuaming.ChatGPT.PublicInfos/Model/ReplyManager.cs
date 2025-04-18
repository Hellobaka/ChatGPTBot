using System;
using System.Collections.Generic;
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
                || (!HighReplyWilling && MainSave.Random.NextDouble() < 0.1))
            {
                if (HighReplyWilling)
                {
                    HighReplyWilling = false;
                    ReplyWilling = 0.01;
                    CurrentModeKeepInterval = TimeSpan.FromMinutes(MainSave.Random.Next(10, 20));
                }
                else
                {
                    HighReplyWilling = true;
                    ReplyWilling = 1;
                    CurrentModeKeepInterval = TimeSpan.FromMinutes(MainSave.Random.Next(3, 5));
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

            double baseProbablity = 0;
            if (ContextMode)
            {
                baseProbablity = HighReplyWilling ? 0.5 : 0.25;
            }
            else if (HighReplyWilling)
            {
                baseProbablity = (MessageHoldCount >= 4 && MessageHoldCount <= 8) ? 0.5 : 0.2;
            }
            else
            {
                baseProbablity = MessageHoldCount > 15 ? 0.3 : (0.03 * Math.Min(MessageHoldCount, 10));
            }

            ReplyWilling = Math.Min(3, Math.Max(0, ReplyWilling));
            LastReplyQQ = qq;

            CommonHelper.DebugLog("回复意愿更新", $"更新后的回复意愿为：{ReplyWilling}，回复倍率：{AppConfig.ReplyWillingAmplifier}，额外倍率：{baseProbablity}，最终计算结果：{ReplyWilling * baseProbablity * AppConfig.ReplyWillingAmplifier}");

            return ReplyWilling * baseProbablity * AppConfig.ReplyWillingAmplifier;
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
                ReplyWilling += MainSave.Random.NextDouble(0.05, 0.1);
            }

            ReplyWilling = Math.Min(3, Math.Max(0, ReplyWilling));
            CommonHelper.DebugLog("不发送后回复意愿更新", $"更新后的回复意愿为：{ReplyWilling}");
        }
    }
}
