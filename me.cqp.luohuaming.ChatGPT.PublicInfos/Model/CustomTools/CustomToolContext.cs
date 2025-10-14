namespace me.cqp.luohuaming.ChatGPT.PublicInfos.Model.CustomTools
{
    public class CustomToolContext(long groupId, long qq, string chatIdentity, string prompt, MCPClientManager mcpClientManager)
    {
        public long GroupId { get; } = groupId;
       
        public long QQ { get; } = qq;
        
        public string ChatIdentity { get; } = chatIdentity;
        
        public string ExtraIdentity { get; set; }

        public string Prompt { get; set; } = prompt;

        public MCPClientManager MCPClientManager { get; set; } = mcpClientManager;
    }
}
