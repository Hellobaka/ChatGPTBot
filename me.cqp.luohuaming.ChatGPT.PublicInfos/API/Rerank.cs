using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Security.Cryptography;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public static class Rerank
    {
        private static string TencentAPIAction { get; set; } = "RunRerank";

        public static (string document, float score)[] GetRerank(string text, string[] documents, int topn = 5)
        {
            if (documents.Length == 0)
            {
                return [];
            }
            string json = null;
            var api = AppConfig.RerankApiKeyId.OrderBy(x => Guid.NewGuid()).FirstOrDefault();
            if (api == null)
            {
                MainSave.CQLog.Error("Rerank", "没有可用的Rerank API Key");
                return [];
            }
            try
            {
                (string document, float score)[] results = [];
                long tokenUsage = 0;

                // 针对腾讯云、阿里百炼进行特殊处理
                if (api.Key.UseTencentSign)
                {
                    json = CommonHelper.Post_TecentSignV3(new
                    {
                        Model = api.ModelName,
                        Query = text,
                        Docs = documents
                    }.ToJson(), TencentAPIAction, AppConfig.RerankTimeout);
                    var j = JObject.Parse(json);
                    for (int i = 0; i < documents.Length; i++)
                    {
                        var document = documents[i];
                        results = [(document, (float)j["Response"]["ScoreList"][i]), .. results];
                    }
                    tokenUsage = j["Response"]["Usage"]["TotalTokens"].ToObject<long>();
                }
                else
                {
                    JToken arr;
                    if (api.Key.EndPoint.Contains("aliyuncs.com"))
                    {
                        // 阿里百炼
                        json = CommonHelper.Post("POST", api.Key.EndPoint, new
                        {
                            model = api.ModelName,
                            input = new
                            {
                                query = text,
                                documents,
                            },
                            parameters = new
                            {
                                top_n = topn,
                            }
                        }.ToJson(), api.Key.APIKey, AppConfig.RerankTimeout);
                        var j = JObject.Parse(json);
                        arr = j["output"]["results"];
                        tokenUsage = j["usage"]["total_tokens"].ToObject<long>();
                    }
                    else
                    {
                        json = CommonHelper.Post("POST", api.Key.EndPoint, new
                        {
                            model = api.ModelName,
                            query = text,
                            documents,
                            top_n = topn,
                        }.ToJson(), api.Key.APIKey, AppConfig.RerankTimeout);
                        var j = JObject.Parse(json);
                        arr = j["results"];
                        tokenUsage = j["tokens"]["input_tokens"].ToObject<long>()
                            + j["tokens"]["output_tokens"].ToObject<long>();
                    }

                    foreach (var item in arr as JArray)
                    {
                        int index = ((int)item["index"]);
                        float score = ((float)item["relevance_score"]);

                        var document = documents[index];

                        results = [(document, score), .. results];
                    }
                }

                api.Key.AddTokenConsume(tokenUsage);
                return results.OrderByDescending(x => x.score).Take(topn).ToArray();
            }
            catch (Exception ex)
            {
                CommonHelper.DebugLog("Rerank", json ?? "null");
                MainSave.CQLog.Error("获取Rerank", $"结果Json解析失败: {ex}");

                return [];
            }
        }
    }
}
