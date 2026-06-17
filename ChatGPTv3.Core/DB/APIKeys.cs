using SqlSugar;

namespace ChatGPTv3.Core.DB;

/// <summary>
/// API key record for LLM service authentication.
/// Aligned with v2 schema for backward compatibility.
/// </summary>
[SugarTable("APIKeys")]
public class APIKey
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>Display name for this key.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The API endpoint base URL.</summary>
    public string EndPoint { get; set; } = string.Empty;

    /// <summary>The API key / Bearer token.</summary>
    [SugarColumn(ColumnName = "APIKey")]
    public string Key { get; set; } = string.Empty;

    /// <summary>Total tokens consumed by this key.</summary>
    [SugarColumn(ColumnName = "TokenConsume")]
    public long TotalTokens { get; set; }

    // TODO: 需要计算
    /// <summary>Cumulative cost in RMB (calculated from token usage × pricing).</summary>
    public decimal TotalConsume { get; set; }

    /// <summary>Whether to use Tencent Cloud TC3-HMAC-SHA256 signing for this endpoint.</summary>
    public bool UseTencentSign { get; set; }

    /// <summary>Available models for this endpoint.</summary>
    [Navigate(NavigateType.OneToMany, nameof(LLMModelConfig.APIKeyId))]
    public List<LLMModelConfig>? AvailableModels { get; set; }
}

/// <summary>
/// LLM model configuration for a specific API endpoint.
/// </summary>
[SugarTable("LLMModel")]
public class LLMModelConfig
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>Foreign key to APIKey.</summary>
    public int APIKeyId { get; set; }

    /// <summary>Model name (e.g., "gpt-4o", "deepseek-chat").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether this model is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Input price per 1M tokens (RMB).</summary>
    public decimal InputPricePer1M { get; set; }

    /// <summary>Output price per 1M tokens (RMB).</summary>
    public decimal OutputPricePer1M { get; set; }

    /// <summary>Cache-hit price per 1M tokens (RMB) — for prompt caching discounts.</summary>
    public decimal CachePricePer1M { get; set; }
}

/// <summary>
/// Associates an API key + model with a specific purpose (chat, reply, memory, etc.).
/// Serialized in Config.json, resolved from DB at startup.
/// </summary>
public class APIKeyPurpose
{
    public int Id { get; set; }

    [SugarColumn(IsIgnore = true)]
    public APIKey? Key { get; set; }

    [SugarColumn(IsIgnore = true)]
    public LLMModelConfig? Model { get; set; }
}
