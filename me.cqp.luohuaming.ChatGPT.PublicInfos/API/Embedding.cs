using Newtonsoft.Json.Linq;
using System;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public static class Embedding
    {
        private static string TencentAPIAction { get; set; } = "GetEmbedding";

        public static float[] GetEmbedding(string text)
        {
            string json;
            bool isTencentAPI = false;
            if (AppConfig.EmbeddingUrl.Contains("lkeap.tencentcloudapi.com") && AppConfig.EnableTencentSign)
            {
                isTencentAPI = true;
                json = CommonHelper.Post_TecentSignV3(new
                {
                    Model = AppConfig.EmbeddingModelName,
                    Inputs = new string[] { text }
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
                if (isTencentAPI)
                {
                    return JObject.Parse(json)["Response"]["Data"][0]["Embedding"].ToObject<float[]>();
                }
                else
                {
                    return JObject.Parse(json)["data"][0]["embedding"].ToObject<float[]>();
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
