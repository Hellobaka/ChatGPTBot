using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model.CustomTools;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
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

        private static object _todoSaveLock = new();
        private static object _shortMemorySaveLock = new();

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
            SaveShortTermMemories();
        }

        public static ShortTermMemory[] GetShortTermMemories(long groupId, long qq)
        {
            var memories = ShortTermMemories.Where(x => x.Context.GroupId == groupId || (x.Context.GroupId == -1 && x.Context.QQ == qq)).ToArray();
            foreach (var memory in memories)
            {
                memory.UsedCount++;
            }
            ShortTermMemories.RemoveAll(x => x.UsedCount > AppConfig.ShortTermMemoryMaxUseCount);
            SaveShortTermMemories();
            return memories;
        }

        public static void RenewShortTermMemory(int id)
        {
            var memory = ShortTermMemories.FirstOrDefault(x => x.Id == id);
            if (memory != null)
            {
                memory.UsedCount = 0;
            }
            SaveShortTermMemories();
        }

        public static void RemoveShortTermMemory(int id)
        {
            ShortTermMemories.RemoveAll(x => x.Id == id);
            SaveShortTermMemories();
        }

        public static void RemoveShortTermMemories(int[] ids)
        {
            ShortTermMemories.RemoveAll(x => ids.Contains(x.Id));
            SaveShortTermMemories();
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
            SaveToDoItems();
        }

        public static ToDoItem[] GetToDoItems(long groupId, long qq)
        {
            return ToDoItems.Where(x => x.IsGlobalTodo || x.Context.GroupId == groupId || (x.Context.GroupId == -1 && x.Context.QQ == qq)).ToArray();
        }

        public static void CompleteToDoItem(int id)
        {
            var item = ToDoItems.FirstOrDefault(x => x.Id == id);
            if (item != null)
            {
                item.Completed = true;
                item.CompletedTime = DateTime.Now;
            }
            SaveToDoItems();
        }

        public static void RemoveToDoItem(int id)
        {
            ToDoItems.RemoveAll(x => x.Id == id);
            SaveToDoItems();
        }

        public static void RemoveToDoItems(int[] ids)
        {
            ToDoItems.RemoveAll(x => ids.Contains(x.Id));
            SaveToDoItems();
        }

        public static void SaveToDoItems()
        {
            lock (_todoSaveLock)
            {
                try
                {
                    File.WriteAllText(Path.Combine(MainSave.AppDirectory, "ToDoItems.json"), ToDoItems.ToJson(true));
                }
                catch (Exception e)
                {
                    MainSave.CQLog?.Error("保存待办事项", e);
                }
            }
        }

        public static void LoadToDoItems()
        {
            try
            {
                string path = Path.Combine(MainSave.AppDirectory, "ToDoItems.json");
                if (File.Exists(path))
                {
                    ToDoItems = JsonConvert.DeserializeObject<List<ToDoItem>>(File.ReadAllText(path));
                }
                else
                {
                    ToDoItems = [];
                }
                MainSave.CQLog?.Info("加载待办事项", $"加载了 {ToDoItems.Count} 条待办事项");
            }
            catch (Exception e)
            {
                MainSave.CQLog?.Error("加载待办事项", e);
            }
        }

        public static void SaveShortTermMemories()
        {
            lock (_shortMemorySaveLock)
            {
                try
                {
                    File.WriteAllText(Path.Combine(MainSave.AppDirectory, "ShortTermMemories.json"), ShortTermMemories.ToJson(true));
                }
                catch (Exception e)
                {
                    MainSave.CQLog?.Error("保存短期记忆", e);
                }
            }
        }

        public static void LoadShortTermMemories()
        {
            try
            {
                string path = Path.Combine(MainSave.AppDirectory, "ShortTermMemories.json");
                if (File.Exists(path))
                {
                    ShortTermMemories = JsonConvert.DeserializeObject<List<ShortTermMemory>>(File.ReadAllText(path));
                }
                else
                {
                    ShortTermMemories = [];
                }
                MainSave.CQLog?.Info("加载短期记忆", $"加载了 {ShortTermMemories.Count} 条短期记忆");
            }
            catch (Exception e)
            {
                MainSave.CQLog?.Error("加载短期记忆", e);
            }
        }

        public static void AddLongTermMemory(string memory, long qq)
        {
            if (!AppConfig.EnableQdrant || Qdrant.Instance == null)
            {
                return;
            }
            Task.Run(() =>
            {
                if (Qdrant.Instance.Insert(memory, $"LongTermMemory_{qq}"))
                {
                    CommonHelper.DebugLog("长记忆插入", $"Memory={memory}; QQ={qq} 插入成功");
                }
                else
                {
                    CommonHelper.DebugLog("长记忆插入", $"Memory={memory}; QQ={qq} 插入失败");
                }
            });
        }

        public static (string id, string record, DateTime time, float score)[] GetLongTermMemories(string query, long qq)
        {
            if (!AppConfig.EnableQdrant || Qdrant.Instance == null)
            {
                return [];
            }
            var memories = Qdrant.Instance.GetRelevantCollection(query, $"LongTermMemory_{qq}");
            return memories.ToArray();
        }

        public static void AddKnowledge(string knowledge)
        {
            if (!AppConfig.EnableQdrant || Qdrant.Instance == null)
            {
                return;
            }
            Task.Run(() =>
            {
                if (Qdrant.Instance.Insert(knowledge, Qdrant.KnowledgeCollectionName))
                {
                    MainSave.CQLog?.Info("知识插入", $"Knowledge={knowledge} 插入成功");
                }
                else
                {
                    MainSave.CQLog?.Warning("知识插入", $"Knowledge={knowledge} 插入失败");
                }
            });
        }

        public static (string id, string record, DateTime time, float score)[] GetKnowledges(string query)
        {
            if (!AppConfig.EnableQdrant || Qdrant.Instance == null)
            {
                return [];
            }
            var memories = Qdrant.Instance.GetRelevantCollection(query, Qdrant.KnowledgeCollectionName);
            return memories.ToArray();
        }
    }
}
