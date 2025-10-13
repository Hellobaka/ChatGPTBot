using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.DB
{
    public class Qdrant
    {
        public static Qdrant Instance { get; set; }

        private string Host { get; set; }

        private ushort Port { get; set; }

        private List<string> Collections { get; set; } = [];

        private static string CollectionName { get; set; } = "ChatMemory_v2";

        public Qdrant(string host, ushort port)
        {
            Host = host;
            Port = port;
            Instance = this;
        }

        private JObject? Request(string endpoint, string? payload, string method = "GET", int timeout = 60000)
        {
            string result = "";
            try
            {
                using HttpClient client = new();
                client.Timeout = TimeSpan.FromMilliseconds(timeout);

                HttpRequestMessage request = payload == null
                    ? new HttpRequestMessage(new HttpMethod(method), $"http://{Host}:{Port}/{endpoint}")
                    : new HttpRequestMessage(new HttpMethod(method), $"http://{Host}:{Port}/{endpoint}")
                    {
                        Content = new StringContent(payload, Encoding.UTF8, "application/json")
                    };
                request.Headers.Add("api-key", AppConfig.QdrantAPIKey);
                HttpResponseMessage response = client.SendAsync(request).Result;
                result = response.Content.ReadAsStringAsync().Result;
                if (!response.IsSuccessStatusCode)
                {
                    MainSave.CQLog?.Info("发送请求返回失败", result);
                }
                response.EnsureSuccessStatusCode();
                return JObject.Parse(result);
            }
            catch (Exception ex)
            {
                MainSave.CQLog?.Error("发送请求", endpoint + "\n" + $"Payload: {payload}\n{result}\n" + ex);
                return null;
            }
        }

        public bool GetCollections()
        {
            try
            {
                Collections = (Request("collections", null)["result"]["collections"] as JArray).Select(x => x["name"].ToString()).ToList();
                return true;
            }
            catch (Exception e)
            {
                MainSave.CQLog?.Error("检查连接", $"连接失败：{e}");
                return false;
            }
        }

        public bool CreateCollection()
        {
            if (Collections.Contains(CollectionName))
            {
                return true;
            }
            try
            {
                var r = Request($"collections/{CollectionName}", new
                {
                    vectors = new
                    {
                        size = AppConfig.MemoryDimensions,
                        distance = "Cosine",
                        on_disk = true
                    }
                }.ToJson(), "PUT");
                if (r["status"].ToString() != "ok")
                {
                    throw new Exception($"创建集合失败：{r}");
                }

                Collections.Add(CollectionName);
                return true;
            }
            catch (Exception ex)
            {
                MainSave.CQLog.Error("初始化向量数据库", $"初始化失败：{ex}");
                return false;
            }
        }

        public bool Insert(string memory)
        {
            if (string.IsNullOrEmpty(memory))
            {
                if (string.IsNullOrEmpty(memory))
                {
                    MainSave.CQLog.Error("插入向量", $"由于Record传入的文本为空，无法插入");
                }
                return false;
            }
            try
            {
                var embedding = Embedding.GetEmbedding(memory);
                if (embedding.Length == 0)
                {
                    MainSave.CQLog.Error("插入向量", $"由于获取Embedding失败，无法插入");
                    return false;
                }
                if (embedding.Length != AppConfig.MemoryDimensions)
                {
                    MainSave.CQLog.Error("插入向量", $"由于Embedding维度数量与记忆维度不符，无法插入");
                    return false;
                }
                var r = Request($"collections/{CollectionName}/points", new
                {
                    points = new object[]
                    {
                        new
                        {
                            id = Guid.NewGuid().ToString(),
                            vector = embedding,
                            payload = new
                            {
                                timestamp = DateTime.Now.GetTimeStamp(),
                                text = memory,
                            }
                        }
                    }
                }.ToJson(), "PUT");
                string resultStr = r["status"].ToString();
                string status = r["result"]["status"].ToString();

                bool result = resultStr == "ok" && (status == "acknowledged" || status == "completed");

                return result;
            }
            catch (Exception ex)
            {
                MainSave.CQLog.Error("插入向量", $"插入失败：{ex}");
                return false;
            }
        }

        public (string id, string record, DateTime time, float score)[] GetRelevantCollection(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return [];
            }

            try
            {
                var r = Request($"collections/{CollectionName}/points/query", new
                {
                    query = Embedding.GetEmbedding(query),
                    limit = AppConfig.EnableRerank ? 50 : (ulong)AppConfig.MaxMemoryCount,
                }.ToJson(), "POST");

                if (r["status"].ToString() != "ok")
                {
                    MainSave.CQLog.Error("向量查询", $"查询失败：{r}");
                    return [];
                }
                var searchResult = (r["result"]["points"] as JArray).Select(x => new { Id = x["id"], Payload = x["payload"], Score = (float)x["score"] }).ToArray();
                searchResult = searchResult.Where(x => x.Score >= AppConfig.MinMemorySimilarity).ToArray();
                
                using var db = SQLHelper.GetInstance();
                (string id, string record, DateTime time, float score)[] result = [];
                if (AppConfig.EnableRerank)
                {
                    var search = searchResult.Select(x => x.Payload["text"].ToString()).ToArray();
                    var rerank = Rerank.GetRerank(query, search, AppConfig.MaxMemoryCount);
                    foreach (var (document, score) in rerank)
                    {
                        var point = searchResult.FirstOrDefault(x => x.Payload["text"].ToString() == document);
                        if (point == null)
                        {
                            continue;
                        }
                        var id = point.Id.ToString();
                        var time = CommonHelper.TimestampToDateTime(((long)point.Payload["time"]));
                        result = [(id, point.Payload["text"].ToString(), time, score), .. result];
                    }
                    return result;
                }
                else
                {
                    foreach (var item in searchResult.OrderByDescending(x => x.Score).Take(AppConfig.MaxMemoryCount))
                    {
                        var id = item.Id.ToString();
                        var time = CommonHelper.TimestampToDateTime(((long)item.Payload["time"]));
                        result = [(id, item.Payload["text"].ToString(), time, item.Score), .. result];
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                MainSave.CQLog.Error("向量查询", $"输入: {query}，查询失败：{ex}");
                return [];
            }
        }

        public bool Delete(string id)
        {
            try
            {
                var r = Request($"collections/{CollectionName}/points/delete?wait=true", new
                {
                    points = new string[] { id },
                }.ToJson(), "POST");
                bool ok = r?["status"]?.ToString() == "ok";

                return ok;
            }
            catch (Exception ex)
            {
                MainSave.CQLog?.Error("Qdrant删除", $"删除失败：{ex}");
                return false;
            }
        }

        public bool DropCollection()
        {
            try
            {
                var r = Request($"collections/{CollectionName}", null, "DELETE");
                bool ok = r?["status"]?.ToString() == "ok";

                return ok;
            }
            catch (Exception ex)
            {
                MainSave.CQLog?.Error("Qdrant删除集合", $"删除失败：{ex}");
                return false;
            }
        }

        public int GetCollectionCount()
        {
            try
            {
                var r = Request($"collections/{CollectionName}/points/count", new
                {
                    exact = true,
                }.ToJson(), "POST");
                return r?["result"]?["count"]?.ToObject<int>() ?? 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
