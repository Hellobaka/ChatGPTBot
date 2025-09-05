using SqlSugar;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.DB
{
    public class APIKeyPurpose
    {
        public int Id { get; set; }

        [JsonIgnore]
        public APIKeys Key { get; set; }

        public string ModelName { get; set; } = string.Empty;
    }

    [SugarTable]
    public class APIKeys
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string EndPoint { get; set; } = string.Empty;

        public string APIKey { get; set; } = string.Empty;

        [SugarColumn(IsJson = true)]
        public List<string> AvailableModels { get; set; } = [];

        public long TokenConsume { get; set; }

        public bool UseTencentSign { get; set; }

        [SugarColumn(IsIgnore = true)]
        public decimal Balance { get; set; }

        [SugarColumn(IsIgnore = true)]
        public static Dictionary<int, APIKeys> KeyCache { get; set; } = [];

        public APIKeys Clone()
        {
            return new APIKeys
            {
                Id = this.Id,
                Name = this.Name,
                EndPoint = this.EndPoint,
                APIKey = this.APIKey,
                AvailableModels = [.. this.AvailableModels],
                TokenConsume = this.TokenConsume,
                Balance = this.Balance,
                UseTencentSign = this.UseTencentSign
            };
        }

        public static List<APIKeys> GetAllKeys()
        {
            using var db = SQLHelper.GetInstance();
            var list = db.Queryable<APIKeys>().ToList();
            foreach(var item in list)
            {
                if (!KeyCache.ContainsKey(item.Id))
                {
                    KeyCache.Add(item.Id, item);
                }
                else
                {
                    KeyCache[item.Id] = item;
                }
            }

            return list;
        }

        public static APIKeys? GetKeyById(int id)
        {
            if (KeyCache.TryGetValue(id, out var cache))
            {
                return cache;
            }
            using var db = SQLHelper.GetInstance();
            return db.Queryable<APIKeys>().First(x => x.Id == id);
        }

        public void Save()
        {
            using var db = SQLHelper.GetInstance();
            if (Id == 0)
            {
                Id = db.Insertable(this).ExecuteCommand();
            }
            else
            {
                db.Updateable(this).ExecuteCommand();
            }
            if (!KeyCache.ContainsKey(Id))
            {
                KeyCache.Add(Id, this);
            }
            else
            {
                KeyCache[Id] = this;
            }
        }

        public void Delete()
        {
            using var db = SQLHelper.GetInstance();
            if (Id != 0)
            {
                db.Deleteable<APIKeys>(x => x.Id == Id).ExecuteCommand();
                if (KeyCache.ContainsKey(Id))
                {
                    KeyCache.Remove(Id);
                }
            }
        }
    }
}
