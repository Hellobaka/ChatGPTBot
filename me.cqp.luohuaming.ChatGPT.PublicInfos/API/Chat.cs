using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model.CustomTools;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public class Chat
    {
        public enum Purpose
        {
            聊天,
            图片描述,
            日程获取,
            分段,
            表情包推荐,
            回复意愿,
            记忆提取,
        }

        public const string ErrorMessage = "连接发生问题，查看日志排查问题";

        public static event Action<string, string>? OnToolCall;

        private static readonly Lazy<ChatService> _chatService = new Lazy<ChatService>(() => new ChatService());

        public static string GetChatResult(List<APIKeyPurpose> key, List<ChatMessage> chatMessages, Purpose purpose, bool jsonMode = false, int timeout = 10000, MCPClientManager? mcp = null, string? identity = null)
        {
            return _chatService.Value.GetChatResult(key, chatMessages, purpose, jsonMode, timeout, mcp, identity);
        }

        public static string GetChatResult(APIKeyPurpose? key, List<ChatMessage> chatMessages, Purpose purpose, bool jsonMode = false, int timeout = 10000, MCPClientManager? mcp = null, string? identity = null)
        {
            return _chatService.Value.GetChatResult(key, chatMessages, purpose, jsonMode, timeout, mcp, identity);
        }

        public static void TriggerOnToolCall(string identity, string msg)
        {
            OnToolCall?.Invoke(identity, msg);
        }

        public static string GetChatResult(string baseUrl,
                                           string apiKey,
                                           string modelName,
                                           List<ChatMessage> chatMessages,
                                           Purpose purpose,
                                           bool jsonMode = false,
                                           int timeout = 10000,
                                           MCPClientManager mcp = null,
                                           string? identity = null,
                                           LLMModel? model = null)
        {
            return _chatService.Value.GetChatResult(baseUrl, apiKey, modelName, chatMessages, purpose, jsonMode, timeout, mcp, identity, model);
        }
    }
}