using System.Collections.Generic;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos
{
    public static class AppConfig
    {
        public static string ChatAPIKey { get; set; } = "";

        public static string ChatBaseURL { get; set; } = "";

        public static string ChatModelName { get; set; } = "";

        public static int ChatMaxTokens { get; set; } = 1000;

        public static float ChatTemperature { get; set; } = 1.3f;

        public static int ImageGenerateSize { get; set; } = 1;

        public static int ImageGenerateQuality { get; set; } = 0;

        public static string ImageGenerateBaseURL { get; set; } = "";

        public static string ImageGenerateAPIKey { get; set; } = "";

        public static bool EnableGroupReply { get; set; }

        public static bool StreamMode { get; set; }

        public static long MasterQQ { get; set; }

        public static List<long> GroupList { get; set; } = new List<long>();

        public static List<long> BlackList { get; set; } = new List<long>();

        public static List<long> PersonList { get; set; } = new List<long>();

        public static string ImageGenerationOrder { get; set; } = ".画图";

        public static string AddBlackListOrder { get; set; } = ".添加黑名单";

        public static string RemoveBlackListOrder { get; set; } = ".移除黑名单";

        public static string GroupPrompt { get; set; } = "";

        public static string PrivatePrompt { get; set; }

        public static string BotName { get; set; } = "";

        public static List<string> BotNicknames { get; set; } = [BotName];

        public static bool EnableVision { get; set; }

        public static bool IgnoreNotEmoji { get; set; }

        public static bool EnableTTS { get; set; }

        public static string TTSVoice { get; set; } = "zh-CN-YunxiNeural";

        public static bool EnableSpliter { get; set; }

        public static string SpliterModelName { get; set; }

        public static string SpliterPrompt { get; set; }

        public static int SpliterMaxLines { get; set; } = 3;

        public static bool SpliterRegexFirst { get; set; }

        public static bool SpliterRegexRemovePunctuation { get; set; }

        public static int SpliterSimulateTypeSpeed { get; set; }

        public static bool EnableSpliterRandomDelay { get; set; }

        public static int SpliterRandomDelayMin { get; set; }

        public static int SpliterRandomDelayMax { get; set; }

        public static string BochaAPIKey { get; set; }

        public static bool EnableEmojiSend {  get; set; }

        public static int EmojiSendProbablity {  get; set; }

        public static string SpliterUrl {  get; set; }

        public static string SpliterApiKey {  get; set; }

        public static string ImageDescriberUrl {  get; set; }

        public static string ImageDescriberApiKey {  get; set; }

        public static string ImageDescriberModelName {  get; set; }

        public static int ContextMaxLength {  get; set; }

        public static string EmbeddingUrl { get; set; }

        public static string EmbeddingApiKey { get; set; }

        public static string EmbeddingModelName { get; set; }

        public static string SchedulePrompt { get; set; }

        public static string DefaultSchedule { get; set; }

        public static string ChatEmptyResponse { get; set; }

        public static bool DebugMode { get; set; }

        public static bool RandomSendEmoji { get; set; }

        public static int RecommendEmojiCount { get; set; }

        public static bool EnableRerank { get; set; }

        public static string RerankUrl { get; set; }

        public static string RerankApiKey { get; set; }

        public static string RerankModelName { get; set; }

        public static bool EnableMemory { get; set; }

        public static double MinMemorySimilarty { get; set; }

        public static int MaxMemoryCount { get; set; }

        public static int SpliterMinLength { get; set; }

        public static bool EnableTencentSign { get; set; }

        public static string TencentSecretKey { get; set; }

        public static string TencentSecretId { get; set; }

        public static string QdrantAPIKey { get; set; }

        public static string QdrantHost { get; set; }

        public static ushort QdrantPort { get; set; }

        public static bool QdrantSearchOnlyPerson { get; set; }

        public static double ReplyWillingAmplifier { get; set; }

        public static List<string> Filters { get; set; } = [];

        public static int RelationshipUpdateTime { get; set; } = 7;

        public static int EmbeddingTimeout { get; set; } = 3000;

        public static int RerankTimeout { get; set; } = 3000;

        public static int ChatTimeout { get; set; } = 30000;

        public static int MemoryDimensions { get; set; } = 1024;

        public static void Init()
        {
            ConfigHelper.DisableHotReload();
            EnableGroupReply = ConfigHelper.GetConfig("EnableGroupReply", false);
            StreamMode = ConfigHelper.GetConfig("StreamMode", true);
            EnableTTS = ConfigHelper.GetConfig("EnableTTS", false);
            TTSVoice = ConfigHelper.GetConfig("TTSVoice", "zh-CN-YunxiNeural");
            ChatAPIKey = ConfigHelper.GetConfig("ChatAPIKey", "");
            BochaAPIKey = ConfigHelper.GetConfig("BochaAPIKey", "");
            ChatBaseURL = ConfigHelper.GetConfig("ChatBaseURL", "https://api.openai.com/v1");
            ChatModelName = ConfigHelper.GetConfig("ChatModelName", "gpt-4o");
            MasterQQ = ConfigHelper.GetConfig<long>("MasterQQ", 114514);
            ChatMaxTokens = ConfigHelper.GetConfig("ChatMaxTokens", 3000);
            ChatTemperature = ConfigHelper.GetConfig("ChatTemperature", 1.3f);

            ImageGenerationOrder = ConfigHelper.GetConfig("ImageGenerationOrder", ".画图");
            ImageGenerateSize = ConfigHelper.GetConfig("ImageGenerateSize", 2);
            ImageGenerateQuality = ConfigHelper.GetConfig("ImageGenerateQuality", 1);
            ImageGenerateBaseURL = ConfigHelper.GetConfig("ImageGenerateBaseURL", "https://api.openai.com/v1");
            ImageGenerateAPIKey = ConfigHelper.GetConfig("ImageGenerateAPIKey", "");

            GroupList = ConfigHelper.GetConfig("GroupList", new List<long>());
            PersonList = ConfigHelper.GetConfig("PersonList", new List<long>());
            BlackList = ConfigHelper.GetConfig("BlackList", new List<long>());
            AddBlackListOrder = ConfigHelper.GetConfig("AddBlackListOrder", ".添加黑名单");
            RemoveBlackListOrder = ConfigHelper.GetConfig("RemoveBlackListOrder", ".移除黑名单");
            BotName = ConfigHelper.GetConfig("BotName", "ChatGPT");
            BotNicknames = ConfigHelper.GetConfig("BotNicknames", new List<string>() { BotName });

            GroupPrompt = ConfigHelper.GetConfig("GroupPrompt", "胆小害羞，说话简单意骇，心情好时会使用emoji与颜文字。");
            PrivatePrompt = ConfigHelper.GetConfig("PrivatePrompt", "胆小害羞，说话简单意骇，心情好时会使用emoji与颜文字。");

            EnableVision = ConfigHelper.GetConfig("EnableVision", true);
            EnableSpliter = ConfigHelper.GetConfig("EnableSpliter", false);
            SpliterModelName = ConfigHelper.GetConfig("SpliterModelName", "gpt-4o-mini");
            SpliterPrompt = ConfigHelper.GetConfig("SpliterPrompt", "请将后续输入的一段话，按符合正常人节奏与习惯，最大分段不能超过$MaxLines$段。分段拆分成Json数组，示例格式：['语句1', '语句2']。注意一定不要有影响到json格式的其他内容输出。上下文相关性很强的内容，一定要单独占一段，不得分开。不得精简我提供的内容，一定不得更改我的输入文本。每个分段结尾只能有问号、叹号或者省略号，逗号句号都不要");
            SpliterMaxLines = ConfigHelper.GetConfig("SpliterMaxLines", 3);
            SpliterRegexFirst = ConfigHelper.GetConfig("SpliterRegexFirst", false);
            SpliterRegexRemovePunctuation = ConfigHelper.GetConfig("SpliterRegexRemovePunctuation", false);
            SpliterSimulateTypeSpeed = ConfigHelper.GetConfig("SpliterSimulateTypeSpeed", 100);
            EnableSpliterRandomDelay = ConfigHelper.GetConfig("EnableSpliterRandomDelay", true);
            EnableEmojiSend = ConfigHelper.GetConfig("EnableEmojiSend", false);
            SpliterRandomDelayMin = ConfigHelper.GetConfig("SpliterRandomDelayMin", 1000);
            SpliterRandomDelayMax = ConfigHelper.GetConfig("SpliterRandomDelayMax", 4500);
            SpliterMinLength = ConfigHelper.GetConfig("SpliterMinLength", 10);
            EmojiSendProbablity = ConfigHelper.GetConfig("EmojiSendProbablity", 10);
            ContextMaxLength = ConfigHelper.GetConfig("ContextMaxLength", 20);
            SpliterUrl = ConfigHelper.GetConfig("SpliterUrl", "https://api.openai.com/v1");
            SpliterApiKey = ConfigHelper.GetConfig("SpliterApiKey", "");
            ImageDescriberUrl = ConfigHelper.GetConfig("ImageDescriberUrl", "https://api.openai.com/v1");
            ImageDescriberApiKey = ConfigHelper.GetConfig("ImageDescriberApiKey", "");
            ImageDescriberModelName = ConfigHelper.GetConfig("ImageDescriberModelName", "gpt-4o-mini");

            EmbeddingUrl = ConfigHelper.GetConfig("EmbeddingUrl", "https://api.openai.com/v1/embeddings");
            EmbeddingApiKey = ConfigHelper.GetConfig("EmbeddingApiKey", "");
            EmbeddingModelName = ConfigHelper.GetConfig("EmbeddingModelName", "text-embedding-ada-002");
            EnableRerank = ConfigHelper.GetConfig("EnableRerank", true);
            RerankUrl = ConfigHelper.GetConfig("RerankUrl", "");
            RerankApiKey = ConfigHelper.GetConfig("RerankApiKey", "");
            RerankModelName = ConfigHelper.GetConfig("RerankModelName", "");
            IgnoreNotEmoji = ConfigHelper.GetConfig("IgnoreNotEmoji", true);
            DebugMode = ConfigHelper.GetConfig("DebugMode", false);
            SchedulePrompt = ConfigHelper.GetConfig("SchedulePrompt", "喜欢打各种游戏，为人热情积极向上，作息健康，10%概率熬夜");
            DefaultSchedule = ConfigHelper.GetConfig("DefaultSchedule", "摸鱼");
            ChatEmptyResponse = ConfigHelper.GetConfig("ChatEmptyResponse", "<EMPTY>");
            RandomSendEmoji = ConfigHelper.GetConfig("RandomSendEmoji", true);
            RecommendEmojiCount = ConfigHelper.GetConfig("RecommendEmojiCount", 5);
            EnableMemory = ConfigHelper.GetConfig("EnableMemory", true);
            MinMemorySimilarty = ConfigHelper.GetConfig("MinMemorySimilarty", 0.8);
            MaxMemoryCount = ConfigHelper.GetConfig("MaxMemoryCount", 5);
            EnableTencentSign = ConfigHelper.GetConfig("EnableTencentSign", false);
            TencentSecretKey = ConfigHelper.GetConfig("TencentSecretKey", "");
            TencentSecretId = ConfigHelper.GetConfig("TencentSecretId", "");
            QdrantHost = ConfigHelper.GetConfig("QdrantHost", "localhost");
            QdrantPort = ConfigHelper.GetConfig("QdrantPort", (ushort)6333);
            QdrantAPIKey = ConfigHelper.GetConfig("QdrantAPIKey", "aFZsX4Xe2pzWybnX61Vi");
            QdrantSearchOnlyPerson = ConfigHelper.GetConfig("QdrantSearchOnlyPerson", false);
            ReplyWillingAmplifier = ConfigHelper.GetConfig("ReplyWillingAmplifier", (double)1);
            Filters = ConfigHelper.GetConfig("Filter", new List<string>() { "[CQ:", "&#" });
            RelationshipUpdateTime = ConfigHelper.GetConfig("RelationshipUpdateTime", 7);
            EmbeddingTimeout = ConfigHelper.GetConfig("EmbeddingTimeout", 3000);
            RerankTimeout = ConfigHelper.GetConfig("RerankTimeout", 3000);
            ChatTimeout = ConfigHelper.GetConfig("ChatTimeout", 30000);
            MemoryDimensions = ConfigHelper.GetConfig("MemoryDimensions", 1024);

            ConfigHelper.EnableHotReload();
        }
    }
}