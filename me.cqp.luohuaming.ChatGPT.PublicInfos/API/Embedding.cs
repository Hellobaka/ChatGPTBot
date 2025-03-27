using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public static class Embedding
    {
        public static double[] GetEmbedding(string text)
        {
            string url = $"";
            var json = CommonHelper.Post("POST", url, new
            {
                model = "",
                input = text
            }.ToJson(), "");
            try
            {
                return JObject.Parse(json)["data"][0]["embedding"].ToObject<double[]>();
            }
            catch (Exception ex)
            {
                MainSave.CQLog.Debug("Embedding", json);
                MainSave.CQLog.Error("获取Embedding", $"结果Json解析失败: {ex.Message}");

                return [];
            }
        }
    }
}
