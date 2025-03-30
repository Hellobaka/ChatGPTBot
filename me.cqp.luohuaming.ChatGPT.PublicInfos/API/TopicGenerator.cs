using OpenAI.Chat;
using System.Linq;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public static class TopicGenerator
    {
        public const string Prompt = "我会为你提供一段话，这段话是聊天记录。请你从后续提供的一段话中总结出{0}个关键的概念，可以是名词，动词，或者特定人物，帮我列出来，用逗号`,`隔开，尽可能精简。只需要列举{0}个话题就好，不要有序号，不要告诉我其他内容。\n这是文本输入：{1}";

        private static string[] Filter { get; set; } = ["昵称", "图片", "聊天记录", "QuoteMessage"];

        public static string[]? GetTopics(string input)
        {
            string raw = Chat.GetChatResult(AppConfig.TopicUrl, AppConfig.TopicApiKey,
                [
                    new SystemChatMessage(string.Format(Prompt, AppConfig.TopicCount, input)),
                ], AppConfig.TopicModelName);
            if (raw != Chat.ErrorMessage)
            {
                raw = raw.Replace("，", ",").Replace("、", ",").Replace(" ", ",");
                return raw.Split(',').Take(AppConfig.TopicCount).Select(x => x.Trim()).Where(x => !string.IsNullOrEmpty(x) && !Filter.Contains(x)).ToArray();
            }
            else
            {
                return null;
            }
        }
    }
}
