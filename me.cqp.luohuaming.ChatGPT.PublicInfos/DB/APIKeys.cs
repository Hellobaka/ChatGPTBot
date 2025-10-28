using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.DB
{
    public class APIKeyPurpose
    {
        public int Id { get; set; }

        [JsonIgnore]
        public APIKeys Key { get; set; }

        public LLMModel Model { get; set; }

        public APIKeyPurpose Clone()
        {
            return new APIKeyPurpose
            {
                Id = this.Id,
                Key = this.Key?.Clone(),
                Model = this.Model
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

        [Navigate(NavigateType.OneToMany, nameof(LLMModel.APIKeyId))]
        public List<LLMModel> AvailableModels { get; set; }

        public long TokenConsume { get; set; }

        public decimal TotalConsume { get; set; }

        public bool UseTencentSign { get; set; }

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
                TotalConsume = this.TotalConsume,
                UseTencentSign = this.UseTencentSign
            };
        }

        public static List<APIKeys> GetAllKeys()
        {
            using var db = SQLHelper.GetInstance();
            var list = db.Queryable<APIKeys>().Includes(x => x.AvailableModels).ToList();
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
            return db.Queryable<APIKeys>().Includes(x => x.AvailableModels).First(x => x.Id == id);
        }

        public static APIKeys? GetKeyByAPIKey(string apiKey)
        {
            using var db = SQLHelper.GetInstance();
            return db.Queryable<APIKeys>().Includes(x => x.AvailableModels).First(x => x.APIKey == apiKey);
        }

        public static void UpdateTokenConsume(string apiKey, LLMModel model, long inputTokenCount, long outputTokenCount, long cachedTokenCount, long totalTokenCount)
        {
            using var db = SQLHelper.GetInstance();
            var key = db.Queryable<APIKeys>().Includes(x => x.AvailableModels).First(x => x.APIKey == apiKey);
            if (key != null)
            {
                key.AddTokenConsume(model, inputTokenCount, outputTokenCount, totalTokenCount, cachedTokenCount);
                if (KeyCache.ContainsKey(key.Id))
                {
                    KeyCache[key.Id] = key;
                }
            }
        }

        public void Save()
        {
            using var db = SQLHelper.GetInstance();
            AvailableModels ??= [];
            if (Id == 0)
            {
                Id = db.InsertNav(this).Include(x => x.AvailableModels).ExecuteReturnEntity().Id;

                // 设置所有模型的APIKeyId
                foreach (var model in AvailableModels)
                {
                    model.APIKeyId = Id;
                }
            }
            else
            {
                db.UpdateNav(this).Include(x => x.AvailableModels).ExecuteCommand();

                // 设置所有模型的APIKeyId
                foreach (var model in AvailableModels)
                {
                    model.APIKeyId = Id;
                }
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
                db.DeleteNav<APIKeys>(x => x.Id == Id).Include(x => x.AvailableModels).ExecuteCommand();
                if (KeyCache.ContainsKey(Id))
                {
                    KeyCache.Remove(Id);
                }
            }
        }

        public void AddTokenConsume(LLMModel model, long inputToken, long outputToken, long totalToken, long cachedToken)
        {
            if (totalToken <= 0)
            {
                return;
            }
            lock (KeyCache)
            {
                var consume = model.CalcConsume(inputToken, outputToken, totalToken, cachedToken);

                using var db = SQLHelper.GetInstance();
                TokenConsume += totalToken;
                TotalConsume += consume;
                model.TotalConsume += consume;

                db.UpdateNav(this).Include(x => x.AvailableModels).ExecuteCommand();
                //db.Updateable(model).ExecuteCommand();
            }
        }
    }

    [SugarTable]
    public class LLMModel
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        public int APIKeyId { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal OutputConsumePer1M { get; set; } = decimal.Zero;

        public decimal InputConsumePer1M { get; set; } = decimal.Zero;

        public decimal InputCachedConsumePer1M { get; set; } = decimal.Zero;

        public decimal TotalConsume { get; set; } = decimal.Zero;

        public decimal CalcConsume(long inputTokenCount, long outputTokenCount, long totalTokenCount, long cachedTokenCount)
        {
            decimal nonCachedInputToken = inputTokenCount - cachedTokenCount;

            decimal consume = (nonCachedInputToken / 1000000) * InputConsumePer1M
                + (outputTokenCount / 1000000) * OutputConsumePer1M
                + (cachedTokenCount / 1000000) * InputCachedConsumePer1M;

            return consume;
        }

        public LLMModel Clone()
        {
            return new LLMModel
            {
                Id = Id,
                APIKeyId = APIKeyId,
                InputCachedConsumePer1M = InputCachedConsumePer1M,
                InputConsumePer1M = InputConsumePer1M,
                Name = Name,
                OutputConsumePer1M = OutputConsumePer1M,
                TotalConsume = TotalConsume,
            };
        }
    }
}
