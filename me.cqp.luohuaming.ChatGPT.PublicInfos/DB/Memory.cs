using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.DB
{
    [SugarTable]
    public class MemoryNodes
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(IsNullable = false)]
        public string Topic { get; set; }

        [SugarColumn(IsJson = true, Length = 65535)]
        public double[] TopicEmbedding { get; set; } = [];

        [SugarColumn(IsJson = true, Length = 65535)]
        public int[] RelationRecords { get; set; } = [];

        public long GroupId { get; set; }

        public long QQ { get; set; }

        public DateTime LastModify { get; set; }

        public DateTime CreateTime { get; set; }

        public void Update()
        {
            using var db = SQLHelper.GetInstance();
            db.Updateable(this).ExecuteCommand();
        }

        public static MemoryNodes GetOrCreateNode(long groupId, long qq, string topic, int recordId, double[] embedding)
        {
            using var db = SQLHelper.GetInstance();
            var node = db.Queryable<MemoryNodes>().First(x => x.Topic == topic);
            if (node == null)
            {
                node = new MemoryNodes
                {
                    Topic = topic,
                    CreateTime = DateTime.Now,
                    LastModify = DateTime.Now,
                    TopicEmbedding = embedding,
                    RelationRecords = [recordId],
                    GroupId = groupId,
                    QQ = qq
                };
                node.Id = db.Insertable(node).ExecuteReturnIdentity();
            }

            return node;
        }

        public ChatRecord[] GetRecords()
        {
            using var db = SQLHelper.GetInstance();
            return db.Queryable<ChatRecord>().Where(x => RelationRecords.Contains(x.Id)).ToArray();
        }
    }

    [SugarTable]
    public class MemoryEdges
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        public int NodeA { get; set; }

        public int NodeB { get; set; }

        [SugarColumn(DefaultValue = "1")]
        public double Strength { get; set; }

        public static void Connect(MemoryNodes nodeA, MemoryNodes nodeB, double similarity = -1)
        {
            if (nodeA.Id == nodeB.Id)
            {
                return;
            }
            using var db = SQLHelper.GetInstance();
            var existEdge = db.Queryable<MemoryEdges>().First(x => x.NodeA == nodeA.Id && x.NodeB == nodeB.Id
                    || x.NodeB == nodeA.Id && x.NodeA == nodeB.Id);
            if (existEdge != null)
            {
                existEdge.Strength++;
                db.Updateable(existEdge).ExecuteCommand();
            }
            else
            {
                var edge = new MemoryEdges()
                {
                    NodeA = nodeA.Id,
                    NodeB = nodeB.Id,
                    Strength = similarity < 0 ? 1 : similarity * 10
                };
                db.Insertable(edge).ExecuteCommand();
            }
        }
    }

    public static class Memory
    {
        private static List<MemoryNodes> CacheNodes { get; set; } = [];

        public static void AddMemory(ChatRecord record)
        {
            if (record.IsEmpty || record.IsImage)
            {
                return;
            }
            var topic = record.Topics;
            if (topic.Length == 0)
            {
                return;
            }
            MemoryNodes lastTopicNode = null;
            foreach (var item in topic)
            {
                var inCacheNode = CacheNodes.FirstOrDefault(x => x.Topic == item);
                var embedding = inCacheNode == null ? Embedding.GetEmbedding(item) : inCacheNode.TopicEmbedding;

                var topicNode = MemoryNodes.GetOrCreateNode(record.GroupID, record.QQ, item, record.Id, embedding);
                var relationNodes = GetRecommandNodes(record.GroupID, record.QQ, embedding, 0.8);
                if (!CacheNodes.Contains(topicNode))
                {
                    CacheNodes.Add(topicNode);
                }

                foreach ((double similarity, MemoryNodes node) in relationNodes)
                {
                    if (!node.RelationRecords.Contains(record.Id))
                    {
                        node.RelationRecords = [record.Id, .. node.RelationRecords];
                        node.LastModify = DateTime.Now;
                        node.Update();
                    }

                    MemoryEdges.Connect(topicNode, node, similarity);
                    MainSave.CQLog.Debug("记忆节点", $"{topicNode.Topic} 与 {node.Topic} 节点连接，相似度为 {similarity}");
                }
                if (lastTopicNode != null)
                {
                    MemoryEdges.Connect(topicNode, lastTopicNode);
                }
                lastTopicNode = topicNode;
            }
        }

        public static (double similarity, MemoryNodes nodes)[] GetMemories(long groupId, long qq, string[] topics)
        {
            (double similarity, MemoryNodes nodes)[] result = [];
            foreach (var item in topics)
            {
                var embedding = Embedding.GetEmbedding(item);
                result = [.. GetRecommandNodes(groupId, qq, embedding, AppConfig.MinMemorySimilarty), .. result];
            }

            return result;
        }

        public static ((double similarity, MemoryNodes nodes)[] memories, string[] topics) GetMemories(ChatRecord record)
        {
            var topic = record.Topics;
            if (topic.Length == 0)
            {
                return ([], []);
            }
            return (GetMemories(record.GroupID, record.QQ, topic), topic);
        }

        public static double CalcMemoryActivateRate(ChatRecord record)
        {
            return CalcMemoryActivateRate(record.Topics, GetMemories(record).memories);
        }

        public static double CalcMemoryActivateRate(string[] topics, (double similarity, MemoryNodes nodes)[] nodes)
        {
            if (nodes.Length == 0)
            {
                return 0;
            }
            double topicSimilarity = 1.0 * topics.Length / nodes.Length;
            double averageSimilarity = nodes.Sum(x => x.similarity) / nodes.Length;

            return (topicSimilarity + averageSimilarity) / 2;
        }

        private static (double Similarity, MemoryNodes Node)[] GetRecommandNodes(long groupId, long qq, double[] embedding, double minSimilarity)
        {
            return GetAllNodes().AsParallel()
                .Where(x => x.GroupId == groupId && x.QQ == qq)
                .Select(x => new { Similarity = Picture.CosineSimilarity(embedding, x.TopicEmbedding), Node = x })
                .Where(x => x.Similarity > minSimilarity)
                .OrderByDescending(x => x.Similarity)
                .Select(x => (x.Similarity, x.Node))
                .ToArray();
        }

        private static List<MemoryNodes> GetAllNodes()
        {
            if (CacheNodes == null)
            {
                using var db = SQLHelper.GetInstance();
                CacheNodes = db.Queryable<MemoryNodes>().ToList();
            }
            return CacheNodes;
        }
    }
}
