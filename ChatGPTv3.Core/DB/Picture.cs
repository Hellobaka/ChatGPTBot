using ChatGPTv3.Core.Api;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.Model;
using SqlSugar;

namespace ChatGPTv3.Core.DB;

/// <summary>
/// Image/emoji metadata cache.
/// </summary>
[SugarTable("Picture")]
public class Picture
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>MD5 hash of the image data.</summary>
    public string Md5 { get; set; } = string.Empty;

    /// <summary>File path relative to the image directory.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Whether this is an emoji (expression image).</summary>
    public bool IsEmoji { get; set; }

    /// <summary>LLM-generated description of the image.</summary>
    [SugarColumn(ColumnDataType = "text")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Emotion tags extracted from the image.</summary>
    public string Emotions { get; set; } = string.Empty;

    /// <summary>Download URL for the image (from QQ).</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Number of times this emoji was recommended/used.</summary>
    public int UseCount { get; set; }

    /// <summary>Soft-delete flag.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Last time this image was used (for cleanup policy).</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>Creation/insertion time.</summary>
    public DateTime Time { get; set; } = DateTime.Now;

    // ── CRUD ─────────────────────────────────────────────

    public static Picture? FindByHash(string md5)
    {
        using var db = SQLiteManager.GetInstance();
        return db.Queryable<Picture>().First(p => p.Md5 == md5.ToUpper());
    }

    /// <summary>
    /// Search for emoji images matching an emotion description.
    /// Uses Qdrant for initial recall, then optionally reranks via RerankService.
    /// Returns pictures sorted by relevance, or empty if none found.
    /// </summary>
    public static async Task<List<(Picture picture, float score)>> GetRecommendEmojiAsync(
        string emotion, int topK = 3)
    {
        if (MemoryManager.Qdrant == null || string.IsNullOrWhiteSpace(emotion))
        {
            return [];
        }

        // ── Step 1: Broad recall from Qdrant ──
        int recallCount = AppConfig.EnableRerank ? topK * 5 : topK;
        var results = MemoryManager.Qdrant.Search(emotion, QdrantService.ImageCollectionName, recallCount);
        if (results.Count == 0)
        {
            return [];
        }

        var candidates = new List<(Picture picture, float score)>();
        foreach (var (hash, desc, _, score) in results)
        {
            var picture = FindByHash(hash);
            if (picture != null && picture.IsEmoji && !picture.IsDeleted)
            {
                candidates.Add((picture, score));
            }
        }

        if (candidates.Count == 0)
        {
            return [];
        }

        // ── Step 2: Rerank (if enabled) ──
        if (AppConfig.EnableRerank && candidates.Count > topK)
        {
            var descriptions = candidates.Select(c => c.picture.Description).ToList();
            var reranked = await RerankService.RerankAsync(emotion, descriptions);
            if (reranked.Count > 0)
            {
                var rerankedCandidates = reranked
                    .Take(topK)
                    .Select(r => (candidates[r.index].picture, r.score))
                    .ToList();
                return rerankedCandidates;
            }
        }

        return candidates.OrderByDescending(c => c.score).Take(topK).ToList();
    }

    public static void Upsert(Picture picture)
    {
        using var db = SQLiteManager.GetInstance();
        var exist = db.Queryable<Picture>().First(p => p.Md5 == picture.Md5.ToUpper());
        if (exist != null)
        {
            picture.Id = exist.Id;
            db.Updateable(picture).ExecuteCommand();
        }
        else
        {
            picture.Id = db.Insertable(picture).ExecuteReturnIdentity();
        }
    }
}