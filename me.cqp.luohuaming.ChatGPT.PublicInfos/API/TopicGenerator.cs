using OpenAI.Chat;
using System.Linq;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public static class TopicGenerator
    {
        public const string Prompt = "这是一段文字：{0}。请你从这段话中总结出{1}个关键的概念，可以是名词，动词，或者特定人物，帮我列出来，用逗号`,`隔开，尽可能精简。只需要列举{1}个话题就好，不要有序号，不要告诉我其他内容。";

        public static string[] GetTopics(string input)
        {
            string raw = Chat.GetChatResult(AppConfig.TopicUrl, AppConfig.TopicApiKey,
                [
                    new SystemChatMessage(string.Format(Prompt, input, AppConfig.TopicCount)),
                ], AppConfig.TopicModelName);
            raw = raw.Replace("，", ",");
            return raw.Split(',').Take(AppConfig.TopicCount).ToArray();
        }
    }
}
