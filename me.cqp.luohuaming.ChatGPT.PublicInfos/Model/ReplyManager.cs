using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.Model
{
    public class ReplyManager
    {
        public static Dictionary<long, ReplyManager> ReplyManagers { get; set; } = [];

        public bool HighReplyWilling { get; set; }

        public int MessageHoldCount { get; set; }

        public DateTime WillingLastChangeTime { get; set; }

        public DateTime LastReplyTime { get; set; }

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
                replyManager = new ReplyManager(id);
                ReplyManagers.Add(id, replyManager);
                return replyManager;
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
            }

            if ((DateTime.Now - LastReplyTime).TotalMinutes == 5)
            {
                ContextMode = false;
            }
        }

        public double ChangeReplyWilling(bool emoji, bool at, bool contain, long qq)
        {
            MessageHoldCount++;
            if (qq == LastReplyQQ && (DateTime.Now - LastReplyTime).TotalMinutes < 2 && MessageHoldCount <= 5)
            {
                ContextMode = true;
                ReplyWilling += 0.3;
            }

            if (at)
            {
                ReplyWilling += 1;
                ContextMode = true;
            }

            if (contain)
            {
                ReplyWilling += 0.5;
                ContextMode = true;
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

            ReplyWilling *= baseProbablity;

            ReplyWilling = Math.Min(3, Math.Max(0, ReplyWilling));
            LastReplyQQ = qq;

            return ReplyWilling;
        }

        public void ChangeReplyWillingAfterSendingMessage()
        {
            ReplyWilling -= 0.3;

            ReplyWilling = Math.Min(3, Math.Max(0, ReplyWilling));
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
        }
    }
}
