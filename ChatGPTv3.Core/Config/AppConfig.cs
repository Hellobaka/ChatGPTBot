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

    public static List<APIKeyPurpose> SplitterApiKeyId { get; set; } = [];

    public static List<APIKeyPurpose> ImageDescriberApiKeyId { get; set; } = [];

    public static List<APIKeyPurpose> EmbeddingApiKeyId { get; set; } = [];

    public static List<APIKeyPurpose> RerankApiKeyId { get; set; } = [];

    public static List<APIKeyPurpose> SummarizerApiKeyId { get; set; } = [];

    // ── Chat Configuration ────────────────────────────────
    public static int ChatMaxTokens { get; set; } = 3000;

    public static float ChatTemperature { get; set; } = 1f;

    public static int ChatTimeout { get; set; } = 30000;

    public static int ReplyTimeout { get; set; } = 5000;

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

    // ── Knowledge Import Chunking ───────────────────────────
    public static int KnowledgeChunkSize { get; set; } = 300;

    public static int KnowledgeChunkOverlap { get; set; } = 50;

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

    public static double MinMemorySimilarity { get; set; } = 0.8;

    public static int MaxMemoryCount { get; set; } = 5;

    public static int MemoryDimensions { get; set; } = 1024;

    // ── Reply Willingness — Attention ──────────────────────
    /// <summary>Base attention for any message.</summary>
    public static double BaseAttention { get; set; } = 0.1;

    /// <summary>Attention when @mentioned.</summary>
    public static double AttnMention { get; set; } = 1.0;

    /// <summary>Attention when message is a reply to bot.</summary>
    public static double AttnReplyToBot { get; set; } = 0.9;

    /// <summary>Attention when bot nickname appears in text.</summary>
    public static double AttnNickname { get; set; } = 0.8;

    /// <summary>Attention when message contains a question mark.</summary>
    public static double AttnQuestion { get; set; } = 0.5;

    /// <summary>Attention when continuing a conversation with same user.</summary>
    public static double AttnContinuity { get; set; } = 0.4;

    /// <summary>Multiplier for image-only messages.</summary>
    public static double AttnImageFactor { get; set; } = 0.1;

    // ── Reply Willingness — Timing curve ───────────────────
    /// <summary>Timing multiplier: just sent (0 msgs since last bot msg).</summary>
    public static double TimingJustSent { get; set; } = 0.1;

    /// <summary>Timing multiplier: brief pause (1-3 msgs).</summary>
    public static double TimingBriefPause { get; set; } = 0.3;

    /// <summary>Timing multiplier: optimal window (4-10 msgs).</summary>
    public static double TimingOptimal { get; set; } = 1.0;

    /// <summary>Timing multiplier: getting stale (11-20 msgs).</summary>
    public static double TimingStale { get; set; } = 0.6;

    /// <summary>Timing multiplier: too late (21+ msgs).</summary>
    public static double TimingVeryStale { get; set; } = 0.3;

    // ── Reply Willingness — Activity throttle ──────────────
    /// <summary>Activity multiplier: no recent sends.</summary>
    public static double ActivityFirst { get; set; } = 1.0;

    /// <summary>Activity multiplier: 1 recent send.</summary>
    public static double ActivitySecond { get; set; } = 0.5;

    /// <summary>Activity multiplier: 2 recent sends.</summary>
    public static double ActivityThird { get; set; } = 0.2;

    /// <summary>Throttle window in seconds.</summary>
    public static int ActivityThrottleSeconds { get; set; } = 60;

    // ── Reply Willingness — General ────────────────────────
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

    // ── Rerank ────────────────────────────────────────────
    public static bool EnableRerank { get; set; } = true;

    // ── Qdrant ────────────────────────────────────────────
    public static string QdrantHost { get; set; } = "localhost";

    public static ushort QdrantPort { get; set; } = 6333;

    public static string QdrantAPIKey { get; set; } = "";

    // ── Relationship ──────────────────────────────────────
    public static int RelationshipUpdateTime { get; set; } = 7;

    // ── Debounce ──────────────────────────────────────────
    /// <summary>Milliseconds to buffer before processing. If a new message from
    /// the same source arrives during this window, the old one is discarded.</summary>
    public static int MessageDebounceMs { get; set; } = 3000;

    // ── Content Filter Fallback ───────────────────────────
    /// <summary>Whether to use LLM to generate a content-filter deflection (vs random from list).</summary>
    public static bool UseLLMContentFilterFallback { get; set; }

    /// <summary>Custom replies used when UseLLMContentFilterFallback is false.</summary>
    public static List<string> ContentFilterFallbacks { get; set; } = [
        "啊这个...换个话题吧！",
        "唔，这个我不太擅长回答...",
        "诶嘿，跳过这个话题～"
    ];

    // ── Diary ─────────────────────────────────────────────
    /// <summary>Master switch for diary memory system.</summary>
    public static bool EnableDiary { get; set; } = true;

    /// <summary>API keys for diary generation (falls back to ChatAPIKeyId if empty).</summary>
    public static List<APIKeyPurpose> DiaryAPIKeyId { get; set; } = [];

    /// <summary>Trigger diary after N messages in a group.</summary>
    public static int DiaryMessageThreshold { get; set; } = 50;

    /// <summary>Trigger diary if at least this many minutes have passed since last diary, when there are new messages.</summary>
    public static int DiaryIntervalMinutes { get; set; } = 120;

    /// <summary>How many hours of chat history to review for each diary entry.</summary>
    public static int DiaryReviewHours { get; set; } = 24;

    /// <summary>Keep last N diary entries per group.</summary>
    public static int DiaryMaxKeep { get; set; } = 7;

    /// <summary>LLM timeout for diary generation (ms).</summary>
    public static int DiaryTimeout { get; set; } = 60000;

    // ── Scheduled Task ────────────────────────────────────
    /// <summary>Minimum interval in minutes for cron expressions. Prevents abuse.</summary>
    public static int MinCronIntervalMinutes { get; set; } = 5;

    // ── Context Compression ───────────────────────────────
    /// <summary>Enable time-based compression trigger (every N minutes).</summary>
    public static bool EnableCompressByTime { get; set; } = true;

    /// <summary>Enable count-based compression trigger (every N new droppable messages).</summary>
    public static bool EnableCompressByCount { get; set; } = true;

    /// <summary>Minimum minutes between LLM compressions per group.</summary>
    public static int CompressIntervalMinutes { get; set; } = 60;

    /// <summary>Minimum new droppable messages since last LLM compression to trigger again.</summary>
    public static int CompressMessageThreshold { get; set; } = 20;

    // ── Debug ─────────────────────────────────────────────
    public static bool DebugMode { get; set; }

    /// <summary>
    /// When true, all outgoing messages are logged instead of delivered and
    /// every send reports success. Used for dry-run / testing without QQ sends.
    /// </summary>
    public static bool MockSendMessage { get; set; }

    // ═══════════════════════════════════════════════════════
    //  Initialization
    // ═══════════════════════════════════════════════════════

    public static void Init()
    {
        ConfigManager.DisableHotReload();

        // API key purpose lists — loaded from DB in ReloadAPIKeys
        ChatAPIKeyId = [];
        ReplyAPIKeyId = [];
        SplitterApiKeyId = [];
        ImageDescriberApiKeyId = [];
        EmbeddingApiKeyId = [];
        SummarizerApiKeyId = [];
        RerankApiKeyId = [];

        // Chat config
        ChatMaxTokens = ConfigManager.GetConfig("ChatMaxTokens", 3000);
        ChatTemperature = ConfigManager.GetConfig("ChatTemperature", 1f);
        ChatTimeout = ConfigManager.GetConfig("ChatTimeout", 30000);
        ReplyTimeout = ConfigManager.GetConfig("ReplyTimeout", 5000);
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
        Filters = ConfigManager.GetConfig("Filters", new List<string> { "[CQ:", "&#" });

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

        // Knowledge Import Chunking
        KnowledgeChunkSize = ConfigManager.GetConfig("KnowledgeChunkSize", 300);
        KnowledgeChunkOverlap = ConfigManager.GetConfig("KnowledgeChunkOverlap", 50);

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
        MinMemorySimilarity = ConfigManager.GetConfig("MinMemorySimilarity", 0.8);
        MaxMemoryCount = ConfigManager.GetConfig("MaxMemoryCount", 5);
        MemoryDimensions = ConfigManager.GetConfig("MemoryDimensions", 1024);
        // Reply willingness — Attention
        BaseAttention = ConfigManager.GetConfig("BaseAttention", 0.1);
        AttnMention = ConfigManager.GetConfig("AttnMention", 1.0);
        AttnReplyToBot = ConfigManager.GetConfig("AttnReplyToBot", 0.9);
        AttnNickname = ConfigManager.GetConfig("AttnNickname", 0.8);
        AttnQuestion = ConfigManager.GetConfig("AttnQuestion", 0.5);
        AttnContinuity = ConfigManager.GetConfig("AttnContinuity", 0.4);
        AttnImageFactor = ConfigManager.GetConfig("AttnImageFactor", 0.1);

        // Reply willingness — Timing curve
        TimingJustSent = ConfigManager.GetConfig("TimingJustSent", 0.1);
        TimingBriefPause = ConfigManager.GetConfig("TimingBriefPause", 0.3);
        TimingOptimal = ConfigManager.GetConfig("TimingOptimal", 1.0);
        TimingStale = ConfigManager.GetConfig("TimingStale", 0.6);
        TimingVeryStale = ConfigManager.GetConfig("TimingVeryStale", 0.3);

        // Reply willingness — Activity throttle
        ActivityFirst = ConfigManager.GetConfig("ActivityFirst", 1.0);
        ActivitySecond = ConfigManager.GetConfig("ActivitySecond", 0.5);
        ActivityThird = ConfigManager.GetConfig("ActivityThird", 0.2);
        ActivityThrottleSeconds = ConfigManager.GetConfig("ActivityThrottleSeconds", 60);

        // Reply willingness — General
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
        // Endpoint and model are now managed via APIKey binding (EmbeddingApiKeyId)

        // Rerank
        EnableRerank = ConfigManager.GetConfig("EnableRerank", true);

        // Qdrant
        QdrantHost = ConfigManager.GetConfig("QdrantHost", "localhost");
        QdrantPort = ConfigManager.GetConfig("QdrantPort", (ushort)6333);
        QdrantAPIKey = ConfigManager.GetConfig("QdrantAPIKey", "");

        // Relationship
        RelationshipUpdateTime = ConfigManager.GetConfig("RelationshipUpdateTime", 7);

        // Debounce
        MessageDebounceMs = ConfigManager.GetConfig("MessageDebounceMs", 3000);

        // Content filter fallback
        UseLLMContentFilterFallback = ConfigManager.GetConfig("UseLLMContentFilterFallback", false);
        ContentFilterFallbacks = ConfigManager.GetConfig("ContentFilterFallbacks",
            new List<string> { "啊这个...换个话题吧！", "唔，这个我不太擅长回答...", "诶嘿，跳过这个话题～" });

        // Diary
        EnableDiary = ConfigManager.GetConfig("EnableDiary", true);
        DiaryAPIKeyId = [];
        DiaryMessageThreshold = ConfigManager.GetConfig("DiaryMessageThreshold", 50);
        DiaryIntervalMinutes = ConfigManager.GetConfig("DiaryIntervalMinutes", 120);
        DiaryReviewHours = ConfigManager.GetConfig("DiaryReviewHours", 24);
        DiaryMaxKeep = ConfigManager.GetConfig("DiaryMaxKeep", 7);
        DiaryTimeout = ConfigManager.GetConfig("DiaryTimeout", 60000);

        // Scheduled task
        MinCronIntervalMinutes = ConfigManager.GetConfig("MinCronIntervalMinutes", 5);

        // Context compression
        EnableCompressByTime = ConfigManager.GetConfig("EnableCompressByTime", true);
        EnableCompressByCount = ConfigManager.GetConfig("EnableCompressByCount", true);
        CompressIntervalMinutes = ConfigManager.GetConfig("CompressIntervalMinutes", 60);
        CompressMessageThreshold = ConfigManager.GetConfig("CompressMessageThreshold", 20);

        // Debug
        DebugMode = ConfigManager.GetConfig("DebugMode", false);
        MockSendMessage = ConfigManager.GetConfig("MockSendMessage", false);

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

        // Load purpose bindings from DB
        var dbBindings = db.Queryable<PurposeBinding>().ToList();

        // Build purpose → list mapping
        ChatAPIKeyId.Clear();
        ReplyAPIKeyId.Clear();
        SplitterApiKeyId.Clear();
        ImageDescriberApiKeyId.Clear();
        EmbeddingApiKeyId.Clear();
        RerankApiKeyId.Clear();
        SummarizerApiKeyId.Clear();
        DiaryAPIKeyId.Clear();

        var purposeMap = new Dictionary<string, List<APIKeyPurpose>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Chat"] = ChatAPIKeyId,
            ["Reply"] = ReplyAPIKeyId,
            ["Splitter"] = SplitterApiKeyId,
            ["ImageDescriber"] = ImageDescriberApiKeyId,
            ["Embedding"] = EmbeddingApiKeyId,
            ["Rerank"] = RerankApiKeyId,
            ["Summarizer"] = SummarizerApiKeyId,
            ["Diary"] = DiaryAPIKeyId
        };

        foreach (var binding in dbBindings)
        {
            if (!purposeMap.TryGetValue(binding.Purpose, out var list))
            {
                continue;
            }

            var key = allKeys.FirstOrDefault(k => k.Id == binding.APIKeyId);
            var model = key?.AvailableModels?.FirstOrDefault(m => m.Id == binding.LLMModelConfigId);
            list.Add(new APIKeyPurpose
            {
                Id = binding.APIKeyId,
                Key = key,
                Model = model
            });
        }
    }
}
