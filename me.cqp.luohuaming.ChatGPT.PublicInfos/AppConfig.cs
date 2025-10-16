using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using System.Collections.Generic;
using System.Linq;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos
{
    public static class AppConfig
    {
        public static List<APIKeyPurpose> ChatAPIKeyId { get; set; } = [];

        public static List<APIKeyPurpose> SplitterApiKeyId { get; set; } = [];

        public static List<APIKeyPurpose> ImageDescriberApiKeyId { get; set; } = [];

        public static List<APIKeyPurpose> EmbeddingApiKeyId { get; set; } = [];

        public static List<APIKeyPurpose> RerankApiKeyId { get; set; } = [];

        public static int ChatMaxTokens { get; set; } = 1000;

        public static float ChatTemperature { get; set; } = 1f;

        public static bool EnableGroupReply { get; set; }

        public static bool StreamMode { get; set; }

        public static List<long> MasterQQ { get; set; }

        public static bool IsGroupBlackList { get; set; }

        public static bool IsPersonBlackList { get; set; }

        public static List<long> GroupList { get; set; } = new List<long>();

        public static List<long> PersonList { get; set; } = new List<long>();

        public static string GroupPrompt { get; set; } = "";

        public static string PrivatePrompt { get; set; }

        public static string BotName { get; set; } = "";

        public static List<string> BotNicknames { get; set; } = [BotName];

        public static bool EnableVision { get; set; }

        public static bool IgnoreNotEmoji { get; set; }

        public static bool EnableSplitter { get; set; }

        public static int SplitterMaxLines { get; set; } = 3;

        public static bool SplitterRegexFirst { get; set; }

        public static bool SplitterRegexRemovePunctuation { get; set; }

        public static int SplitterSimulateTypeSpeed { get; set; }

        public static bool EnableSplitterRandomDelay { get; set; }

        public static int SplitterRandomDelayMin { get; set; }

        public static int SplitterRandomDelayMax { get; set; }

        /// <summary>
        /// 允许被动发送表情包（概率）
        /// </summary>
        public static bool EnableEmojiPassiveSend { get; set; }

        /// <summary>
        /// 允许通过特定模板发送表情包
        /// </summary>
        public static bool EnableEmojiActiveSend { get; set; }

        public static int EmojiSendProbability { get; set; }

        public static int ContextMaxLength { get; set; }

        public static bool EnableSchedules { get; set; }

        public static string SchedulePrompt { get; set; }

        public static string DefaultSchedule { get; set; }

        public static string ChatEmptyResponse { get; set; }

        public static bool DebugMode { get; set; }

        public static bool RandomSendEmoji { get; set; }

        public static int RecommendEmojiCount { get; set; }

        public static bool EnableRerank { get; set; }

        public static bool EnableQdrant { get; set; }

        public static double MinMemorySimilarity { get; set; }

        public static int MaxMemoryCount { get; set; }

        public static int SplitterMinLength { get; set; }

        public static string TencentSecretKey { get; set; }

        public static string TencentSecretId { get; set; }

        public static string QdrantAPIKey { get; set; }

        public static string QdrantHost { get; set; }

        public static ushort QdrantPort { get; set; }

        public static double ReplyWillingAmplifier { get; set; }

        public static List<string> Filters { get; set; } = [];

        public static int RelationshipUpdateTime { get; set; } = 7;

        public static int EmbeddingTimeout { get; set; } = 3000;

        public static int RerankTimeout { get; set; } = 3000;

        public static int ChatTimeout { get; set; } = 30000;

        public static int SplitterTimeout { get; set; } = 30000;

        public static int ImageDescriberTimeout { get; set; } = 30000;

        public static int MemoryDimensions { get; set; } = 1024;

        public static double MinEmojiRecommendScore { get; set; } = 0.5;

        public static int NonEmojiPictureSaveDays { get; set; }

        public static bool LogThinkBlock { get; set; }

        public static bool RemoveThinkBlock { get; set; }

        public static bool EnableVisionWhenMentioned { get; set; }

        public static bool RecordNotExistSkipResponse { get; set; }

        public static bool EnableMCP { get; set; }

        public static bool EnableLLMCheckShouldResponse { get; set; } = false;
       
        public static int ShortTermMemoryMaxUseCount { get; set; }
       
        public static int MaxToolCallCountEachTurn { get; set; }

        public static void Init()
        {
            ConfigHelper.DisableHotReload();
            ChatAPIKeyId = ConfigHelper.GetConfig("ChatAPIKeyId", new List<APIKeyPurpose>());
            SplitterApiKeyId = ConfigHelper.GetConfig("SplitterApiKeyId", new List<APIKeyPurpose>());
            ImageDescriberApiKeyId = ConfigHelper.GetConfig("ImageDescriberApiKeyId", new List<APIKeyPurpose>());
            EmbeddingApiKeyId = ConfigHelper.GetConfig("EmbeddingApiKeyId", new List<APIKeyPurpose>());
            RerankApiKeyId = ConfigHelper.GetConfig("RerankApiKeyId", new List<APIKeyPurpose>());

            EnableGroupReply = ConfigHelper.GetConfig("EnableGroupReply", false);
            StreamMode = ConfigHelper.GetConfig("StreamMode", true);
            MasterQQ = ConfigHelper.GetConfig("MasterQQ", new List<long>());
            ChatMaxTokens = ConfigHelper.GetConfig("ChatMaxTokens", 3000);
            ChatTemperature = ConfigHelper.GetConfig("ChatTemperature", 1f);

            GroupList = ConfigHelper.GetConfig("GroupList", new List<long>());
            PersonList = ConfigHelper.GetConfig("PersonList", new List<long>());
            IsGroupBlackList = ConfigHelper.GetConfig("IsGroupBlackList", false);
            IsPersonBlackList = ConfigHelper.GetConfig("IsPersonBlackList", false);
            BotName = ConfigHelper.GetConfig("BotName", "ChatGPT");
            BotNicknames = ConfigHelper.GetConfig("BotNicknames", new List<string>() { BotName });

            GroupPrompt = ConfigHelper.GetConfig("GroupPrompt", "胆小害羞，说话简单意骇，心情好时会使用emoji与颜文字。,现在请你读读之前的聊天记录，然后给出日常且口语化的回复，平淡一些，尽量简短一些。感觉有趣也可以直接复读消息。请注意把握聊天内容，不要刻意突出自身学科背景，不要回复的太有条理，可以有个性，请回复时不要过多提及自身的背景。");
            PrivatePrompt = ConfigHelper.GetConfig("PrivatePrompt", "胆小害羞，说话简单意骇，心情好时会使用emoji与颜文字。现在请你读读之前的聊天记录，然后给出日常且口语化的回复，尽量简短一些。请注意把握聊天内容，不要刻意突出自身学科背景，不要回复的太有条理，可以有个性，请回复时不要过多提及自身的背景。");

            EnableVision = ConfigHelper.GetConfig("EnableVision", true);
            EnableSplitter = ConfigHelper.GetConfig("EnableSplitter", false);
            SplitterMaxLines = ConfigHelper.GetConfig("SplitterMaxLines", 3);
            SplitterRegexFirst = ConfigHelper.GetConfig("SplitterRegexFirst", false);
            SplitterRegexRemovePunctuation = ConfigHelper.GetConfig("SplitterRegexRemovePunctuation", false);
            SplitterSimulateTypeSpeed = ConfigHelper.GetConfig("SplitterSimulateTypeSpeed", 100);
            EnableSplitterRandomDelay = ConfigHelper.GetConfig("EnableSplitterRandomDelay", true);
            EnableEmojiPassiveSend = ConfigHelper.GetConfig("EnableEmojiPassiveSend", false);
            SplitterRandomDelayMin = ConfigHelper.GetConfig("SplitterRandomDelayMin", 1000);
            SplitterRandomDelayMax = ConfigHelper.GetConfig("SplitterRandomDelayMax", 4500);
            SplitterMinLength = ConfigHelper.GetConfig("SplitterMinLength", 20);
            EmojiSendProbability = ConfigHelper.GetConfig("EmojiSendProbability", 10);
            ContextMaxLength = ConfigHelper.GetConfig("ContextMaxLength", 20);

            EnableRerank = ConfigHelper.GetConfig("EnableRerank", true);
            IgnoreNotEmoji = ConfigHelper.GetConfig("IgnoreNotEmoji", true);
            DebugMode = ConfigHelper.GetConfig("DebugMode", false);
            EnableSchedules = ConfigHelper.GetConfig("EnableSchedules", true);
            SchedulePrompt = ConfigHelper.GetConfig("SchedulePrompt", "喜欢打各种游戏，为人热情积极向上，作息健康，10%概率熬夜");
            DefaultSchedule = ConfigHelper.GetConfig("DefaultSchedule", "摸鱼");
            ChatEmptyResponse = ConfigHelper.GetConfig("ChatEmptyResponse", "<EMPTY>");
            RandomSendEmoji = ConfigHelper.GetConfig("RandomSendEmoji", true);
            RecommendEmojiCount = ConfigHelper.GetConfig("RecommendEmojiCount", 5);
            EnableQdrant = ConfigHelper.GetConfig("EnableQdrant", true);
            MinMemorySimilarity = ConfigHelper.GetConfig("MinMemorySimilarity", 0.8);
            MaxMemoryCount = ConfigHelper.GetConfig("MaxMemoryCount", 5);
            TencentSecretKey = ConfigHelper.GetConfig("TencentSecretKey", "");
            TencentSecretId = ConfigHelper.GetConfig("TencentSecretId", "");
            QdrantHost = ConfigHelper.GetConfig("QdrantHost", "localhost");
            QdrantPort = ConfigHelper.GetConfig("QdrantPort", (ushort)6333);
            QdrantAPIKey = ConfigHelper.GetConfig("QdrantAPIKey", "aFZsX4Xe2pzWybnX61Vi");
            ReplyWillingAmplifier = ConfigHelper.GetConfig("ReplyWillingAmplifier", (double)1);
            Filters = ConfigHelper.GetConfig("Filter", new List<string>() { "[CQ:", "&#" });
            RelationshipUpdateTime = ConfigHelper.GetConfig("RelationshipUpdateTime", 7);
            EmbeddingTimeout = ConfigHelper.GetConfig("EmbeddingTimeout", 3000);
            RerankTimeout = ConfigHelper.GetConfig("RerankTimeout", 3000);
            ChatTimeout = ConfigHelper.GetConfig("ChatTimeout", 30000);
            SplitterTimeout = ConfigHelper.GetConfig("SplitterTimeout", 30000);
            ImageDescriberTimeout = ConfigHelper.GetConfig("ImageDescriberTimeout", 30000);
            MemoryDimensions = ConfigHelper.GetConfig("MemoryDimensions", 1024);
            MinEmojiRecommendScore = ConfigHelper.GetConfig("MinEmojiRecommendScore", (double)0.5);
            NonEmojiPictureSaveDays = ConfigHelper.GetConfig("NonEmojiPictureSaveDays", 7);
            LogThinkBlock = ConfigHelper.GetConfig("LogThinkBlock", true);
            RemoveThinkBlock = ConfigHelper.GetConfig("RemoveThinkBlock", true);
            EnableVisionWhenMentioned = ConfigHelper.GetConfig("EnableVisionWhenMentioned", true);
            EnableEmojiActiveSend = ConfigHelper.GetConfig("EnableEmojiActiveSend", false);
            RecordNotExistSkipResponse = ConfigHelper.GetConfig("RecordNotExistSkipResponse", true);
            EnableMCP = ConfigHelper.GetConfig("EnableMCP", true);
            EnableLLMCheckShouldResponse = ConfigHelper.GetConfig("EnableLLMCheckShouldResponse", false);
            ShortTermMemoryMaxUseCount = ConfigHelper.GetConfig("ShortTermMemoryMaxUseCount", 10);
            MaxToolCallCountEachTurn = ConfigHelper.GetConfig("MaxToolCallCountEachTurn", 5);

            ReloadAPIKey();
            ConfigHelper.EnableHotReload();
        }

        public static void ReloadAPIKey()
        {
            APIKeys.GetAllKeys();
            foreach (var item in ChatAPIKeyId
                .Concat(SplitterApiKeyId)
                .Concat(ImageDescriberApiKeyId)
                .Concat(EmbeddingApiKeyId)
                .Concat(RerankApiKeyId))
            {
                item.Key = APIKeys.GetKeyById(item.Id);
            }
        }
    }
}