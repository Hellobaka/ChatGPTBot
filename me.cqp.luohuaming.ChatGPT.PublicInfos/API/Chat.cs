using Azure;
using Azure.AI.OpenAI;
using HarmonyLib;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public class Chat
    {
        public static List<ChatFlow> ChatFlows { get; set; } = new List<ChatFlow>();

        public static string GetChatResult(string question, long qq)
        {
            var t = GetChatResultAsync(question, qq);
            t.Wait();
            return t.Result;
        }

        public static async Task<string> GetChatResultAsync(string question, long qq)
        {
            string msg = "";
            var client = new OpenAIClient(AppConfig.APIKey, new OpenAIClientOptions());
            var a = Traverse.Create(client).Field("_endpoint");
            a.SetValue(new Uri(a.GetValue().ToString().Replace("https://api.openai.com", AppConfig.BaseURL)));
            client.Pipeline.CreateRequest();
            ChatFlow flow = ChatFlows.FirstOrDefault(x => x.QQ == qq);
            if (flow == null)
            {
                flow = new()
                {
                    QQ = qq
                };
                flow.Conversations.Add(new ChatFlow.ConversationItem
                {
                    Role = "system",
                    Content = $"You are ChatGPT, a large language model trained by OpenAI.\r\nKnowledge cutoff: 2021-09\r\nCurrent model: {AppConfig.ModelName}\r\nCurrent time: {DateTime.Now:G}\r\n"
                });
                ChatFlows.Add(flow);
            }
            flow.RemoveTimeout = 0;
            flow.Conversations.Add(new ChatFlow.ConversationItem
            {
                Role = "user",
                Content = question
            });

            var chatCompletionsOptions = flow.BuildMessages();
            try
            {
                if (AppConfig.StreamMode)
                {
                    Response<StreamingChatCompletions> response = await client.GetChatCompletionsStreamingAsync(AppConfig.ModelName, chatCompletionsOptions);
                    using StreamingChatCompletions streamingChatCompletions = response.Value;
                    await foreach (StreamingChatChoice choice in streamingChatCompletions.GetChoicesStreaming())
                    {
                        await foreach (ChatMessage message in choice.GetMessageStreaming())
                        {
                            msg += message.Content;
                        }
                    }
                }
                else
                {
                    Response<ChatCompletions> response = await client.GetChatCompletionsAsync(AppConfig.ModelName, chatCompletionsOptions);
                    ChatCompletions chatCompletions = response.Value;
                    foreach (var choice in chatCompletions.Choices)
                    {
                        msg += choice.Message.Content;
                    }
                }
            }
            catch (Exception ex)
            {
                MainSave.CQLog.Info("OpenAI_ChatCompletions失败", ex.Message + ex.StackTrace);
                flow.Conversations.RemoveAt(flow.Conversations.Count - 1);
            }
            return msg;
        }
    }
}