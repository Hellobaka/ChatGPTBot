using System;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.Model.CustomTools
{
    public class CustomToolContext(long groupId, long qq, string chatIdentity, string prompt
        , MCPClientManager mcpClientManager
        , int messageId = 0
        , Action<string, long, long, int>? sendReply = null
        , bool enableMemoryFunction = true
        , bool enableCQApiFunction = true
        , bool enableRelationshipFunction = true
        , bool enableRecordFunction = true)
    {
        public long GroupId { get; } = groupId;
       
        public long QQ { get; } = qq;
        
        public string ChatIdentity { get; } = chatIdentity;
        
        public string ExtraIdentity { get; set; }

        public string Prompt { get; set; } = prompt;

        public MCPClientManager MCPClientManager { get; set; } = mcpClientManager;

        public Action<string, long, long, int> SendReply { get; set; } = sendReply;

        public int MessageId { get; } = messageId;

        public bool EnableMemoryFunction { get; set; } = enableMemoryFunction;
        
        public bool EnableCQApiFunction { get; set; } = enableCQApiFunction;

        public bool EnableRelationshipFunction { get; set; } = enableRelationshipFunction;
        
        public bool EnableRecordFunction { get; set; } = enableRecordFunction;
    }
}
