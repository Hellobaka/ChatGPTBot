using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.DB
{
    [SugarTable]
    public class Picture
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        public string Hash { get; set; }

        public string FilePath { get; set; }

        public double[] Embedding { get; set; } = [];

        public string Description { get; set; }

        public bool IsEmoji { get; set; }

        public int UseCount { get; set; }

        public DateTime AddTime { get; set; }

        private static Dictionary<string, Picture> Cache { get; set; } = [];

        public static void InsertImageDescription(CQCode cqcode, bool emoji, string description)
        {
            if (string.IsNullOrEmpty(description))
            {
                return;
            }

            string path = PictureDescriber.ReceiveImage(cqcode);
            if (!File.Exists(path))
            {
                return;
            }

            Picture picture = new()
            {
                AddTime = DateTime.Now,
                Description = description,
                FilePath = path,
                Hash = Path.GetFileNameWithoutExtension(cqcode.Items["file"]).ToUpper(),
                IsEmoji = emoji,
            };
            if (emoji)
            {
                var embedding = API.Embedding.GetEmbedding(description);
                picture.Embedding = embedding;
            }

            if (Cache.ContainsKey(picture.Hash))
            {
                Cache[picture.Hash] = picture;
            }
            else
            {
                Cache.Add(picture.Hash, picture);
            }

            var db = SQLHelper.GetInstance();
            db.Insertable(picture).ExecuteCommand();
        }

        public static Picture? GetPictureByHash(CQCode img)
        {
            if (!img.IsImageCQCode)
            {
                return null;
            }
            string hash = Path.GetFileNameWithoutExtension(img.Items["file"]).ToUpper();
            if (Cache.TryGetValue(hash, out Picture picture))
            {
                return picture;
            }
            var db = SQLHelper.GetInstance();

            return db.Queryable<Picture>().First(x => x.Hash == hash);
        }

        public static List<Picture> GetRecommandEmoji(string text)
        {
            var embedding = API.Embedding.GetEmbedding(text);

            return GetRecommandEmoji(embedding);
        }

        public static List<Picture> GetRecommandEmoji(double[] embedding, int count = 3)
        {
            if (embedding.Length == 0)
            {
                return [];
            }

            var list = new List<Picture>();
            if (Cache.Count == 0)
            {
                var db = SQLHelper.GetInstance();
                var emojis = db.Queryable<Picture>().Where(x => x.IsEmoji).ToList();
                foreach (var emoji in emojis)
                {
                    if (Cache.ContainsKey(emoji.Hash))
                    {
                        Cache[emoji.Hash] = emoji;
                    }
                    else
                    {
                        Cache.Add(emoji.Hash, emoji);
                    }
                }
                if (Cache.Count > 0)
                {
                    MainSave.CQLog.Info("表情包缓存", $"已加载 {Cache.Count} 个表情包缓存");
                }
            }

            return Cache.AsParallel()
                .Select(x => new { Image = x.Value, Similarity = CosineSimilarity(embedding, x.Value.Embedding) })
                .OrderByDescending(x => x.Similarity)
                .Where(x => x.Similarity > 0)
                .Select(x => x.Image)
                .Take(count).ToList();
        }

        public void Update()
        {
            using var db = SQLHelper.GetInstance();
            db.Updateable(this).ExecuteCommand();
        }

        /// <summary>
        /// 余弦相似度
        /// </summary>
        /// <param name="vectorA">EmbeddingA</param>
        /// <param name="vectorB">EmbeddingB</param>
        public static double CosineSimilarity(double[] vectorA, double[] vectorB)
        {
            if (vectorA.Length != vectorB.Length)
            {
                Debugger.Break();// 向量的长度必须相同
                return 0;
            }

            double dotProduct = 0.0;
            double magnitudeA = 0.0;
            double magnitudeB = 0.0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += Math.Pow(vectorA[i], 2);
                magnitudeB += Math.Pow(vectorB[i], 2);
            }

            magnitudeA = Math.Sqrt(magnitudeA);
            magnitudeB = Math.Sqrt(magnitudeB);

            return magnitudeA == 0 || magnitudeB == 0 ? 0.0 : dotProduct / (magnitudeA * magnitudeB);
        }
    }
}
