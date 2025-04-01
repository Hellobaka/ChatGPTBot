using Newtonsoft.Json.Linq;
using System;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public static class Embedding
    {
        private static string TencentAPIAction { get; set; } = "GetEmbedding";

        public static double[] GetEmbedding(string text)
        {
            string json;
            if(AppConfig.EmbeddingUrl.Contains("lkeap.tencentcloudapi.com") && AppConfig.EnableTencentSign)
            {
                json = CommonHelper.Post_TecentSignV3(new
                {
                    model = AppConfig.EmbeddingModelName,
                    input = text
                }.ToJson(), TencentAPIAction, 3000);
            }
            else
            {
                json = CommonHelper.Post("POST", AppConfig.EmbeddingUrl, new
                {
                    model = AppConfig.EmbeddingModelName,
                    input = text
                }.ToJson(), AppConfig.EmbeddingApiKey, 3000);
            }
            try
            {
                return JObject.Parse(json)["data"][0]["embedding"].ToObject<double[]>();
            }
            catch (Exception ex)
            {
                MainSave.CQLog.Debug("Embedding", json ?? "null");
                MainSave.CQLog.Error("获取Embedding", $"结果Json解析失败: {ex.Message}");

                return [];
            }
        }
    }
}
