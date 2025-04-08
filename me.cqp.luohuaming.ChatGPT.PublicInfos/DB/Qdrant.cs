using Grpc.Net.Client;
using Grpc.Core.Interceptors;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using static Qdrant.Client.Grpc.Conditions;
using System.IO;

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
            WinHttpHandler handler = new()
            {
                SslProtocols = SslProtocols.Tls13,
                WindowsProxyUsePolicy = WindowsProxyUsePolicy.DoNotUseProxy
            };

            X509Certificate2 cert = new(Path.Combine(MainSave.AppDirectory, "ca.pfx"), AppConfig.QdrantCertPassword);
            handler.ClientCertificates.Add(cert);

            var channel = GrpcChannel.ForAddress($"https://{host}:{port}", new GrpcChannelOptions
            {
                HttpHandler = handler
            });
            var callInvoker = channel.Intercept(metadata =>
            {
                metadata.Add("api-key", AppConfig.QdrantAPIKey);
                return metadata;
            });

            var grpcClient = new QdrantGrpcClient(callInvoker);
            QdrantClient = new QdrantClient(grpcClient);
            Instance = this;
        }

        public bool CheckConnection()
        {
            try
            {
                Collections = QdrantClient.ListCollectionsAsync().Result.ToList();
                return true;
            }
            catch (Exception e)
            {
                MainSave.CQLog?.Error("检查连接", $"连接失败：{e.Message}");
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
                    Size = 1024,
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
                        ["text"] = record.Message_NoAppendInfo,
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

        public (ChatRecord record, float score)[] GetReleventCollection(ChatRecord record)
        {
            if (record.IsEmpty || record.IsEmpty || string.IsNullOrEmpty(record.Message_NoAppendInfo))
            {
                return [];
            }

            try
            {
                Filter filter;
                if(record.GroupID > 0)
                {
                    filter = (AppConfig.QdrantSearchOnlyPerson ? MatchKeyword("user_id", $"{record.GroupID}_{record.QQ}") : MatchText("user_id", $"{record.GroupID}_"))
                        & Range("timestamp", new Range() { Gte = record.Time.AddMonths(-3).GetTimeStamp() });
                }
                else
                {
                    filter = MatchKeyword("user_id", $"{record.GroupID}_{record.QQ}") & Range("timestamp", new Range() { Gte = record.Time.AddMonths(-3).GetTimeStamp() });
                }
                var searchResult = QdrantClient.QueryAsync(
                    collectionName: CollectionName,
                    query: Embedding.GetEmbedding(record.Message_NoAppendInfo),
                    filter: filter,
                    limit: AppConfig.EnableRerank ? 50 : (ulong)AppConfig.MaxMemoryCount,
                    payloadSelector: true
                ).Result;
                searchResult = searchResult.Where(x => x.Id.Num != (ulong)record.Id).ToList();
                using var db = SQLHelper.GetInstance();
                if (AppConfig.EnableRerank)
                {
                    var search = searchResult.Select(x => x.Payload["text"].StringValue.ToString()).ToArray();
                    var rerank = Rerank.GetRerank(record.Message_NoAppendInfo, search, AppConfig.MaxMemoryCount);
                    (ChatRecord records, float score)[] result = [];
                    foreach (var (document, score) in rerank)
                    {
                        var point = searchResult.FirstOrDefault(x => x.Payload["text"].StringValue.ToString() == document);
                        if (point == null)
                        {
                            continue;
                        }
                        var id = (int)point.Id.Num;
                        result = [(ChatRecord.GetChatRecordById(db, id), score), .. result];
                    }
                    return result;
                }
                else
                {
                    (ChatRecord records, float score)[] result = [];
                    foreach (var item in searchResult.OrderByDescending(x => x.Score).Take(AppConfig.MaxMemoryCount))
                    {
                        var id = (int)item.Id.Num;
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
