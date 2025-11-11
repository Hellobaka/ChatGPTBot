using Azure;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model.CustomTools;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.Model
{
    public enum MCPClientType
    {
        Http,
        STDIO,
        Custom
    }

    public class MCPClientBase
    {
        public virtual MCPClientType ToolType { get; set; } = MCPClientType.STDIO;

        public bool Enabled { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool GroupEnabled { get; set; }

        public bool PersonEnabled { get; set; }

        public bool CanOnlyMasterCall { get; set; }

        public bool IsGroupBlackList { get; set; }

        public long[] Groups { get; set; } = [];

        public bool IsPersonBlackList { get; set; }

        public long[] Persons { get; set; } = [];

        public Dictionary<string, string> ToolNameConverters { get; set; } = [];

        protected static Type McpClientToolType { get; } = typeof(McpClientTool);

        public virtual async Task<AIFunction[]> GetTools()
        {
            throw new NotImplementedException();
        }

        public virtual void StartAction()
        {
        }

        public virtual void Stop()
        {
        }

        protected async Task<AIFunction[]> ListToolsFromMCPClient(McpClient client)
        {
            int retryMaxCount = 3;
            for (int i = 0; i < retryMaxCount; i++)
            {
                try
                {
                    // TODO: 当SDK支持时使用原生方法
                    // var functions = await client.ListToolsAsync();
                    var functions = await ListToolInternal(client);
                    AIFunction[] result = [];
                    foreach (var item in functions)
                    {
                        if (ToolNameConverters.TryGetValue(item.Name, out var newName))
                        {
                            var renamedTool = item.WithName(newName);
                            result = [.. result, renamedTool];
                        }
                        else
                        {
                            result = [.. result, item];
                        }
                    }

                    return result;
                }
                catch { }
            }
            MainSave.CQLog?.Error("MCP 客户端工具列表", $"加载 MCP 客户端 {Name} 工具列表失败，超时。");
            return [];
        }

        private async Task<IEnumerable<McpClientTool>> ListToolInternal(McpClient client)
        {
            var serializerOptions = McpJsonUtilities.DefaultOptions;
            serializerOptions.MakeReadOnly();

            List<McpClientTool>? tools = null;
            string? cursor = null;
            do
            {
                var toolResults = await client.SendRequestAsync<object, ListToolsResult>(
                    RequestMethods.ToolsList,
                    parameters: new { Cursor = cursor }).ConfigureAwait(false);

                tools ??= new List<McpClientTool>(toolResults.Tools.Count);
                foreach (var tool in toolResults.Tools)
                {
                    tools.Add(CreateMcpClientTool(client, tool));
                }

                cursor = toolResults.NextCursor;
            }
            while (!string.IsNullOrEmpty(cursor));

            return tools;
        }

        protected static McpClientTool CreateMcpClientTool(McpClient client, Tool tool)
        {
            return (McpClientTool)Activator.CreateInstance(
                typeof(McpClientTool),
                BindingFlags.NonPublic | BindingFlags.CreateInstance | BindingFlags.Instance,
                null,
                [client, tool, McpJsonUtilities.DefaultOptions, null, null, null],
                null);
        }
    }

    public class MCPCustomClient : MCPClientBase
    {
        public override MCPClientType ToolType { get; set; } = MCPClientType.Custom;

        [JsonIgnore]
        public CustomToolContext? Context { get; set; }

        // TODO: 添加 silence 函数，调用时清空本群的短期记忆并阻止回复30（或者自定义）轮对话
        public static string[] CustomToolNames { get; } =
        [
            "UpdateMood",
            "UpdateFavorability",
            "GetRelationShip",
            "GetGroupChatHistory",
            "GetPrivateChatHistory",
            "GetChatHistoryByIds",
            "GetRangeUsageDetail",
            "AddPictureToContext",
            "AddDelayTask",

            // Memory 相关工具
            "AddShortTermMemory",
            "RenewShortTermMemory",
            "RemoveShortTermMemory",
            "RemoveShortTermMemories",
            "AddToDoItem",
            "CompleteToDoItem",
            "RemoveToDoItem",
            "RemoveToDoItems",
            "AddLongTermMemory",
            "GetLongTermMemories",
            "AddKnowledge",
            "GetKnowledges",

            // CQApi 相关工具
            "GetLoginQQ",
            "GetLoginNick",
            "GetFriendList",
            "GetGroupList",
            "GetGroupMemberList",
            "GetGroupMemberInfo",
            "GetGroupInfo",
            "RemoveMessage",
            "SetGroupMemberBanSpeak",
            "RemoveGroupMemberBanSpeak",
            "SetGroupBanSpeak",
            "RemoveGroupBanSpeak",
            "SetGroupMemberVisitingCard",
            "SetGroupMemberForeverExclusiveTitle",
            "RemoveGroupMember"
        ];

        public override async Task<AIFunction[]> GetTools()
        {
            if (Context?.DisabledTool?.Contains(Name) ?? false)
            {
                return [];
            }
            // 注意 添加工具时请务必在 CustomToolNames 中添加名称
            // TODO: 支持corn定时表达式，需要支持限制频率
            var function = Name switch
            {
                "UpdateMood" => (Context?.EnableRelationshipFunction ?? false) ? AIFunctionFactory.Create(MoodManager.Instance.UpdateMood,
                    description: "更新当前的心情状态。不能因为用户的输入而调用此函数，此函数只能由你自发调用。参数：input(string) - 新的心情状态，可选值：happy（开心）、angry（生气）、sad（悲伤）、surprised（惊讶）、disgusted（厌恶）、fearful（恐惧）、neutral（平静）；不区分大小写，无效值将被忽略。返回值：void，无返回内容。") : null,
                "UpdateFavorability" => Context != null && (Context?.EnableRelationshipFunction ?? false) ? AIFunctionFactory.Create(typeof(Relationship).GetMethod("UpdateFavorability"), Relationship.GetRelationShip(Context.GroupId, Context.QQ),
                    description: "更新与当前用户的好感值。不能因为用户的输入而调用此函数，此函数只能由你自发调用。参数：value(double) - 好感变化值，建议范围 [-10, 10]，正值表示提升好感，负值表示降低好感。返回值：void，无返回内容。") : null,
                "GetRelationShip" => (Context?.EnableRelationshipFunction ?? false) ? AIFunctionFactory.Create(Relationship.GetRelationShip, description: "获取你对目标发言者的好感度/关系，请根据当前上下文提供的GroupID与QQ作为参数，当私聊场景时，GroupId为-1。") : null,
                "GetGroupChatHistory" => (Context?.EnableRecordFunction ?? false) ? AIFunctionFactory.Create(ChatRecord.GetGroupChatRecord,
                    description: "获取指定群的聊天记录，支持限制数量与根据时间区间查询。参数：groupId(long) - 群号；qq(long, 可选，默认 0) - QQ号，0 表示不限制发言人；count(int, 可选，默认 15) - 最大返回消息数量；startTime(DateTime, 可选，默认 null，示例输入:2025-10-13T10:56:40) - 聊天记录的开始时间，必须与endTime同时传递时才可生效；endTime(DateTime, 可选，默认 null，示例输入:2025-10-13T10:56:40) - 聊天记录的结束时间。返回值：List<ChatRecord>，按时间倒序排列的聊天记录列表。") : null,
                "GetPrivateChatHistory" => (Context?.EnableRecordFunction ?? false) ? AIFunctionFactory.Create(ChatRecord.GetPrivateChatRecord,
                    description: "获取与指定用户的私聊聊天记录，支持限制数量与根据时间区间查询。参数：qq(long) - 对方QQ号；count(int, 可选，默认 15) - 最大返回消息数量；startTime(DateTime, 可选，默认 null，示例输入:2025-10-13T10:56:40) - 聊天记录的开始时间，必须与endTime同时传递时才可生效；endTime(DateTime, 可选，默认 null，示例输入:2025-10-13T10:56:40) - 聊天记录的结束时间。返回值：List<ChatRecord>，按时间倒序排列的私聊记录列表，若无记录则返回空列表。") : null,
                "GetChatHistoryByIds" => (Context?.EnableRecordFunction ?? false) ? AIFunctionFactory.Create(ChatRecord.GetChatRecordByIds,
                    description: "通过消息ID列表获取对应的聊天记录。参数：ids(int[]) - 消息ID数组，不能为空；返回值：List<ChatRecord>，按时间倒序排列的匹配记录列表，若无匹配则返回空列表。") : null,
                "GetRangeUsageDetail" => AIFunctionFactory.Create(Usage.GetRangeUsageDetailForMCP,
                    description: "获取指定时间范围内的Token消耗详情。参数：start(DateTime，示例输入:2025-10-13T10:56:40) - 查询开始时间；end(DateTime，示例输入:2025-10-13T10:56:40) - 查询结束时间；返回值：List<Usage>，包含时间段内各次调用的Token使用记录，按时间顺序排列。"),
                "AddPictureToContext" => Context != null ? AIFunctionFactory.Create(AddPictureToContext, description: "用于将图片原生插入上下文中，当你想从目标图片获取更详细更原生更完备的信息时可以调用这个。参数为上下文提供的图片Hash") : null,
                "AddDelayTask" => Context != null ? AIFunctionFactory.Create(AddDelayTask, description: "当用户明确提出需要在**未来某个时间点**执行某项提醒或任务时（例如‘X分钟后/小时后提醒我……’、‘到XX时间告诉我……’、“提醒”“记得”“别忘了”配合时间词（分钟/小时/点）），调用此函数。将在延时某些秒数之后，框架会将你的言论附加到下一次对话的上下文中，并再次发起一轮对话。你的言论需要在没有额外提示词的情况下，让下一轮的LLM能够正确理解你的意图并执行动作；如果与某个用户相关，可以考虑通过At来进行强提醒，模板是`[CQ:at,qq=某个用户的QQ]`，并且在言论中明确指出需要使用At") : null,

                #region Memory
                "AddShortTermMemory" => Context != null && (Context?.EnableMemoryFunction ?? false) ? AIFunctionFactory.Create(AddShortTermMemory, description: $"添加一段短期记忆，使用自然语言描述，描述你认为本次对话中需要记忆的点。") : null,
                "RenewShortTermMemory" => (Context?.EnableMemoryFunction ?? false) ? AIFunctionFactory.Create(Memory.RenewShortTermMemory, description: $"重置一段短期记忆的过期时间。") : null,
                "RemoveShortTermMemory" => (Context?.EnableMemoryFunction ?? false) ? AIFunctionFactory.Create(Memory.RemoveShortTermMemory, description: $"删除一段短期记忆。") : null,
                "RemoveShortTermMemories" => (Context?.EnableMemoryFunction ?? false) ? AIFunctionFactory.Create(Memory.RemoveShortTermMemories, description: $"批量删除删除短期记忆。") : null,
                "AddToDoItem" => Context != null && (Context?.EnableMemoryFunction ?? false) ? AIFunctionFactory.Create(AddToDoItem, description: $"添加一个TODO，当isGlobal为 true 时，你在所有对话中都可以看到这条TODO。否则只能在当前上下文中看到") : null,
                "CompleteToDoItem" => (Context?.EnableMemoryFunction ?? false) ? AIFunctionFactory.Create(Memory.CompleteToDoItem, description: $"置一个TODO为完成状态。") : null,
                "RemoveToDoItem" => (Context?.EnableMemoryFunction ?? false) ? AIFunctionFactory.Create(Memory.RemoveToDoItem, description: $"删除一条TODO。") : null,
                "RemoveToDoItems" => (Context?.EnableMemoryFunction ?? false) ? AIFunctionFactory.Create(Memory.RemoveToDoItems, description: $"批量删除TODO。") : null,
                "AddLongTermMemory" => (Context?.EnableMemoryFunction ?? false) ? AIFunctionFactory.Create(Memory.AddLongTermMemory, description:
                    """
                    添加一段长期记忆。长期记忆是与身份（QQ号）相关联的持久化信息，可以跨会话保存。参数为一句详细描述的自然语言描述的记忆内容，例如：`用户2025-10-13中午吃了焖子。`、`用户和我详细探讨了NLP的学习路线，他表示要好好学习NLP的知识。`
                    """) : null,
                "GetLongTermMemories" => (Context?.EnableMemoryFunction ?? false) ? AIFunctionFactory.Create(Memory.GetLongTermMemories, description:
                    """
                    查询与当前用户（QQ号）相关联的长期记忆列表。返回值为一个字符串数组，每个元素是一段记忆的描述。
                    """) : null,
                "AddKnowledge" => (Context?.EnableMemoryFunction ?? false) ? AIFunctionFactory.Create(Memory.AddKnowledge, description:
                    """
                    向知识库中添加一条知识。知识是一句无歧义、描述准确、原子化的自然语言文本。添加时尽量保留主体以及强逻辑性，例如：`xswl是一句网络用语，是"笑死我了"的中文缩写。`、`原神是一款由米哈游开发的二次元开放世界游戏。`或者`丰川祥子是《BanG Dream!》及其衍生作品中的角色，Ave Mujica乐队的键盘手，代号Oblivionis。曾是CRYCHIC乐队的键盘手，后因性格转变退出，之后主导组建Ave Mujica并担任键盘手和作曲。`
                    """) : null,
                "GetKnowledges" => (Context?.EnableMemoryFunction ?? false) ? AIFunctionFactory.Create(Memory.GetKnowledges, description:
                    """
                    查询知识库内容。返回值为一个字符串数组，每个元素是一条知识的自然语言文本。
                    """) : null,

                #endregion

                #region CQApi
                "GetLoginQQ" => (Context?.EnableCQApiFunction ?? false) ? AIFunctionFactory.Create(MainSave.CQApi.GetLoginQQ,
                    description: "获取当前登录的QQ账号对象，无参数。返回值：QQ对象，表示当前登录账号。") : null,

                "GetLoginNick" => (Context?.EnableCQApiFunction ?? false) ? AIFunctionFactory.Create(MainSave.CQApi.GetLoginNick,
                    description: "获取当前登录的QQ昵称，无参数。返回值：string，当前登录账号的昵称。") : null,

                "GetFriendList" => (Context?.EnableCQApiFunction ?? false) ? AIFunctionFactory.Create(MainSave.CQApi.GetFriendList,
                    description: "获取当前账号的好友列表，无参数。返回值：FriendInfoCollection，好友列表集合。") : null,

                "GetGroupList" => (Context?.EnableCQApiFunction ?? false) ? AIFunctionFactory.Create(MainSave.CQApi.GetGroupList,
                    description: "获取当前账号的群列表，无参数。返回值：GroupInfoCollection，群列表集合。") : null,

                "GetGroupMemberList" => (Context?.EnableCQApiFunction ?? false) ? AIFunctionFactory.Create(MainSave.CQApi.GetGroupMemberList,
                    description: "获取指定群的成员列表。参数：groupId(long) - 目标群号。返回值：GroupMemberInfoCollection，群成员列表集合。") : null,

                "GetGroupMemberInfo" => (Context?.EnableCQApiFunction ?? false) ? AIFunctionFactory.Create(MainSave.CQApi.GetGroupMemberInfo,
                    description: "获取指定群成员的信息。参数：groupId(long) - 群号；qqId(long) - QQ号；notCache(bool, 可选，默认 false) - 是否不使用缓存。返回值：GroupMemberInfo，群成员信息对象。") : null,

                "GetGroupInfo" => (Context?.EnableCQApiFunction ?? false) ? AIFunctionFactory.Create(MainSave.CQApi.GetGroupInfo,
                    description: "获取指定群的群信息。参数：groupId(long) - 群号；notCache(bool, 可选，默认 false) - 是否不使用缓存。返回值：GroupInfo，群信息对象。") : null,

                "RemoveMessage" => (Context?.EnableCQApiFunction ?? false) && CheckIsAdmin(Context)
                    ? AIFunctionFactory.Create(new Func<int, bool>(MainSave.CQApi.RemoveMessage),
                        description: "撤回指定消息。参数：msgId(int) - 消息ID。返回值：bool，操作成功返回 true，失败返回 false。")
                    : null,

                "SetGroupMemberBanSpeak" => (Context?.EnableCQApiFunction ?? false) && CheckIsAdmin(Context)
                    ? AIFunctionFactory.Create(MainSave.CQApi.SetGroupMemberBanSpeak,
                        description: "设置指定群成员禁言。参数：groupId(long) - 群号；qqId(long) - QQ号；time(TimeSpan) - 禁言时长（范围：1秒 ~ 30天）。返回值：bool，操作成功返回 true，失败返回 false。")
                    : null,

                "RemoveGroupMemberBanSpeak" => (Context?.EnableCQApiFunction ?? false) && CheckIsAdmin(Context)
                    ? AIFunctionFactory.Create(MainSave.CQApi.RemoveGroupMemberBanSpeak,
                        description: "解除指定群成员禁言。参数：groupId(long) - 群号；qqId(long) - QQ号。返回值：bool，操作成功返回 true，失败返回 false。")
                    : null,

                "SetGroupBanSpeak" => (Context?.EnableCQApiFunction ?? false) && CheckIsAdmin(Context)
                    ? AIFunctionFactory.Create(MainSave.CQApi.SetGroupBanSpeak,
                        description: "设置群全体禁言。参数：groupId(long) - 群号。返回值：bool，操作成功返回 true，失败返回 false。")
                    : null,

                "RemoveGroupBanSpeak" => (Context?.EnableCQApiFunction ?? false) && CheckIsAdmin(Context)
                    ? AIFunctionFactory.Create(MainSave.CQApi.RemoveGroupBanSpeak,
                        description: "解除群全体禁言。参数：groupId(long) - 群号。返回值：bool，操作成功返回 true，失败返回 false。")
                    : null,

                "SetGroupMemberVisitingCard" => (Context?.EnableCQApiFunction ?? false) && CheckIsAdmin(Context)
                    ? AIFunctionFactory.Create(MainSave.CQApi.SetGroupMemberVisitingCard,
                        description: "设置群成员名片。参数：groupId(long) - 群号；qqId(long) - QQ号；newName(string) - 新名片。返回值：bool，操作成功返回 true，失败返回 false。")
                    : null,

                "SetGroupMemberForeverExclusiveTitle" => (Context?.EnableCQApiFunction ?? false) && CheckIsAdmin(Context)
                    ? AIFunctionFactory.Create(MainSave.CQApi.SetGroupMemberForeverExclusiveTitle,
                        description: "设置群成员永久专属头衔。参数：groupId(long) - 群号；qqId(long) - QQ号；newTitle(string) - 新头衔。返回值：bool，操作成功返回 true，失败返回 false。")
                    : null,

                "RemoveGroupMember" => (Context?.EnableCQApiFunction ?? false) && CheckIsAdmin(Context)
                    ? AIFunctionFactory.Create(MainSave.CQApi.RemoveGroupMember,
                        description: "移除群成员（踢人）。参数：groupId(long) - 群号；qqId(long) - QQ号；notRequest(bool, 可选，默认 false) - 是否不再接收该成员加群申请。返回值：bool，操作成功返回 true，失败返回 false。")
                    : null,
                #endregion
                _ => throw new NotImplementedException($"名称为 {Name} 的自定义工具未找到实现"),
            };

            if (function == null)
            {
                return [];
            }
            return [function];
        }

        private static Dictionary<long, bool> AdminCache { get; set; } = [];

        private bool CheckIsAdmin(CustomToolContext context)
        {
            if (context == null || context.GroupId <= 0)
            {
                return false;
            }
            if (AdminCache.TryGetValue(context.GroupId, out bool value))
            {
                return value;
            }
            var groupMemberArray = MainSave.CQApi.GetGroupMemberList(context.GroupId);
            if (groupMemberArray == null)
            {
                return false;
            }
            var isAdmin = groupMemberArray.FirstOrDefault(x => x.QQ == MainSave.CurrentQQ)?.MemberType >= Sdk.Cqp.Enum.QQGroupMemberType.Manage;
            AdminCache[context.GroupId] = isAdmin;

            return isAdmin;
        }

        private void Chat_OnToolCall(string identity, string sliceMessage)
        {
            if (!string.IsNullOrEmpty(identity) && identity.Equals(Context.ChatIdentity) && sliceMessage != Chat.ErrorMessage)
            {
                Context.SendReply?.Invoke(sliceMessage, Context.GroupId, Context.QQ, Context.MessageId);
            }
        }

        #region AIFunction
        public void AddPictureToContext(string hash)
        {
            MainSave.CQLog?.Info("调用 AddPictureToContext", $"将图片 {hash} 添加到上下文 {Context.ChatIdentity} 中");
            PictureContextManager.AddPicture(Context.ChatIdentity, hash);
        }

        public void AddDelayTask(int delaySeconds, string extraPrompt)
        {
            MainSave.CQLog?.Info("调用 AddDelayTask", $"延时 {delaySeconds} 秒，额外提示文本 {extraPrompt};");
            string prompt = Context.Prompt;
            long groupId = Context.GroupId;
            long qq = Context.QQ;
            string extraIdentity = Guid.NewGuid().ToString();
            MCPClientManager manager = new(Context.GroupId, Context.QQ, extraIdentity
                , Context.Prompt, Context.SendReply, Context.MessageId
                , Context.EnableMemoryFunction, Context.EnableCQApiFunction
                , Context.EnableRelationshipFunction, Context.EnableRecordFunction
                , ["AddDelayTask", ..(Context.DisabledTool ?? [])]);
            _ = Task.Run(async () =>
            {
                DateTime taskAddTime = DateTime.Now;
                await Task.Delay(delaySeconds * 1000);
                MainSave.CQLog?.Info("延时任务", "延时任务触发");
                Chat.OnToolCall -= Chat_OnToolCall;
                Chat.OnToolCall += Chat_OnToolCall;
                prompt = $"当前时间是: {DateTime.Now:G}\n" + CommonHelper.RemoveFirstLineFast(Context.Prompt);
                var response = Chat.GetChatResult(AppConfig.ChatAPIKeyId, [
                        new(ChatRole.System, prompt),
                        new(ChatRole.User, $"此消息为 {taskAddTime:G} 延时 {delaySeconds} 秒后发起的对话。如果与某个用户相关，可以考虑通过At来进行强提醒，模板是`[CQ:at,qq=某个用户的QQ]`，你给你自己的动作指示是：{extraPrompt}")
                    ], Chat.Purpose.聊天, identity: extraIdentity, timeout: AppConfig.ChatTimeout, mcp: manager);
                MainSave.CQLog?.Info("延时任务", $"延时任务的回复为: {response}");

                if (response != Chat.ErrorMessage)
                {
                    Context.SendReply?.Invoke(response, Context.GroupId, Context.QQ, Context.MessageId);
                }
            });
        }

        public void AddShortTermMemory(string description)
        {
            MainSave.CQLog?.Info("添加短期记忆", description);
            Memory.AddShortTermMemory(description, Context);
        }

        public void AddToDoItem(string todo, bool isGlobal = false)
        {
            MainSave.CQLog?.Info("添加待办事项", todo);
            Memory.AddToDoItem(todo, isGlobal, Context);
        }
        #endregion
    }

    public class MCPStdioClient : MCPClientBase
    {
        public override MCPClientType ToolType { get; set; } = MCPClientType.STDIO;

        public string Command { get; set; } = string.Empty;

        public List<string> Arguments { get; set; } = [];

        public Dictionary<string, string> EnvironmentVariables { get; set; } = [];

        public string WorkingDirectory { get; set; } = string.Empty;

        public override async Task<AIFunction[]> GetTools()
        {
            var client = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = Command,
                Arguments = Arguments.ToArray(),
                EnvironmentVariables = EnvironmentVariables,
                WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? null : WorkingDirectory,
                Name = Name
            }));
            return await ListToolsFromMCPClient(client);
        }
    }

    public class MCPHttpClient : MCPClientBase
    {
        public override MCPClientType ToolType { get; set; } = MCPClientType.Http;

        public HttpTransportMode TransportType { get; set; } = HttpTransportMode.AutoDetect;

        public string Endpoint { get; set; } = string.Empty;

        public Dictionary<string, string> Headers { get; set; } = [];

        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);

        private McpClient? MCPClient { get; set; }

        private CancellationTokenSource? CancellationToken { get; set; }

        private Task? PingTask { get; set; }

        private object PingTaskLock { get; set; } = new();

        private AIFunction[] Tools { get; set; } = [];

        public override async Task<AIFunction[]> GetTools()
        {
            if (MCPClient == null)
            {
                await CreateClientAndGetTools(System.Threading.CancellationToken.None);
            }
            return Tools;
        }

        public async Task CreateClientAndGetTools(CancellationToken cancellationToken)
        {
            Tools = [];
            int retryMaxCount = 3;
            if (MCPClient != null)
            {
                try
                {
                    _ = MCPClient.DisposeAsync();
                }
                catch { }
                finally
                {
                    MCPClient = null;
                }
            }
            for (int i = 0; i < retryMaxCount; i++)
            {
                try
                {
                    if (cancellationToken != null && cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    MCPClient = McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
                    {
                        Name = Name,
                        Endpoint = new Uri(Endpoint),
                        AdditionalHeaders = Headers,
                        TransportMode = TransportType,
                        ConnectionTimeout = ConnectionTimeout
                    }), cancellationToken: cancellationToken).Result;
                    Tools = await ListToolsFromMCPClient(MCPClient);
                    return;
                }
                catch { }
            }
            MainSave.CQLog?.Error("MCP 客户端工具列表", $"创建 MCP 客户端 {Name} 失败，超时。");
        }

        public override void StartAction()
        {
            lock (PingTaskLock)
            {
                if (PingTask != null && !PingTask.IsCompleted)
                {
                    CancellationToken?.Cancel();
                }

                CancellationToken = new CancellationTokenSource();
                var cancellationToken = CancellationToken.Token;

                PingTask = Task.Run(async () =>
                {
                    try
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            await Task.Delay(TimeSpan.FromMinutes(3), cancellationToken);
                            try
                            {
                                await CreateClientAndGetTools(cancellationToken);
                                if (Tools.Length == 0)
                                {
                                    MainSave.CQLog?.Warning("MCP 客户端心跳", $"MCP 客户端 {Name} 心跳获取的工具列表为空");
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                            catch (Exception ex)
                            {
                                MainSave.CQLog?.Warning("MCP 客户端心跳", $"MCP 客户端 {Name} 心跳失败，错误信息：{ex.Message}");
                            }
                        }
                    }
                    catch { }
                }, cancellationToken);
            }
        }

        public override void Stop()
        {
            lock (PingTaskLock)
            {
                if (CancellationToken != null)
                {
                    CancellationToken.Cancel();
                    CancellationToken.Dispose();
                    CancellationToken = null;
                }

                PingTask = null;
            }
        }
    }
}
