using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
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

        public string ModelName { get; set; }

        public string Purpose { get; set; }

        public DateTime Time { get; set; }

        public static event Action<Usage> OnUsageInserted;

        public static void Insert(string endpoint, string modelName, string purpose, int inputToken, int outputToken)
        {
            using var db = SQLHelper.GetInstance();
            var u = new Usage()
            {
                Endpoint = endpoint,
                InputToken = inputToken,
                OutputToken = outputToken,
                ModelName = modelName,
                Purpose = purpose,
                Time = DateTime.Now,
            };

            db.Insertable(u).ExecuteCommand();
            OnUsageInserted?.BeginInvoke(u, null, null);
        }

        public static List<Usage> GetDayUsageDetail(DateTime time)
        {
            using var db = SQLHelper.GetInstance();

            return db.Queryable<Usage>().Where(x => x.Time.Date == time.Date).ToList();
        }

        public static List<Usage> GetRangeUsageDetail(DateTime start, DateTime end)
        {
            start = start.Date;
            end = end.Date.AddDays(1);
            using var db = SQLHelper.GetInstance();

            return db.Queryable<Usage>().Where(x => x.Time >= start && x.Time <= end).ToList();
        }

        public static (string[] services, string[] models, string[] puropses) GetGroups()
        {
            using var db = SQLHelper.GetInstance();
            var services = db.Queryable<Usage>().Select(x => x.Endpoint).Distinct().ToList();
            var models = db.Queryable<Usage>().Select(x => x.ModelName).Distinct().ToList();
            var puropses = db.Queryable<Usage>().Select(x => x.Purpose).Distinct().ToList();
            services.RemoveAll(x => string.IsNullOrEmpty(x));
            models.RemoveAll(x => string.IsNullOrEmpty(x));
            puropses.RemoveAll(x => string.IsNullOrEmpty(x));

            return (services.ToArray(), models.ToArray(), puropses.ToArray());
        }
    }
}
