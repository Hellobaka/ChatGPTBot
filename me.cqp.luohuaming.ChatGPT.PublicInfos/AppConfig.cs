using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos
{
    public static class AppConfig
    {
        public static bool AtResponse { get; set; }

        public static string ResponsePrefix { get; set; } = "";

        public static string APIKey { get; set; } = "";

        public static string BaseURL { get; set; } = "";

        public static string ModelName { get; set; } = "";

        public static string ImageGenerationModelName { get; set; } = "";

        public static bool EnableVision { get; set; }

        public static int ChatTimeout { get; set; } = 10 * 60;

        public static int ChatMaxTokens { get; set; } = 500;

        public static int ImageGenerateSize { get; set; } = 1;

        public static int ImageGenerateQuality { get; set; } = 0;

        public static bool EnableGroupReply { get; set; }   

        public static bool AppendExecuteTime { get; set; }

        public static bool StreamMode { get; set; }

        public static long MasterQQ { get; set; }

        public static List<long> GroupList { get; set; } = new List<long>();

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

        public static string GroupPrompt { get; set; } = "";

        public static string PrivatePrompt { get; set; } = "You are ChatGPT, a large language model trained by OpenAI. You have powerful ability to process images." +
                    "Current model: $ModelName$." +
                    "Current time: $Time$.";

        public static bool AppendGroupNick { get; set; }

        public static string BotName { get; set; } = "";

        public static void Init()
        {
            AtResponse = ConfigHelper.GetConfig("AtResponse", false);
            EnableGroupReply = ConfigHelper.GetConfig("EnableGroupReply", false);
            StreamMode = ConfigHelper.GetConfig("StreamMode", true);
            AppendExecuteTime = ConfigHelper.GetConfig("AppendExecuteTime", true);
            EnableVision = ConfigHelper.GetConfig("EnableVision", false);
            AppendGroupNick = ConfigHelper.GetConfig("AppendGroupNick", false);
            APIKey = ConfigHelper.GetConfig("APIKey", "");
            BaseURL = ConfigHelper.GetConfig("BaseURL", "https://api.openai.com");
            ModelName = ConfigHelper.GetConfig("ModelName", "gpt-3.5-turbo-16k");
            ImageGenerationModelName = ConfigHelper.GetConfig("ImageGenerationModelName", "dall-e-3");
            MasterQQ = ConfigHelper.GetConfig("MasterQQ", 114514);
            ChatTimeout = ConfigHelper.GetConfig("ChatTimeout", 10 * 60);
            ChatMaxTokens = ConfigHelper.GetConfig("ChatMaxTokens", 500);
            ImageGenerateSize = ConfigHelper.GetConfig("ImageGenerateSize", 1);
            ImageGenerateQuality = ConfigHelper.GetConfig("ImageGenerateQuality", 0);

            GroupList = ConfigHelper.GetConfig("GroupList", new List<long>());
            PersonList = ConfigHelper.GetConfig("PersonList", new List<long>());
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
        }
    }
}