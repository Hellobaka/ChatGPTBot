using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Utilities;

namespace ChatGPTv3.Core.Config;

/// <summary>
/// Application configuration. All properties loaded from Config.json with defaults.
/// Supports hot reload via ConfigManager.OnConfigReloaded.
/// </summary>
public static class AppConfig
{
    // ── API Key Purpose Lists ────────────────────────────
    public static List<APIKeyPurpose> ChatAPIKeyId { get; set; } = [];
    public static List<APIKeyPurpose> ReplyAPIKeyId { get; set; } = [];
    public static List<APIKeyPurpose> MemoryAPIKeyId { get; set; } = [];
    public static List<APIKeyPurpose> SplitterApiKeyId { get; set; } = [];
    public static List<APIKeyPurpose> ImageDescriberApiKeyId { get; set; } = [];
    public static List<APIKeyPurpose> EmbeddingApiKeyId { get; set; } = [];
    public static List<APIKeyPurpose> RerankApiKeyId { get; set; } = [];

    // ── Chat Configuration ────────────────────────────────
    public static int ChatMaxTokens { get; set; } = 3000;
    public static float ChatTemperature { get; set; } = 1f;
    public static int ChatTimeout { get; set; } = 30000;
    public static int ReplyTimeout { get; set; } = 5000;
    public static int MemoryTimeout { get; set; } = 30000;
    public static int SplitterTimeout { get; set; } = 30000;
    public static int ImageDescriberTimeout { get; set; } = 30000;
    public static int EmbeddingTimeout { get; set; } = 3000;
    public static int RerankTimeout { get; set; } = 3000;

    // ── Streaming & Reply ─────────────────────────────────
    public static bool StreamMode { get; set; } = true;
    public static bool EnableGroupReply { get; set; }

    // ── Bot Identity ──────────────────────────────────────
    public static string BotName { get; set; } = "ChatGPT";
    public static List<string> BotNicknames { get; set; } = [];
    public static List<long> MasterQQ { get; set; } = [];

    // ── Group/Person Lists ────────────────────────────────
    public static bool IsGroupBlackList { get; set; }
    public static bool IsPersonBlackList { get; set; }
    public static List<long> GroupList { get; set; } = [];
    public static List<long> PersonList { get; set; } = [];
    public static List<string> Filters { get; set; } = ["[CQ:", "&#"];

    // ── Prompts ───────────────────────────────────────────
    public static string GroupPrompt { get; set; }
        = "胆小害羞，说话简单意骇，心情好时会使用emoji与颜文字。," +
          "现在请你读读之前的聊天记录，然后给出日常且口语化的回复，" +
          "平淡一些，尽量简短一些。感觉有趣也可以直接复读消息。" +
          "请注意把握聊天内容，不要刻意突出自身学科背景，" +
          "不要回复的太有条理，可以有个性，请回复时不要过多提及自身的背景。";

    public static string PrivatePrompt { get; set; }
        = "胆小害羞，说话简单意骇，心情好时会使用emoji与颜文字。" +
          "现在请你读读之前的聊天记录，然后给出日常且口语化的回复，尽量简短一些。" +
          "请注意把握聊天内容，不要刻意突出自身学科背景，" +
          "不要回复的太有条理，可以有个性，请回复时不要过多提及自身的背景。";

    // ── Vision ────────────────────────────────────────────
    public static bool EnableVision { get; set; } = true;
    public static bool EnableVisionWhenMentioned { get; set; } = true;

    // ── Splitting ─────────────────────────────────────────
    public static bool EnableSplitter { get; set; }
    public static bool EnableSplitterRemoveMarkdown { get; set; }
    public static int SplitterMaxLines { get; set; } = 3;
    public static bool SplitterRegexFirst { get; set; }
    public static bool SplitterRegexRemovePunctuation { get; set; }
    public static int SplitterSimulateTypeSpeed { get; set; } = 100;
    public static bool EnableSplitterRandomDelay { get; set; } = true;
    public static int SplitterRandomDelayMin { get; set; } = 1000;
    public static int SplitterRandomDelayMax { get; set; } = 4500;
    public static int SplitterMinLength { get; set; } = 20;

    // ── Emoji ─────────────────────────────────────────────
    public static bool EnableEmojiPassiveSend { get; set; }
    public static bool EnableEmojiActiveSend { get; set; }
    public static int EmojiSendProbability { get; set; } = 10;
    public static bool IgnoreNotEmoji { get; set; } = true;
    public static bool RandomSendEmoji { get; set; } = true;
    public static int RecommendEmojiCount { get; set; } = 5;
    public static double MinEmojiRecommendScore { get; set; } = 0.5;
    public static int NonEmojiPictureSaveDays { get; set; } = 7;

    // ── Context & Memory ──────────────────────────────────
    public static int ContextMaxLength { get; set; } = 20;
    public static bool EnableQdrant { get; set; } = true;
    public static bool QdrantSearchOnlyPerson { get; set; }
    public static double MinMemorySimilarity { get; set; } = 0.8;
    public static int MaxMemoryCount { get; set; } = 5;
    public static int MemoryDimensions { get; set; } = 1024;
    public static int ShortTermMemoryMaxUseCount { get; set; } = 30;
    public static int MemoryExtractionCount { get; set; } = 30;

    // ── Reply Willingness ─────────────────────────────────
    public static double ReplyWillingAmplifier { get; set; } = 1.0;
    public static bool EnableLLMCheckShouldResponse { get; set; }

    // ── Schedule ──────────────────────────────────────────
    public static bool EnableSchedules { get; set; } = true;
    public static string SchedulePrompt { get; set; } = "喜欢打各种游戏，为人热情积极向上，作息健康，10%概率熬夜";
    public static string DefaultSchedule { get; set; } = "摸鱼";

    // ── Response Processing ───────────────────────────────
    public static string ChatEmptyResponse { get; set; } = "<EMPTY>";
    public static bool LogThinkBlock { get; set; } = true;
    public static bool RemoveThinkBlock { get; set; } = true;

    // ── Record Handling ───────────────────────────────────
    public static bool RecordNotExistSkipResponse { get; set; } = true;
    public static bool CanCallFrameIfRecordNotExist { get; set; } = true;

    // ── MCP ───────────────────────────────────────────────
    public static bool EnableMCP { get; set; } = true;
    public static int MaxToolCallCountEachTurn { get; set; } = 5;
    public static int AbortToolCallCountEachTurn { get; set; } = 7;

    // ── Embedding ─────────────────────────────────────────
    public static string EmbeddingUrl { get; set; } = "https://api.openai.com/v1/embeddings";
    public static string EmbeddingModelName { get; set; } = "text-embedding-ada-002";

    // ── Rerank ────────────────────────────────────────────
    public static bool EnableRerank { get; set; } = true;
    public static string RerankUrl { get; set; } = "https://lkeap.tencentcloudapi.com";
    public static string RerankModelName { get; set; } = "lke-reranker-base";

    // ── Tencent Cloud ─────────────────────────────────────
    public static bool EnableTencentSign { get; set; }
    public static string TencentSecretId { get; set; } = "";
    public static string TencentSecretKey { get; set; } = "";

    // ── Qdrant ────────────────────────────────────────────
    public static string QdrantHost { get; set; } = "localhost";
    public static ushort QdrantPort { get; set; } = 6333;
    public static string QdrantAPIKey { get; set; } = "";

    // ── Relationship ──────────────────────────────────────
    public static int RelationshipUpdateTime { get; set; } = 7;

    // ── Debug ─────────────────────────────────────────────
    public static bool DebugMode { get; set; }

    // ═══════════════════════════════════════════════════════
    //  Initialization
    // ═══════════════════════════════════════════════════════

    public static void Init()
    {
        ConfigManager.DisableHotReload();

        // API key purpose lists
        ChatAPIKeyId = ConfigManager.GetConfig("ChatAPIKeyId", new List<APIKeyPurpose>());
        ReplyAPIKeyId = ConfigManager.GetConfig("ReplyAPIKeyId", new List<APIKeyPurpose>());
        MemoryAPIKeyId = ConfigManager.GetConfig("MemoryAPIKeyId", new List<APIKeyPurpose>());
        SplitterApiKeyId = ConfigManager.GetConfig("SplitterApiKeyId", new List<APIKeyPurpose>());
        ImageDescriberApiKeyId = ConfigManager.GetConfig("ImageDescriberApiKeyId", new List<APIKeyPurpose>());
        EmbeddingApiKeyId = ConfigManager.GetConfig("EmbeddingApiKeyId", new List<APIKeyPurpose>());
        RerankApiKeyId = ConfigManager.GetConfig("RerankApiKeyId", new List<APIKeyPurpose>());

        // Chat config
        ChatMaxTokens = ConfigManager.GetConfig("ChatMaxTokens", 3000);
        ChatTemperature = ConfigManager.GetConfig("ChatTemperature", 1f);
        ChatTimeout = ConfigManager.GetConfig("ChatTimeout", 30000);
        ReplyTimeout = ConfigManager.GetConfig("ReplyTimeout", 5000);
        MemoryTimeout = ConfigManager.GetConfig("MemoryTimeout", 30000);
        SplitterTimeout = ConfigManager.GetConfig("SplitterTimeout", 30000);
        ImageDescriberTimeout = ConfigManager.GetConfig("ImageDescriberTimeout", 30000);
        EmbeddingTimeout = ConfigManager.GetConfig("EmbeddingTimeout", 3000);
        RerankTimeout = ConfigManager.GetConfig("RerankTimeout", 3000);

        // Streaming & reply
        StreamMode = ConfigManager.GetConfig("StreamMode", true);
        EnableGroupReply = ConfigManager.GetConfig("EnableGroupReply", false);

        // Bot identity
        BotName = ConfigManager.GetConfig("BotName", "ChatGPT");
        BotNicknames = ConfigManager.GetConfig("BotNicknames", new List<string> { BotName });
        MasterQQ = ConfigManager.GetConfig("MasterQQ", new List<long>());

        // Lists
        IsGroupBlackList = ConfigManager.GetConfig("IsGroupBlackList", false);
        IsPersonBlackList = ConfigManager.GetConfig("IsPersonBlackList", false);
        GroupList = ConfigManager.GetConfig("GroupList", new List<long>());
        PersonList = ConfigManager.GetConfig("PersonList", new List<long>());
        Filters = ConfigManager.GetConfig("Filter", new List<string> { "[CQ:", "&#" });

        // Prompts
        GroupPrompt = ConfigManager.GetConfig("GroupPrompt", GroupPrompt);
        PrivatePrompt = ConfigManager.GetConfig("PrivatePrompt", PrivatePrompt);

        // Vision
        EnableVision = ConfigManager.GetConfig("EnableVision", true);
        EnableVisionWhenMentioned = ConfigManager.GetConfig("EnableVisionWhenMentioned", true);

        // Splitting
        EnableSplitter = ConfigManager.GetConfig("EnableSplitter", false);
        EnableSplitterRemoveMarkdown = ConfigManager.GetConfig("EnableSplitterRemoveMarkdown", true);
        SplitterMaxLines = ConfigManager.GetConfig("SplitterMaxLines", 3);
        SplitterRegexFirst = ConfigManager.GetConfig("SplitterRegexFirst", false);
        SplitterRegexRemovePunctuation = ConfigManager.GetConfig("SplitterRegexRemovePunctuation", false);
        SplitterSimulateTypeSpeed = ConfigManager.GetConfig("SplitterSimulateTypeSpeed", 100);
        EnableSplitterRandomDelay = ConfigManager.GetConfig("EnableSplitterRandomDelay", true);
        SplitterRandomDelayMin = ConfigManager.GetConfig("SplitterRandomDelayMin", 1000);
        SplitterRandomDelayMax = ConfigManager.GetConfig("SplitterRandomDelayMax", 4500);
        SplitterMinLength = ConfigManager.GetConfig("SplitterMinLength", 20);

        // Emoji
        EnableEmojiPassiveSend = ConfigManager.GetConfig("EnableEmojiPassiveSend", false);
        EnableEmojiActiveSend = ConfigManager.GetConfig("EnableEmojiActiveSend", false);
        EmojiSendProbability = ConfigManager.GetConfig("EmojiSendProbability", 10);
        IgnoreNotEmoji = ConfigManager.GetConfig("IgnoreNotEmoji", true);
        RandomSendEmoji = ConfigManager.GetConfig("RandomSendEmoji", true);
        RecommendEmojiCount = ConfigManager.GetConfig("RecommendEmojiCount", 5);
        MinEmojiRecommendScore = ConfigManager.GetConfig("MinEmojiRecommendScore", 0.5);
        NonEmojiPictureSaveDays = ConfigManager.GetConfig("NonEmojiPictureSaveDays", 7);

        // Context & memory
        ContextMaxLength = ConfigManager.GetConfig("ContextMaxLength", 20);
        EnableQdrant = ConfigManager.GetConfig("EnableQdrant", true);
        QdrantSearchOnlyPerson = ConfigManager.GetConfig("QdrantSearchOnlyPerson", false);
        MinMemorySimilarity = ConfigManager.GetConfig("MinMemorySimilarity", 0.8);
        MaxMemoryCount = ConfigManager.GetConfig("MaxMemoryCount", 5);
        MemoryDimensions = ConfigManager.GetConfig("MemoryDimensions", 1024);
        ShortTermMemoryMaxUseCount = ConfigManager.GetConfig("ShortTermMemoryMaxUseCount", 30);
        MemoryExtractionCount = ConfigManager.GetConfig("MemoryExtractionCount", 30);

        // Reply willingness
        ReplyWillingAmplifier = ConfigManager.GetConfig("ReplyWillingAmplifier", 1.0);
        EnableLLMCheckShouldResponse = ConfigManager.GetConfig("EnableLLMCheckShouldResponse", false);

        // Schedule
        EnableSchedules = ConfigManager.GetConfig("EnableSchedules", true);
        SchedulePrompt = ConfigManager.GetConfig("SchedulePrompt", "喜欢打各种游戏，为人热情积极向上，作息健康，10%概率熬夜");
        DefaultSchedule = ConfigManager.GetConfig("DefaultSchedule", "摸鱼");

        // Response
        ChatEmptyResponse = ConfigManager.GetConfig("ChatEmptyResponse", "<EMPTY>");
        LogThinkBlock = ConfigManager.GetConfig("LogThinkBlock", true);
        RemoveThinkBlock = ConfigManager.GetConfig("RemoveThinkBlock", true);

        // Record handling
        RecordNotExistSkipResponse = ConfigManager.GetConfig("RecordNotExistSkipResponse", true);
        CanCallFrameIfRecordNotExist = ConfigManager.GetConfig("CanCallFrameIfRecordNotExist", true);

        // MCP
        EnableMCP = ConfigManager.GetConfig("EnableMCP", true);
        MaxToolCallCountEachTurn = ConfigManager.GetConfig("MaxToolCallCountEachTurn", 5);
        AbortToolCallCountEachTurn = ConfigManager.GetConfig("AbortToolCallCountEachTurn", 7);

        // Embedding
        EmbeddingUrl = ConfigManager.GetConfig("EmbeddingUrl", "https://api.openai.com/v1/embeddings");
        EmbeddingModelName = ConfigManager.GetConfig("EmbeddingModelName", "text-embedding-ada-002");

        // Rerank
        EnableRerank = ConfigManager.GetConfig("EnableRerank", true);
        RerankUrl = ConfigManager.GetConfig("RerankUrl", "https://lkeap.tencentcloudapi.com");
        RerankModelName = ConfigManager.GetConfig("RerankModelName", "lke-reranker-base");

        // Tencent Cloud
        EnableTencentSign = ConfigManager.GetConfig("EnableTencentSign", false);
        TencentSecretId = ConfigManager.GetConfig("TencentSecretId", "");
        TencentSecretKey = ConfigManager.GetConfig("TencentSecretKey", "");

        // Qdrant
        QdrantHost = ConfigManager.GetConfig("QdrantHost", "localhost");
        QdrantPort = ConfigManager.GetConfig("QdrantPort", (ushort)6333);
        QdrantAPIKey = ConfigManager.GetConfig("QdrantAPIKey", "");

        // Relationship
        RelationshipUpdateTime = ConfigManager.GetConfig("RelationshipUpdateTime", 7);

        // Debug
        DebugMode = ConfigManager.GetConfig("DebugMode", false);

        ReloadAPIKeys();
        ConfigManager.EnableHotReload();

        CommonHelper.DebugMode = DebugMode;
    }

    /// <summary>
    /// Reload API key configurations from the database.
    /// </summary>
    public static void ReloadAPIKeys()
    {
        using var db = SQLiteManager.GetInstance();
        var allKeys = db.Queryable<APIKey>()
            .Includes(k => k.AvailableModels)
            .ToList();

        foreach (var keyList in new[] { ChatAPIKeyId, ReplyAPIKeyId, MemoryAPIKeyId,
                       SplitterApiKeyId, ImageDescriberApiKeyId, EmbeddingApiKeyId, RerankApiKeyId })
        {
            foreach (var item in keyList)
            {
                item.Key = allKeys.FirstOrDefault(k => k.Id == item.Id);
                item.Model = item.Key?.AvailableModels?
                    .FirstOrDefault(m => m.Name == item.Model?.Name);

                if (item.Model == null)
                {
                    CommonHelper.LogWarning?.Invoke("API 无效",
                        $"Id = {item.Id} 的 Key 中不包括 API 中的模型，请重新配置");
                }
            }
        }
    }
}
