using OpenAI.Chat;
using SqlSugar;
using System;
using System.Collections.Generic;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.DB
{
    [SugarTable]
    public class Usage
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int ID { get; set; }

        public int InputToken { get; set; }

        public int OutputToken { get; set; }

        public string Endpoint { get; set; }

        public DateTime Time { get; set; }

        public static void Insert(string endpoint, int inputToken, int outputToken)
        {
            using var db = SQLHelper.GetInstance();
            var u = new Usage()
            {
                Endpoint = endpoint,
                InputToken = inputToken,
                OutputToken = outputToken,
                Time = DateTime.Now,
            };

            db.Insertable(u).ExecuteCommand();
        }

        public static void Insert(string endpoint, ChatTokenUsage usage)
        {
            Insert(endpoint, usage.InputTokenCount, usage.OutputTokenCount);
        }

        public static (int inputToken, int outputToken) GetDayUsage(DateTime time)
        {
            using var db = SQLHelper.GetInstance();
            int i = 0, o = 0;
            foreach (var item in db.Queryable<Usage>().Where(x => x.Time.Date == time.Date).ToList())
            {
                i += item.InputToken;
                o += item.OutputToken;
            }
            return (i, o);
        }

        public static (int inputToken, int outputToken) GetRangeUsage(DateTime start, DateTime end)
        {
            start = start.Date;
            end = end.Date;

            using var db = SQLHelper.GetInstance();
            int i = 0, o = 0;
            foreach (var item in db.Queryable<Usage>().Where(x => x.Time >= start && x.Time <= end).ToList())
            {
                i += item.InputToken;
                o += item.OutputToken;
            }
            return (i, o);
        }

        public static List<Usage> GetDayUsageDetail(DateTime time)
        {
            using var db = SQLHelper.GetInstance();

            return db.Queryable<Usage>().Where(x => x.Time.Date == time.Date).ToList();
        }

        public static List<Usage> GetRangeUsageDetail(DateTime start, DateTime end)
        {
            start = start.Date;
            end = end.Date;
            using var db = SQLHelper.GetInstance();

            return db.Queryable<Usage>().Where(x => x.Time >= start && x.Time <= end).ToList();
        }
    }
}
