namespace me.cqp.luohuaming.ChatGPT.PublicInfos.Model.CustomTools
{
    public class CustomToolContext(long groupId, long qq, string chatIdentity)
    {
        public long GroupId { get; } = groupId;
       
        public long QQ { get; } = qq;
        
        public string ChatIdentity { get; } = chatIdentity;
    }
}
