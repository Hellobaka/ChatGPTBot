using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos
{
    public static class AppConfig
    {
        public static bool AtResponse { get; set; }

        public static bool ReplyResponse { get; set; }

        public static bool AtAnyPosition { get; set; }

        public static string ResponsePrefix { get; set; } = "";

        public static string APIKey { get; set; } = "";

        public static string BaseURL { get; set; } = "";

        public static string ModelName { get; set; } = "";

        public static int ChatTimeout { get; set; } = 10 * 60;

        public static int ChatMaxTokens { get; set; } = 500;

        public static int ImageGenerateSize { get; set; } = 1;

        public static int ImageGenerateQuality { get; set; } = 0;

        public static bool EnableGroupReply { get; set; }

        public static bool AppendExecuteTime { get; set; }

        public static bool StreamMode { get; set; }

        public static long MasterQQ { get; set; }

        public static List<long> GroupList { get; set; } = new List<long>();

        public static List<long> BlackList { get; set; } = new List<long>();

        public static List<long> PersonList { get; set; } = new List<long>();

        public static string ContinueModeOrder { get; set; } = "";

        public static string AddPromptOrder { get; set; } = "";

        public static string RemovePromptOrder { get; set; } = "";

        public static string ListPromptOrder { get; set; } = "";

        public static string EnableChatOrder { get; set; } = "";

        public static string DisableChatOrder { get; set; } = "";

        public static string ResetChatOrder { get; set; } = "";

        public static string WelcomeText { get; set; } = "请耐心等待回复...";

        public static string ChatPromptOrder { get; set; } = ".预设";

        public static string ImageGenerationOrder { get; set; } = ".画图";

        public static string AddBlackListOrder { get; set; } = ".添加黑名单";

        public static string RemoveBlackListOrder { get; set; } = ".移除黑名单";

        public static string GroupPrompt { get; set; } = "";

        public static string PrivatePrompt { get; set; }

        public static bool AppendGroupNick { get; set; }

        public static string BotName { get; set; } = "";

        public static bool EnableVision { get; set; }

        public static bool EnableTTS { get; set; }

        public static bool SendTextBeforeTTS { get; set; } = true;

        public static bool SendErrorTextWhenTTSFail { get; set; } = false;

        public static string TTSVoice { get; set; } = "zh-CN-YunxiNeural";

        public static bool RandomReply { get; set; } = false;

        public static int RandomReplyMinuteInterval { get; set; } = 1;

        public static int RandomReplyConversationCount { get; set; } = 20;

        public static int RandomReplyPersonalConversationCount { get; set; } = 20;

        public static bool RemoveThinkBlock { get; set; } = true;

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

        public static void Init()
        {
            AtResponse = ConfigHelper.GetConfig("AtResponse", false);
            ReplyResponse = ConfigHelper.GetConfig("ReplyResponse", false);
            AtAnyPosition = ConfigHelper.GetConfig("AtAnyPosition", false);
            EnableGroupReply = ConfigHelper.GetConfig("EnableGroupReply", false);
            StreamMode = ConfigHelper.GetConfig("StreamMode", true);
            AppendExecuteTime = ConfigHelper.GetConfig("AppendExecuteTime", true);
            AppendGroupNick = ConfigHelper.GetConfig("AppendGroupNick", false);
            EnableTTS = ConfigHelper.GetConfig("EnableTTS", false);
            SendTextBeforeTTS = ConfigHelper.GetConfig("SendTextBeforeTTS", true);
            SendErrorTextWhenTTSFail = ConfigHelper.GetConfig("SendErrorTextWhenTTSFail", false);
            TTSVoice = ConfigHelper.GetConfig("TTSVoice", "zh-CN-YunxiNeural");
            APIKey = ConfigHelper.GetConfig("APIKey", "");
            BochaAPIKey = ConfigHelper.GetConfig("BochaAPIKey", "");
            BaseURL = ConfigHelper.GetConfig("BaseURL", "https://api.openai.com/v1");
            ModelName = ConfigHelper.GetConfig("ModelName", "gpt-4o");
            MasterQQ = ConfigHelper.GetConfig<long>("MasterQQ", 114514);
            ChatTimeout = ConfigHelper.GetConfig("ChatTimeout", 10 * 60);
            ChatMaxTokens = ConfigHelper.GetConfig("ChatMaxTokens", 500);
            ImageGenerateSize = ConfigHelper.GetConfig("ImageGenerateSize", 2);
            ImageGenerateQuality = ConfigHelper.GetConfig("ImageGenerateQuality", 1);

            GroupList = ConfigHelper.GetConfig("GroupList", new List<long>());
            PersonList = ConfigHelper.GetConfig("PersonList", new List<long>());
            BlackList = ConfigHelper.GetConfig("BlackList", new List<long>());
            ResponsePrefix = ConfigHelper.GetConfig("ResponsePrefix", ".chat");
            ContinueModeOrder = ConfigHelper.GetConfig("ContinueModeOrder", "连续模式");
            AddPromptOrder = ConfigHelper.GetConfig("AddPromptOrder", "添加预设");
            RemovePromptOrder = ConfigHelper.GetConfig("RemovePromptOrder", "移除预设");
            ListPromptOrder = ConfigHelper.GetConfig("ListPromptOrder", "预设列表");
            EnableChatOrder = ConfigHelper.GetConfig("EnableChatOrder", "开启聊天");
            DisableChatOrder = ConfigHelper.GetConfig("DisableChatOrder", "关闭聊天");
            ResetChatOrder = ConfigHelper.GetConfig("ResetChatOrder", "重置聊天");
            ChatPromptOrder = ConfigHelper.GetConfig("ChatPromptOrder", ".预设");
            ImageGenerationOrder = ConfigHelper.GetConfig("ImageGenerationOrder", ".画图");
            AddBlackListOrder = ConfigHelper.GetConfig("AddBlackListOrder", ".添加黑名单");
            RemoveBlackListOrder = ConfigHelper.GetConfig("RemoveBlackListOrder", ".移除黑名单");
            WelcomeText = ConfigHelper.GetConfig("WelcomeText", "请耐心等待回复...");
            BotName = ConfigHelper.GetConfig("BotName", "ChatGPT");

            GroupPrompt = ConfigHelper.GetConfig("GroupPrompt", "Current model: $ModelName$." +
                    "Current time: $Time$." +
                    "你的昵称是: $BotName$" +
                    "你当前在一个QQ群中，你需要区分不同人发送的消息并给出符合群组气氛的回答。QQ号即是ID。" +
                    "根据配置不同，客户端传递的信息格式也不同。若每条信息满足 昵称[QQ]: 消息 的格式时，客户端会向你提供发言者昵称以及ID，可以依次区分不同的发言者。" +
                    "当用户昵称为“未获取到昵称”时代表客户端真的无法获取此用户的昵称，请使用ID区分。" +
                    "你在发言时无需附加这个格式，只需要回复信息即可。" +
                    "另外，如果你需要At对话者，请使用<@QQ>的格式，例如<@123456>，同理，如果用户提供这个格式表示他需要指向这个人，请从上下文了解这个人的发言历史以及个人信息");
            PrivatePrompt = ConfigHelper.GetConfig("PrivatePrompt", "Current model: $ModelName$." +
                    "Current time: $Time$." +
                    "你的昵称是: $BotName$");

            EnableVision = ConfigHelper.GetConfig("EnableVision", true);
            RandomReply = ConfigHelper.GetConfig("RandomReply", false);
            RemoveThinkBlock = ConfigHelper.GetConfig("RemoveThinkBlock", true);
            RandomReplyMinuteInterval = ConfigHelper.GetConfig("RandomReplyMinuteInterval", 1);
            RandomReplyConversationCount = ConfigHelper.GetConfig("RandomReplyConversationCount", 10);
            RandomReplyPersonalConversationCount = ConfigHelper.GetConfig("RandomReplyPersonalConversationCount", 10);

            EnableSpliter = ConfigHelper.GetConfig("EnableSpliter", false);
            SpliterModelName = ConfigHelper.GetConfig("SpliterModelName", "gpt-4o-mini");
            SpliterPrompt = ConfigHelper.GetConfig("SpliterPrompt", "为了使Bot的模仿真人的对话节奏与习惯，请将大语言模型输出的一段话，按符合正常人节奏与习惯，最大分段不能超过$MaxLines$段。分段拆分成Json数组，示例格式：['语句1', '语句2']。注意一定不要有影响到json格式的其他内容输出。上下文相关性很强的内容，一定要单独占一段，不得分开。不得精简我提供的内容");
            SpliterMaxLines = ConfigHelper.GetConfig("SpliterMaxLines", 3);
            SpliterRegexFirst = ConfigHelper.GetConfig("SpliterRegexFirst", false);
            SpliterRegexRemovePunctuation = ConfigHelper.GetConfig("SpliterRegexRemovePunctuation", false);
            SpliterSimulateTypeSpeed = ConfigHelper.GetConfig("SpliterSimulateTypeSpeed", 100);
            EnableSpliterRandomDelay = ConfigHelper.GetConfig("EnableSpliterRandomDelay", true);
            SpliterRandomDelayMin = ConfigHelper.GetConfig("SpliterRandomDelayMin", 1000);
            SpliterRandomDelayMax = ConfigHelper.GetConfig("SpliterRandomDelayMax", 4500);
        }
    }
}