using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model.CustomTools;
using Newtonsoft.Json;
using System;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.Model
{
    public class ShortTermMemory
    {
        public static int NextId { get; set; } = 1;

        public int Id { get; set; }

        public DateTime CreateTime { get; set; }

        public string Memory { get; set; }

        public int UsedCount { get; set; }

        public long GroupId { get; set; }

        public long QQ { get; set; }

        public override string ToString()
        {
            return $"ID={Id};记忆内容:{Memory};创建时间:{CreateTime:G};已使用次数:{UsedCount};";
        }
    }
}
