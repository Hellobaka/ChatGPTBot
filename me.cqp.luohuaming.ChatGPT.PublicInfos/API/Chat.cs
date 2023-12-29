using Azure;
using Azure.AI.OpenAI;
using HarmonyLib;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public class Chat
    {
        public static List<ChatFlow> ChatFlows { get; set; } = new List<ChatFlow>();

        public static string GetChatResult(string question, long qq)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var t = CallChatGPTAsync(question, qq);
            t.Wait();
            stopwatch.Stop();
            if (AppConfig.AppendExecuteTime)
            {
                return t.Result + $" ({stopwatch.ElapsedMilliseconds / 1000.0:f2}s)";
            }
            else
            {
                return t.Result;
            }
        }

        private static async Task<string> CallChatGPTAsync(string question, long qq)
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
                    await foreach (StreamingChatCompletionsUpdate chatUpdate in client.GetChatCompletionsStreaming(chatCompletionsOptions))
                    {
                        if (!string.IsNullOrEmpty(chatUpdate.ContentUpdate))
                        {
                            msg += chatUpdate.ContentUpdate;
                        }
                    }
                }
                else
                {
                    Response<ChatCompletions> response = await client.GetChatCompletionsAsync(chatCompletionsOptions);
                    ChatResponseMessage responseMessage = response.Value.Choices[0].Message;
                    msg = responseMessage.Content;
                }
                flow.Conversations.Add(new ChatFlow.ConversationItem
                {
                    Role = "assistant",
                    Content = msg
                });
            }
            catch (Exception ex)
            {
                MainSave.CQLog.Info("OpenAI_ChatCompletions失败", ex.Message + ex.StackTrace);
                flow.Conversations.RemoveAt(flow.Conversations.Count - 1);
                msg = "连接发生问题，查看日志排查问题";
            }
            return msg;
        }
    }
}