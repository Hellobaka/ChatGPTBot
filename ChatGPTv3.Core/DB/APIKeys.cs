using SqlSugar;

namespace ChatGPTv3.Core.DB;

/// <summary>
/// API protocol format for an endpoint. Determines which local client is used.
/// </summary>
public enum ApiFormat
{
    /// <summary>OpenAI-compatible Chat Completions endpoint (default).</summary>
    OpenAI = 0,

    /// <summary>Anthropic Messages API endpoint.</summary>
    Anthropic = 1,

    /// <summary>OpenAI Responses API endpoint.</summary>
    Responses = 2
}

/// <summary>
/// Model capability flags — determines which purposes a model can serve.
/// </summary>
[Flags]
public enum ModelCapability
{
    Chat = 1 << 0,
    Image = 1 << 1,
    Embedding = 1 << 2,
    Rerank = 1 << 3
}

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

    /// <summary>Cumulative cost in RMB (calculated from token usage × pricing).</summary>
    public decimal TotalConsume { get; set; }

    /// <summary>Whether to use Tencent Cloud TC3-HMAC-SHA256 signing for this endpoint.</summary>
    public bool UseTencentSign { get; set; }

    /// <summary>
    /// Whether to attach the built-in web_search tool for Anthropic / Responses endpoints.
    /// </summary>
    public bool EnableWebSearch { get; set; } = true;

    /// <summary>API protocol format for this endpoint (OpenAI / Anthropic / Responses).</summary>
    public ApiFormat ApiFormat { get; set; } = ApiFormat.OpenAI;

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

    /// <summary>What this model can do (chat, image, embedding, rerank).</summary>
    public ModelCapability Capabilities { get; set; } = ModelCapability.Chat;
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

/// <summary>
/// DB-persisted purpose → (APIKey, LLMModelConfig) binding. One purpose can have multiple bindings.
/// </summary>
[SugarTable("PurposeBinding")]
public class PurposeBinding
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>Purpose name: Chat, Reply, Splitter, ImageDescriber, Summarizer, Diary, Embedding, Rerank.</summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>Foreign key to APIKey.</summary>
    public int APIKeyId { get; set; }

    /// <summary>Foreign key to LLMModelConfig.</summary>
    public int LLMModelConfigId { get; set; }
}
