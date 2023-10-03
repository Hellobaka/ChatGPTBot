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

        public static bool StreamMode { get; set; }

        public static long MasterQQ { get; set; }

        public static List<long> GroupList { get; set; } = new List<long>();

        public static List<long> PersonList { get; set; } = new List<long>();

        public static string ContinueModeOrder { get; set; } = "";

        public static string AddPromptOrder { get; set; } = "";

        public static string RemovePromptOrder { get; set; } = "";

        public static string ListPromptOrder { get; set; } = "";

        public static string AddChatOrder { get; set; } = "";

        public static string RemoveChatOrder { get; set; } = "";

        public static void Init()
        {
            AtResponse = ConfigHelper.GetConfig("AtResponse", false);
            StreamMode = ConfigHelper.GetConfig("StreamMode", true);
            ResponsePrefix = ConfigHelper.GetConfig("ResponsePrefix", "Undefined");
            APIKey = ConfigHelper.GetConfig("APIKey", "");
            BaseURL = ConfigHelper.GetConfig("BaseURL", "https://api.openai.com");
            ModelName = ConfigHelper.GetConfig("ModelName", "gpt-3.5-turbo-16k");
            MasterQQ = ConfigHelper.GetConfig("MasterQQ", 114514);
            GroupList = ConfigHelper.GetConfig("GroupList", new List<long>());
            PersonList = ConfigHelper.GetConfig("PersonList", new List<long>());
            ContinueModeOrder = ConfigHelper.GetConfig("ContinueModeOrder", "连续模式");
            AddPromptOrder = ConfigHelper.GetConfig("AddPromptOrder", "添加预设");
            RemovePromptOrder = ConfigHelper.GetConfig("RemovePromptOrder", "移除预设");
            ListPromptOrder = ConfigHelper.GetConfig("ListPromptOrder", "预设列表");
            AddChatOrder = ConfigHelper.GetConfig("AddChatOrder", "开启聊天");
            RemoveChatOrder = ConfigHelper.GetConfig("RemoveChatOrder", "关闭聊天");
        }
    }
}