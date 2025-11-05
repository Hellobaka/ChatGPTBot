using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model.CustomTools;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model;
using Microsoft.Extensions.AI;
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

        private static Dictionary<long, int> MemoryExtractionCount { get; set; } = [];

        private static object _todoSaveLock = new();
        private static object _shortMemorySaveLock = new();

        public static void AddShortTermMemory(string memory, CustomToolContext context)
        {
            if (ShortTermMemory.NextId == int.MaxValue - 1)
            {
                ShortTermMemory.NextId = 1;
            }
            ShortTermMemories.Add(new ShortTermMemory()
            {
                Id = ShortTermMemory.NextId++,
                Memory = memory,
                GroupId = context.GroupId,
                QQ = context.QQ,
                CreateTime = DateTime.Now,
                UsedCount = 0
            });
            SaveShortTermMemories();
        }

        public static ShortTermMemory[] GetShortTermMemories(long groupId, long qq)
        {
            var memories = ShortTermMemories.Where(x => x.GroupId == groupId || (x.GroupId == -1 && x.QQ == qq)).ToArray();
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
            if (ToDoItem.NextId == int.MaxValue - 1)
            {
                ToDoItem.NextId = 1;
            }

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
                if (ToDoItems.Count > 0)
                {
                    ToDoItem.NextId = ToDoItems.Last().Id;
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
                if (ShortTermMemories.Count > 0)
                {
                    ShortTermMemory.NextId = ShortTermMemories.Last().Id;
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
                    MainSave.CQLog?.Info("长记忆插入", $"Memory={memory}; QQ={qq} 插入成功");
                }
                else
                {
                    MainSave.CQLog?.Info("长记忆插入", $"Memory={memory}; QQ={qq} 插入失败");
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

        public static void RecordMemoryExtractionCount(long id)
        {
            if (!MemoryExtractionCount.ContainsKey(id))
            {
                MemoryExtractionCount.Add(id, 0);
            }
            MemoryExtractionCount[id]++;
        }

        public static void ExecuteMemoryExtraction(List<ChatRecord> chatRecords, long groupId, long qq)
        {
            long id = groupId > 0 ? groupId : qq;
            if (MemoryExtractionCount.TryGetValue(id, out var count))
            {
                if (count < AppConfig.MemoryExtractionCount)
                {
                    return;
                }
            }
            MemoryExtractionCount[id] = 0;
            var shortTermMemories = GetShortTermMemories(groupId, qq);
            var todo = GetToDoItems(groupId, qq);
            bool isGroup = groupId > 0;
            string prompt = $$"""
                你是一个聊天助手，你的昵称是:{{AppConfig.BotName}}，或者这些非常用称呼: {{string.Join(",", AppConfig.BotNicknames)}}，负责从用户对话中提取值得记忆的信息。
                当前{{(isGroup ? $"为群聊场景。groupId={groupId}" : $"为私聊场景。对方qq={qq}")}}，上下文中携带的用户[]中的数字为QQ

                请仔细分析以下对话上下文和最新消息，判断是否需要将信息存入短期、长期记忆或者知识库：
                ---
                记忆规则：
                - **短期记忆**：临时、上下文相关、可能在几分钟到几小时内有用的信息（如“我等下要去开会”、“密码是123456”），不要记录表情包内容。
                - **长期记忆**：持久、个人化、反复有用的信息（如“用户A喜欢喝美式咖啡”、“用户B的生日是5月20日”），不要记录表情包内容。
                - **知识**：客观、真实、普适的事实，不依赖特定用户。不记录主观观点（“我觉得 Python 比 Java 好”）；不记录已知常识（“地球是圆的”）；不记录无时效的实时新闻；不要记录表情包内容
                - 添加短期记忆前请检查 short-term-memories 是否已经有相似或重复的内容，若已经存在相似或重复内容应当调用 RenewShortTermMemory 来刷新短期记忆过期时间，而不是再调用 AddShortTermMemory。
                - 不要记录无意义、情绪化或过于泛泛的内容（如“今天好累”、“哈哈哈”）。
                - 每次对话中最多可以记录的记忆总条目数量为 {{AppConfig.MaxToolCallCountEachTurn}} 条
                ---
                以下是你的代办事项:
                <todo>
                {{string.Join("\n", [.. todo.Select(x => x.ToString())])}}
                </todo>

                以下是你的短期记忆，短期记忆最大可使用轮数为:{{AppConfig.ShortTermMemoryMaxUseCount}}
                <short-term-memories>
                {{string.Join("\n", [.. shortTermMemories.Select(x => x.ToString())])}}
                </short-term-memories>
                
                最近对话（按时间倒序）：
                <recent_Message>
                {{string.Join("\n", chatRecords.Select(x => x.ParsedMessage))}}
                </recent_Message>
                """;

            var response = Chat.GetChatResult(AppConfig.SplitterApiKeyId, [
                    new(ChatRole.System, prompt),
                    new(ChatRole.User, "请回复")
                ], Chat.Purpose.记忆提取, identity: Guid.NewGuid().ToString(), timeout: AppConfig.SplitterTimeout
                    , mcp: new MCPClientManager(groupId, qq, string.Empty, prompt
                        , enableCQApiFunction: false
                        , enableRecordFunction: false
                        , enableRelationshipFunction: false
                        , disabledTool: ["UpdateMood", "UpdateFavorability", "GetRangeUsageDetail", "AddPictureToContext", "AddDelayTask"]));
            CommonHelper.DebugLog("记忆提取", response);
        }
    }
}
