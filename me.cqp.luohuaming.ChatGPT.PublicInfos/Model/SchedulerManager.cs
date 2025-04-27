using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.Model
{
    public class SchedulerManager
    {
        public const string Prompt = @"我是{0}，{1}。请为我生成{2}（{3}）的日程安排，包括：
1. 早上的学习或者工作或者娱乐安排
2. 下午的活动和任务
3. 晚上的计划和休息时间
请按照时间顺序列出具体时间点和对应的活动，用一个时间点而不是时间段来表示时间，用JSON格式返回日程表，
仅返回内容，不要返回注释，不要添加任何markdown或代码块样式，时间采用24小时制，
格式为[{{""时间"": ""活动""}},{{""时间"": ""活动""}}]
例如[{{""07:00"": ""起床""}},{{""08:00"": ""打游戏""}},{{""11:00"": ""去食堂吃饭""}},{{""13:00"": ""午觉""}}]。
";
        public SchedulerManager()
        {
            Instance = this;
            EnableTimer();
            Task.Run(UpdateScheduler);
        }

        private void EnableTimer()
        {
            UpdateSchedulerTimer = new()
            {
                Interval = TimeSpan.FromMinutes(1).TotalMilliseconds
            };
            UpdateSchedulerTimer.Elapsed += UpdateSchedulerTimer_Elapsed;
            UpdateSchedulerTimer.Start();
        }

        private void UpdateSchedulerTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (DateTime.Now.Date != LastUpdateTime.Date)
            {
                UpdateScheduler();
            }
        }

        private void UpdateScheduler()
        {
            string path = Path.Combine(MainSave.AppDirectory, "schedules.json");
            if (Schedules.Count == 0 && File.Exists(path))
            {
                try
                {
                    JObject j = JObject.Parse(File.ReadAllText(path));
                    DateTime lastUpdate = j["LastUpdateTime"].ToObject<DateTime>();
                    if (lastUpdate.Date == DateTime.Now.Date)
                    {
                        foreach (var item in j["Schedules"] as JArray)
                        {
                            Schedules.Add((item["Time"].ToObject<DateTime>(), item["Action"].ToString()));
                        }
                        if (Schedules.Count > 0)
                        {
                            LastUpdateTime = lastUpdate;
                            MainSave.CQLog.Info("日程生成", $"已加载日程缓存：{LastUpdateTime:G}. 获取到 {Schedules.Count} 条日程");
                            return;
                        }
                    }
                }
                catch { }
            }
            string prompt = string.Format(Prompt, AppConfig.BotName, AppConfig.SchedulePrompt, DateTime.Now.ToLongDateString(), DateTime.Now.ToString("dddd"));
            var json = Chat.GetChatResult(AppConfig.ChatBaseURL, AppConfig.ChatAPIKey,
            [
                new SystemChatMessage(prompt),
                new UserChatMessage("请回复")
            ], AppConfig.ChatModelName, Chat.Purpose.日程获取);

            if (json == Chat.ErrorMessage)
            {
                MainSave.CQLog.Error("日程生成", $"请求失败");
                return;
            }
            try
            {
                JArray schedule = JArray.Parse(json);
                if (schedule.Count == 0)
                {
                    return;
                }
                Schedules = [];
                foreach (var item in schedule)
                {
                    var property = ((JObject)item).Children().First() as JProperty;
                    DateTime time = DateTime.Parse(property.Name);
                    string action = property.Value.ToString();
                    CommonHelper.DebugLog("日程生成", $"{time.ToShortTimeString()}: {action}");
                    Schedules.Add((time, action));
                }

                MainSave.CQLog.Info("日程生成", $"获取到 {Schedules.Count} 条日程");
                LastUpdateTime = DateTime.Now;
                CacheSchedules(path);
            }
            catch
            { }
        }

        private void CacheSchedules(string path)
        {
            File.WriteAllText(path, new
            {
                LastUpdateTime,
                Schedules = Schedules.Select(x => new { Time = x.time, Action = x.action }).ToArray()
            }.ToJson(true));
        }

        private Timer UpdateSchedulerTimer {  get; set; }

        private DateTime LastUpdateTime { get; set; }

        public List<(DateTime time, string action)> Schedules { get; private set; } = [];

        public static SchedulerManager Instance { get; private set; }

        public string GetCurrentScheduler(DateTime time)
        {
            if (Schedules.Count <= 1)
            {
                return AppConfig.DefaultSchedule;
            }
            for (int i = 0; i < Schedules.Count - 1; i++)
            {
                var schedule = Schedules[i];
                var nextSchedule = Schedules[i + 1];
                if (schedule.time.TimeOfDay <= time.TimeOfDay && nextSchedule.time.TimeOfDay > time.TimeOfDay)
                {
                    return schedule.action;
                }
            }

            return AppConfig.DefaultSchedule;
        }
    }
}
