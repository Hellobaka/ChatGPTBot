using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model;
using OpenAI.Chat;
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

        [SugarColumn(IsJson = true, Length = 65535)]
        public float[] Embedding { get; set; } = [];

        public string Description { get; set; }

        public bool IsEmoji { get; set; }

        public bool IsDeleted { get; set; }

        public int UseCount { get; set; }

        public DateTime AddTime { get; set; }

        public static event Action<Picture> OnPictureAdded;
       
        public static event Action<Picture> OnPictureRemoved;

        [SugarColumn(IsIgnore = true)]
        public static Dictionary<string, Picture> Cache { get; set; } = [];

        public static Picture? InsertImageDescription(string filePath, string hash, bool emoji, string description)
        {
            if (!File.Exists(filePath) || string.IsNullOrEmpty(hash))
            {
                MainSave.CQLog.Error("图片缓存", $"文件不存在或Hash为空，无法记录");
                return null;
            }
            filePath = CommonHelper.GetRelativePath(filePath, MainSave.ImageDirectory);

            Picture picture = new()
            {
                AddTime = DateTime.Now,
                Description = description,
                FilePath = filePath,
                Hash = hash.ToUpper(),
                IsEmoji = emoji,
            };
            if (emoji)
            {
                var embedding = API.Embedding.GetEmbedding(description);
                if (embedding.Length == 0)
                {
                    MainSave.CQLog.Error("图片记录", $"由于获取Embedding失败，无法记录");
                    return null;
                }
                picture.Embedding = embedding;
            }

            if (Cache.ContainsKey(picture.Hash))
            {
                MainSave.CQLog?.Info("图片记录", $"重复的Hash: {picture.Hash}");
                Cache[picture.Hash] = picture;
            }
            else
            {
                Cache.Add(picture.Hash, picture);
            }

            using var db = SQLHelper.GetInstance();
            var exist = db.Queryable<Picture>().First(x => x.Hash == picture.Hash);
            if (exist != null)
            {
                picture.Id = exist.Id;
                db.Updateable(picture).ExecuteCommand();
            }
            else
            {
                picture.Id = db.Insertable(picture).ExecuteReturnIdentity();
            }

            OnPictureAdded?.Invoke(picture);
            return picture;
        }

        public static (Picture? picture, string? cachePath, string? hash) TryGetImageHash(CQCode img, bool isEmoji)
        {
            if (!img.IsImageCQCode || (!isEmoji && AppConfig.IgnoreNotEmoji))
            {
                return (null, null, null);
            }
            var filePath = PictureDescriber.ReceiveImage(img);
            if (!File.Exists(filePath))
            {
                return (null, filePath, null);
            }

            string hash = PictureDescriber.ComputeImageHash(filePath);
            if (Cache.TryGetValue(hash, out Picture picture))
            {
                return (picture, filePath, hash);
            }
            using var db = SQLHelper.GetInstance();

            return (db.Queryable<Picture>().First(x => x.Hash == hash), filePath, hash);
        }

        public static List<(Picture emoji, double similarity)> GetRecommandEmoji(string text)
        {
            var emotion = Chat.GetChatResult(AppConfig.ImageDescriberUrl, AppConfig.ImageDescriberApiKey,
                [
                    new SystemChatMessage(string.Format($"这是你将要发送的消息内容:{text}\r\n若要为其配上表情包，请你输出这个表情包应该表达怎样的情感，应该给人什么样的感觉，不要太简洁也不要太长\r\n，注意不要输出任何对消息内容的分析内容，只输出\"一种什么样的感觉\"中间的形容词部分。"))
                ], AppConfig.ImageDescriberModelName, Chat.Purpose.表情包推荐);

            if (emotion == Chat.ErrorMessage)
            {
                MainSave.CQLog?.Info("表情包推荐", $"请求失败");
                return [];
            }
            MainSave.CQLog?.Info("表情包推荐", $"转换后的情感：{emotion}");
            var embedding = API.Embedding.GetEmbedding(emotion);

            return GetRecommandEmoji(embedding, AppConfig.RecommendEmojiCount);
        }

        public static List<(Picture emoji, double similarity)> GetRecommandEmoji(float[] embedding, int count = 3)
        {
            if (embedding.Length == 0)
            {
                MainSave.CQLog?.Info("表情包推荐", $"Embedding结果为空，无法推荐");
                return [];
            }

            var l = Cache.AsParallel()
                .Select(x => new { Image = x.Value, Similarity = CosineSimilarity(embedding, x.Value.Embedding) })
                .OrderByDescending(x => x.Similarity)
                .Where(x => x.Similarity > AppConfig.MinEmojiRecommendScore)
                .Take(count);
            foreach (var item in l)
            {
                CommonHelper.DebugLog("表情包结果", $"{item.Image.Hash}[{item.Similarity}]: {item.Image.Description}");
            }
            return l.Select(x => (x.Image, x.Similarity)).ToList();
        }

        public void Update()
        {
            using var db = SQLHelper.GetInstance();
            db.Updateable(this).ExecuteCommand();
        }

        public void Delete()
        {
            IsDeleted = true;
            Cache.Remove(Hash);
            Update();
        }

        public static void Remove(Picture picture)
        {
            if (Cache.ContainsKey(picture.Hash))
            {
                Cache.Remove(picture.Hash);
            }
            OnPictureRemoved?.Invoke(picture);
        }

        /// <summary>
        /// 余弦相似度
        /// </summary>
        /// <param name="vectorA">EmbeddingA</param>
        /// <param name="vectorB">EmbeddingB</param>
        public static double CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length)
            {
                // 向量的长度必须相同
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

        public static void InitCache()
        {
            if (Cache.Count == 0)
            {
                using var db = SQLHelper.GetInstance();
                var emojis = db.Queryable<Picture>().Where(x => x.IsEmoji && !x.IsDeleted).ToList();
                foreach (var emoji in emojis)
                {
                    if (!File.Exists(emoji.FilePath) && !File.Exists(Path.Combine(MainSave.ImageDirectory, emoji.FilePath)))
                    {
                        MainSave.CQLog?.Info("表情包缓存", $"{emoji.Hash} 文件已不存在，标记为删除");
                        emoji.IsDeleted = true;
                        emoji.Update();
                        continue;
                    }
                    if (Cache.ContainsKey(emoji.Hash))
                    {
                        MainSave.CQLog?.Info("表情包缓存", $"重复的Hash: {emoji.Hash}");
                        Cache[emoji.Hash] = emoji;
                    }
                    else
                    {
                        Cache.Add(emoji.Hash, emoji);
                    }
                }
                if (Cache.Count > 0)
                {
                    MainSave.CQLog?.Info("表情包缓存", $"已加载 {Cache.Count} 个表情包缓存");
                }
            }
        }
    }
}
