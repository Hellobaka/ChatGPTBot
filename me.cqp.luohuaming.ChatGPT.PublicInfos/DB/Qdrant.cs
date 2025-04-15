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

        private static string CollectionName { get; set; } = "ChatMemroy";

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

                HttpRequestMessage request;
                if (payload == null)
                {
                    request = new HttpRequestMessage(new HttpMethod(method), $"http://{Host}:{Port}/{endpoint}");
                }
                else
                {
                    request = new HttpRequestMessage(new HttpMethod(method), $"http://{Host}:{Port}/{endpoint}")
                    {
                        Content = new StringContent(payload, Encoding.UTF8, "application/json")
                    };
                }
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
                MainSave.CQLog?.Error("发送请求", endpoint + "\n" + $"Payload: {payload}\n{result}\n" + ex.Message + ex.StackTrace);
                return null;
            }
        }

        public bool CheckConnection()
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
                MainSave.CQLog.Error("初始化向量数据库", $"初始化失败：{ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public bool Insert(ChatRecord record)
        {
            if (record.IsEmpty || record.IsEmpty || string.IsNullOrEmpty(record.Message_NoAppendInfo))
            {
                if (string.IsNullOrEmpty(record.Message_NoAppendInfo))
                {
                    MainSave.CQLog.Error("插入向量", $"由于Record传入的文本为空，无法插入");
                }
                return false;
            }
            try
            {
                var embedding = Embedding.GetEmbedding(record.Message_NoAppendInfo);
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
                            id = (ulong)record.Id,
                            vector = embedding,
                            payload = new
                            {
                                user_id = $"{record.GroupID}_{record.QQ}",
                                timestamp = record.Time.GetTimeStamp(),
                                text = record.Message_NoAppendInfo,
                            }
                        }
                    }
                }.ToJson(), "PUT");
                string result = r["status"].ToString();
                string status = r["result"]["status"].ToString();

                return result == "ok" && (status == "acknowledged" || status == "completed");
            }
            catch (Exception ex)
            {
                MainSave.CQLog.Error("插入向量", $"插入失败：{ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public (ChatRecord record, float score)[] GetReleventCollection(ChatRecord record)
        {
            if (record.IsEmpty || record.IsEmpty || string.IsNullOrEmpty(record.Message_NoAppendInfo))
            {
                return [];
            }

            try
            {
                object filter;
                object timeRange = new
                {
                    key = "timestamp",
                    range = new
                    {
                        gte = record.Time.AddMonths(-3).GetTimeStamp()
                    }
                };
                if (record.GroupID > 0)
                {
                    if (AppConfig.QdrantSearchOnlyPerson)
                    {
                        filter = new
                        {
                            key = "user_id",
                            match = new
                            {
                                value = $"{record.GroupID}_{record.QQ}"
                            }
                        };
                    }
                    else
                    {
                        filter = new
                        {
                            key = "user_id",
                            match = new
                            {
                                text = $"{record.GroupID}_"
                            }
                        };
                    }
                }
                else
                {
                    filter = new
                    {
                        key = "user_id",
                        match = new
                        {
                            value = $"{record.GroupID}_{record.QQ}"
                        }
                    };
                }
                var r = Request($"collections/{CollectionName}/points/query", new
                {
                    query = Embedding.GetEmbedding(record.Message_NoAppendInfo),
                    limit = AppConfig.EnableRerank ? 50 : (ulong)AppConfig.MaxMemoryCount,
                    filter = new
                    {
                        must = new object[]
                        {
                            filter,
                            timeRange
                        }
                    },
                    with_payload = true
                }.ToJson(), "POST");
                if (r["status"].ToString() != "ok")
                {
                    MainSave.CQLog.Error("向量查询", $"查询失败：{r}");
                    return [];
                }
                var searchResult = (r["result"]["points"] as JArray).Select(x => new { Id = (long)x["id"], Payload = x["payload"], Score = (float)x["score"] }).ToArray();
                searchResult = searchResult.Where(x => x.Id != record.Id && x.Score >= AppConfig.MinMemorySimilarty).ToArray();
                using var db = SQLHelper.GetInstance();
                if (AppConfig.EnableRerank)
                {
                    var search = searchResult.Select(x => x.Payload["text"].ToString()).ToArray();
                    var rerank = Rerank.GetRerank(record.Message_NoAppendInfo, search, AppConfig.MaxMemoryCount);
                    (ChatRecord records, float score)[] result = [];
                    foreach (var (document, score) in rerank)
                    {
                        var point = searchResult.FirstOrDefault(x => x.Payload["text"].ToString() == document);
                        if (point == null)
                        {
                            continue;
                        }
                        var id = (int)point.Id;
                        result = [(ChatRecord.GetChatRecordById(db, id), score), .. result];
                    }
                    return result;
                }
                else
                {
                    (ChatRecord records, float score)[] result = [];
                    foreach (var item in searchResult.OrderByDescending(x => x.Score).Take(AppConfig.MaxMemoryCount))
                    {
                        var id = (int)item.Id;
                        result = [(ChatRecord.GetChatRecordById(db, id), item.Score), .. result];
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                MainSave.CQLog.Error("向量查询", $"输入: {record.Message_NoAppendInfo}，查询失败：{ex.Message}\n{ex.StackTrace}");
                return [];
            }
        }
    }
}
