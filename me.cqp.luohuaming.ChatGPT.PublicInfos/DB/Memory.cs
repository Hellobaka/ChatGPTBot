using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.DB
{
    public static class Memory
    {
        public static void AddMemory(ChatRecord record)
        {
            if (record.IsEmpty || record.IsImage)
            {
                return;
            }
            
        }

        public static string[] GetMemories(ChatRecord record)
        {
            return [];
        }

        public static double CalcMemoryActivateRate(ChatRecord record)
        {
            return 0;
        }
    }
}
