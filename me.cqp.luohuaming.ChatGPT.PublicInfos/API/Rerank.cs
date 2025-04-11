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
            string json;
            bool tencent, ali = false;
            if (AppConfig.RerankUrl.Contains("lkeap.tencentcloudapi.com") && AppConfig.EnableTencentSign)
            {
                tencent = true;
                json = CommonHelper.Post_TecentSignV3(new
                {
                    Model = AppConfig.RerankModelName,
                    Query = text,
                    Docs = documents
                }.ToJson(), TencentAPIAction, 3000);
            }
            else
            {
                tencent = false;
                if (AppConfig.RerankUrl.Contains("aliyuncs.com"))
                {
                    ali = true;
                    // 阿里百炼
                    json = CommonHelper.Post("POST", AppConfig.RerankUrl, new
                    {
                        model = AppConfig.RerankModelName,
                        input = new
                        {
                            query = text,
                            documents,
                        },
                        parameters = new 
                        {
                            top_n = topn,
                        }
                    }.ToJson(), AppConfig.RerankApiKey, 3000);
                }
                else
                {
                    json = CommonHelper.Post("POST", AppConfig.RerankUrl, new
                    {
                        model = AppConfig.RerankModelName,
                        query = text,
                        documents,
                        top_n = topn,
                    }.ToJson(), AppConfig.RerankApiKey, 3000);
                }
            }
            try
            {
                var j = JObject.Parse(json);
                (string document, float score)[] results = [];
                if (tencent)
                {
                    for (int i = 0; i < documents.Length; i++)
                    {
                        var document = documents[i];
                        results = [(document, (float)j["Response"]["ScoreList"][i]), .. results];
                    }
                }
                else
                {
                    var arr = ali ? j["output"]["results"] : j["results"];
                    foreach (var item in arr as JArray)
                    {
                        int index = ((int)item["index"]);
                        float score = ((float)item["relevance_score"]);

                        var document = documents[index];

                        results = [(document, score), .. results];
                    }
                }

                return results.OrderByDescending(x => x.score).Take(topn).ToArray();
            }
            catch (Exception ex)
            {
                MainSave.CQLog.Debug("Rerank", json ?? "null");
                MainSave.CQLog.Error("获取Rerank", $"结果Json解析失败: {ex.Message}");

                return [];
            }
        }
    }
}
