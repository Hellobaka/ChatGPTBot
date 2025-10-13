using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model.CustomTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.DB
{
    /// <summary>
    /// 记忆管理模块。分为短期记忆与长期记忆（知识库）
    /// 短期记忆保存在内存中，并随着使用轮数提高而自动清理，允许通过Tool延长记忆的可使用轮数
    /// 长期记忆保存在向量数据库中，允许通过Tool主动召回或根据上下文自动召回
    /// 
    /// 记忆均由LLM自动生成、自动插入
    /// 记忆的内容均由自然语言构成
    /// </summary>
    public static class Memory
    {
        public static List<ShortTermMemory> ShortTermMemories { get; set; } = [];

        public static List<ToDoItem> ToDoItems { get; set; } = [];

        public static void AddShortTermMemory(string memory, CustomToolContext context)
        {
            ShortTermMemories.Add(new ShortTermMemory()
            {
                Id = ShortTermMemory.NextId++,
                Memory = memory,
                Context = context,
                CreateTime = DateTime.Now,
                UsedCount = 0
            });
        }

        public static ShortTermMemory[] GetShortTermMemories(long groupId, long qq)
        {
            var memories = ShortTermMemories.Where(x => x.Context.GroupId == groupId && x.Context.QQ == qq).ToArray();
            foreach (var memory in memories)
            {
                memory.UsedCount++;
            }
            ShortTermMemories.RemoveAll(x => x.UsedCount > AppConfig.ShortTermMemoryMaxUseCount);
            return memories;
        }

        public static void RenewShortTermMemory(int id)
        {
            var memory = ShortTermMemories.FirstOrDefault(x => x.Id == id);
            if (memory != null)
            {
                memory.UsedCount = 0;
            }
        }

        public static void RemoveShortTermMemory(int id)
        {
            ShortTermMemories.RemoveAll(x => x.Id == id);
        }

        public static void RemoveShortTermMemories(int[] ids)
        {
            ShortTermMemories.RemoveAll(x => ids.Contains(x.Id));
        }

        public static void AddToDoItem(string todo, bool isGlobalTodo, CustomToolContext context)
        {
            ToDoItems.Add(new ToDoItem()
            {
                Id = ToDoItem.NextId++,
                ToDo = todo,
                IsGlobalTodo = isGlobalTodo,
                Context = context,
                CreateTime = DateTime.Now,
                Completed = false
            });
        }

        public static ToDoItem[] GetToDoItems(long groupId, long qq)
        {
            return ToDoItems.Where(x => x.IsGlobalTodo || (x.Context.GroupId == groupId && x.Context.QQ == qq)).ToArray();
        }

        public static void CompleteToDoItem(int id)
        {
            var item = ToDoItems.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                item.Completed = true;
                item.CompletedTime = DateTime.Now;
            }
        }

        public static void RemoveToDoItem(int id)
        {
            ToDoItems.RemoveAll(x => x.Id == id);
        }

        public static void RemoveToDoItems(int[] ids)
        {
            ToDoItems.RemoveAll(x => ids.Contains(x.Id));
        }

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
            var memories = Qdrant.Instance.GetRelevantCollection(record).Where(x => x.record?.Id != record.Id);
            return memories.ToArray();
        }
    }
}
