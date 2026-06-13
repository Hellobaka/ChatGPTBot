using SqlSugar;

namespace ChatGPTv3.Core.DB;

/// <summary>
/// Token usage tracking record.
/// </summary>
[SugarTable("Usage")]
public class TokenUsage
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// The API endpoint URL used.
    /// </summary>
    public string EndPoint { get; set; } = string.Empty;

    /// <summary>
    /// The model name used.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// The purpose of this API call (e.g., "聊天", "记忆提取").
    /// </summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// The API key used (masked or truncated for privacy).
    /// </summary>
    public string APIKeyHint { get; set; } = string.Empty;

    /// <summary>
    /// Prompt tokens consumed.
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    /// Cached prompt tokens (cache hits, not billed at full price).
    /// </summary>
    public int CachedPromptTokens { get; set; }

    /// <summary>
    /// Completion tokens consumed.
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// Total tokens consumed.
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// Timestamp of the API call.
    /// </summary>
    public DateTime Time { get; set; } = DateTime.Now;
}
