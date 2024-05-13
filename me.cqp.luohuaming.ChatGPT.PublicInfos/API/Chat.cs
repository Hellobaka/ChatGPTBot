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

        public static string GetChatResult(string question, long qq, long groupId, bool isGroup, out long ms)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            string result = "";
            try
            {
                var t = CallChatGPTAsync(question, qq, groupId, isGroup);
                t.Wait();
                result = t.Result;
            }
            catch (Exception ex)
            {
                result = "连接发生问题，查看日志排查问题";
                MainSave.CQLog.Info("调用ChatGPT", ex.Message + ex.StackTrace);
                if (ex.InnerException != null)
                {
                    MainSave.CQLog.Info("调用ChatGPT", ex.InnerException.Message + ex.InnerException.StackTrace);
                }
            }
            stopwatch.Stop();
            ms = stopwatch.ElapsedMilliseconds;
            return result;
        }

        private static async Task<string> CallChatGPTAsync(string question, long qq, long groupId, bool isGroup)
        {
            string msg = "";
            var client = new OpenAIClient(AppConfig.APIKey, new OpenAIClientOptions());
            var a = Traverse.Create(client).Field("_endpoint");
            a.SetValue(new Uri(a.GetValue().ToString().Replace("https://api.openai.com", AppConfig.BaseURL)));
            client.Pipeline.CreateRequest();
            if (isGroup && AppConfig.AppendGroupNick)
            {
                question = (MainSave.CQApi.GetGroupMemberInfo(groupId, qq)?.Card ?? "未获取到昵称") + $"[{qq}]" + ": " + question;
            }
            ChatFlow flow = ChatFlows.FirstOrDefault(x => isGroup ? x.ParentId == groupId : x.Id == qq);
            if (flow == null)
            {
                flow = new()
                {
                    Id = qq,
                    ParentId = groupId,
                    IsGroup = isGroup,
                };
                flow.Init();
                ChatFlows.Add(flow);
            }
            flow.RemoveTimeout = 0;
            flow.Conversations.Add(new ChatFlow.ConversationItem
            {
                Role = "user",
                Content = CommonHelper.TextTemplateParse(question, qq)
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
                msg = CommonHelper.TextTemplateParse(msg, isGroup ? groupId : qq);
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