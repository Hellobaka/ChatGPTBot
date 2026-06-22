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
