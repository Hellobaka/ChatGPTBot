using Newtonsoft.Json;
using SqlSugar;
using System.Collections.Generic;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.DB
{
    public class APIKeyPurpose
    {
        public int Id { get; set; }

        [JsonIgnore]
        public APIKeys Key { get; set; }

        public string ModelName { get; set; } = string.Empty;

        public APIKeyPurpose Clone()
        {
            return new APIKeyPurpose
            {
                Id = this.Id,
                Key = this.Key?.Clone(),
                ModelName = this.ModelName
            };
        }
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

        public override bool Equals(object obj)
        {
            if (obj is APIKeys key)
            {
                return key.Id == this.Id;
            }
            return false;
        }

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
            foreach (var item in list)
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

        public static APIKeys? GetKeyByAPIKey(string apiKey)
        {
            using var db = SQLHelper.GetInstance();
            return db.Queryable<APIKeys>().First(x => x.APIKey == apiKey);
        }

        public static void UpdateTokenConsume(string apiKey, long tokens)
        {
            using var db = SQLHelper.GetInstance();
            var key = db.Queryable<APIKeys>().First(x => x.APIKey == apiKey);
            if (key != null)
            {
                key.TokenConsume += tokens;
                db.Updateable(key).ExecuteCommand();
                if (KeyCache.ContainsKey(key.Id))
                {
                    KeyCache[key.Id] = key;
                }
            }
        }

        public void Save()
        {
            using var db = SQLHelper.GetInstance();
            if (Id == 0)
            {
                Id = db.Insertable(this).ExecuteReturnIdentity();
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

        public void AddTokenConsume(long tokens)
        {
            if (tokens <= 0)
            {
                return;
            }
            using var db = SQLHelper.GetInstance();
            TokenConsume += tokens;
            db.Updateable(this).ExecuteCommand();
        }
    }
}
