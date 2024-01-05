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

        public static bool EnableVision { get; set; }

        public static int ChatTimeout { get; set; } = 10 * 60;

        public static int ChatMaxTokens { get; set; } = 500;

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

        public static void Init()
        {
            AtResponse = ConfigHelper.GetConfig("AtResponse", false);
            StreamMode = ConfigHelper.GetConfig("StreamMode", true);
            ResponsePrefix = ConfigHelper.GetConfig("ResponsePrefix", "Undefined");
            APIKey = ConfigHelper.GetConfig("APIKey", "");
            BaseURL = ConfigHelper.GetConfig("BaseURL", "https://api.openai.com");
            ModelName = ConfigHelper.GetConfig("ModelName", "gpt-3.5-turbo-16k");
            EnableVision = ConfigHelper.GetConfig("EnableVision", false);
            MasterQQ = ConfigHelper.GetConfig("MasterQQ", 114514);
            ChatTimeout = ConfigHelper.GetConfig("ChatTimeout", 10 * 60);
            ChatMaxTokens = ConfigHelper.GetConfig("ChatMaxTokens", 500);
            AppendExecuteTime = ConfigHelper.GetConfig("AppendExecuteTime", true);
            GroupList = ConfigHelper.GetConfig("GroupList", new List<long>());
            PersonList = ConfigHelper.GetConfig("PersonList", new List<long>());
            ContinueModeOrder = ConfigHelper.GetConfig("ContinueModeOrder", "连续模式");
            AddPromptOrder = ConfigHelper.GetConfig("AddPromptOrder", "添加预设");
            RemovePromptOrder = ConfigHelper.GetConfig("RemovePromptOrder", "移除预设");
            ListPromptOrder = ConfigHelper.GetConfig("ListPromptOrder", "预设列表");
            EnableChatOrder = ConfigHelper.GetConfig("EnableChatOrder", "开启聊天");
            DisableChatOrder = ConfigHelper.GetConfig("DisableChatOrder", "关闭聊天");
            ResetChatOrder = ConfigHelper.GetConfig("ResetChatOrder", "重置聊天");
            ChatPromptOrder = ConfigHelper.GetConfig("ChatPromptOrder", ".预设");
            EnableGroupReply = ConfigHelper.GetConfig("EnableGroupReply", false);
            WelcomeText = ConfigHelper.GetConfig("WelcomeText", "请耐心等待回复...");
        }
    }
}