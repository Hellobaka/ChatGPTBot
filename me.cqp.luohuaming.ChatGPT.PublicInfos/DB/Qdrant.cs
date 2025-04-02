using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System;
using System.Collections.Generic;
using System.Linq;
using static Qdrant.Client.Grpc.Conditions;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.DB
{
    public class Qdrant
    {
        public static Qdrant Instance { get; set; }

        private string Host { get; set; }

        private ushort Port { get; set; }

        private QdrantClient QdrantClient { get; set; }

        private List<string> Collections { get; set; } = [];

        private static string CollectionName { get; set; } = "ChatMemroy";

        public Qdrant(string host, ushort port)
        {
            Host = host;
            Port = port;
            QdrantClient = new(host, port, false);
            Instance = this;
        }

        public bool CheckConnection()
        {
            try
            {
                Collections = QdrantClient.ListCollectionsAsync().Result.ToList();
                return true;
            }
            catch
            {
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
                QdrantClient.CreateCollectionAsync(CollectionName, new VectorParams
                {
                    Size = 1536,
                    Distance = Distance.Cosine,
                    OnDisk = true
                }, timeout: TimeSpan.FromSeconds(30)).Wait();

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
                var result = QdrantClient.UpsertAsync(CollectionName,
                    [new PointStruct() {
                    Id = (ulong)record.Id,
                    Vectors = Embedding.GetEmbedding(record.Message_NoAppendInfo),
                    Payload = {
                        ["user_id"]=$"{record.GroupID}_{record.QQ}",
                        ["timestamp"] = record.Time.GetTimeStamp(),
                        ["text"] = record.Message_NoAppendInfo
                    }
                }]).Result;

                return result.Status == UpdateStatus.Completed || result.Status == UpdateStatus.Acknowledged;
            }
            catch (Exception ex)
            {
                MainSave.CQLog.Error("插入向量", $"插入失败：{ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        public (string text, float score)[] GetReleventCollection(ChatRecord record)
        {
            if (record.IsEmpty || record.IsEmpty || string.IsNullOrEmpty(record.Message_NoAppendInfo))
            {
                return [];
            }

            try
            {
                var searchResult = QdrantClient.QueryAsync(
                    collectionName: CollectionName,
                    query: Embedding.GetEmbedding(record.Message_NoAppendInfo),
                    filter: MatchKeyword("user_id", $"{record.GroupID}_{record.QQ}") & Range("timestamp", new Range() { Gte = record.Time.AddMonths(-3).GetTimeStamp() }),
                    limit: AppConfig.EnableRerank ? 50 : (ulong)AppConfig.MaxMemoryCount,
                    payloadSelector: true
                ).Result;
                if (AppConfig.EnableRerank)
                {
                    var s = searchResult.Select(x => x.Payload["text"].ToString()).ToArray();
                    return Rerank.GetRerank(record.Message_NoAppendInfo, s, AppConfig.MaxMemoryCount);
                }
                else
                {
                    return searchResult.Select(x => (x.Payload["text"].ToString(), x.Score)).OrderByDescending(x => x.Score).ToArray();
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
