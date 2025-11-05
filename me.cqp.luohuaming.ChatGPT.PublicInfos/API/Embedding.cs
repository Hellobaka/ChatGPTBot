using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public static class Embedding
    {
        private static string TencentAPIAction { get; set; } = "GetEmbedding";

        public static float[] GetEmbedding(string text)
        {
            string json = null;
            try
            {
                var api = AppConfig.EmbeddingApiKeyId.OrderBy(x => Guid.NewGuid()).FirstOrDefault();
                if (api == null)
                {
                    MainSave.CQLog.Error("获取Embedding", $"Embedding 的 API 为空");
                    return [];
                }
                if (api.Key.UseTencentSign)
                {
                    json = CommonHelper.Post_TencentSignV3(new
                    {
                        Model = api.Model.Name,
                        Inputs = new string[] { text }
                    }.ToJson(), TencentAPIAction, AppConfig.EmbeddingTimeout);

                    var embeddings = JObject.Parse(json)["Response"]["Data"][0]["Embedding"].ToObject<float[]>();
                    var tokenUsage = JObject.Parse(json)["Response"]["Usage"]["TotalTokens"].ToObject<long>();
                    api.Key.AddTokenConsume(api.Model, 0, tokenUsage, tokenUsage, 0);

                    return embeddings;
                }
                else
                {
                    json = CommonHelper.Post("POST", api.Key.EndPoint, new
                    {
                        model = api.Model.Name,
                        input = text
                    }.ToJson(), api.Key.APIKey, AppConfig.EmbeddingTimeout);
                    var embeddings = JObject.Parse(json)["data"][0]["embedding"].ToObject<float[]>();
                    var tokenUsage = JObject.Parse(json)["usage"]["total_tokens"].ToObject<long>();
                    api.Key.AddTokenConsume(api.Model, 0, tokenUsage, tokenUsage, 0);

                    return embeddings;
                }
            }
            catch (Exception ex)
            {
                CommonHelper.DebugLog("Embedding", json ?? "null");
                MainSave.CQLog.Error("获取Embedding", $"结果Json解析失败: {ex}");

                return [];
            }
        }
    }
}
