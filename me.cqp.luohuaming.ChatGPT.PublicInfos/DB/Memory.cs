using System.Linq;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.DB
{
    public static class Memory
    {
        public static void AddMemory(ChatRecord record)
        {
            if (record.IsEmpty || record.IsImage || !AppConfig.EnableMemory || Qdrant.Instance == null)
            {
                return;
            }
            Task.Run(() =>
            {
                if (Qdrant.Instance.Insert(record))
                {
                    CommonHelper.DebugLog("记忆插入", $"MessageID={record.MessageID} 插入成功");
                }
                else
                {
                    CommonHelper.DebugLog("记忆插入", $"MessageID={record.MessageID} 插入失败");
                }
            });
        }

        public static (ChatRecord record, float score)[] GetMemories(ChatRecord record)
        {
            if (record.IsEmpty || record.IsImage || !AppConfig.EnableMemory || Qdrant.Instance == null)
            {
                return [];
            }
            var memories = Qdrant.Instance.GetReleventCollection(record).Where(x => x.record.Id != record.Id);
            return AppConfig.EnableRerank ? memories.ToArray() : memories.Where(x => x.score > AppConfig.MinMemorySimilarty).ToArray();
        }
    }
}
