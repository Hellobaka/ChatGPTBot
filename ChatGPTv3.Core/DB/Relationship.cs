using SqlSugar;

namespace ChatGPTv3.Core.DB;

/// <summary>
/// User-to-bot relationship and favorability tracking.
/// </summary>
[SugarTable("Relationship")]
public class Relationship
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// Group ID (0 for private chat relationships).
    /// </summary>
    public long GroupID { get; set; }

    /// <summary>
    /// User's QQ number.
    /// </summary>
    public long QQ { get; set; }

    /// <summary>
    /// User's display name (nickname or group card).
    /// </summary>
    public string NickName { get; set; } = string.Empty;

    /// <summary>
    /// Favorability score (0-100).
    /// </summary>
    public int Favorability { get; set; } = 50;

    /// <summary>
    /// Total number of interactions.
    /// </summary>
    public int InteractionCount { get; set; }

    /// <summary>
    /// Last interaction time.
    /// </summary>
    public DateTime LastInteractionTime { get; set; } = DateTime.Now;

    /// <summary>
    /// When the relationship info was last updated from QQ API.
    /// </summary>
    public DateTime LastUpdateTime { get; set; } = DateTime.Now;
}
