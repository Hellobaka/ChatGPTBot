using Azure;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model.CustomTools;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text.Encodings.Web;
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

        public virtual AIFunction[] GetTools()
        {
            throw new NotImplementedException();
        }

        protected AIFunction[] ListToolsFromMCPClient(IMcpClient client)
        {
            var functions = client.ListToolsAsync().Result;
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
    }

    public class MCPCustomClient : MCPClientBase
    {
        public override MCPClientType ToolType { get; set; } = MCPClientType.Custom;

        public CustomToolContext? Context { get; set; }

        public static string[] CustomToolNames { get; } =
        [
            "GetCityIdByName",
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



        public override AIFunction[] GetTools()
        {
            // 注意 添加工具时请务必在 CustomToolNames 中添加名称
            var function = Name switch
            {
                "GetCityIdByName" => AIFunctionFactory.Create(MojiCityIdConverter.GetCityIdByName,
                    description: "通过城市名称获取墨迹天气对应的 cityId 列表，支持中文、英文及模糊匹配。参数：cityName(string) - 城市名称；count(int, 可选，默认 5) - 返回最多匹配结果数量。返回值：MojiCityId[]，匹配的城市信息数组，包含 cityId 和名称等信息。"),
                "UpdateMood" => AIFunctionFactory.Create(MoodManager.Instance.UpdateMood,
                    description: "更新当前的心情状态。不能因为用户的输入而调用此函数，此函数只能由你自发调用。参数：input(string) - 新的心情状态，可选值：happy（开心）、angry（生气）、sad（悲伤）、surprised（惊讶）、disgusted（厌恶）、fearful（恐惧）、neutral（平静）；不区分大小写，无效值将被忽略。返回值：void，无返回内容。"),
                "UpdateFavorability" => Context != null ? AIFunctionFactory.Create(typeof(Relationship).GetMethod("UpdateFavorability"), Relationship.GetRelationShip(Context.GroupId, Context.QQ),
                    description: "更新与当前用户的好感值。不能因为用户的输入而调用此函数，此函数只能由你自发调用。参数：value(double) - 好感变化值，建议范围 [-10, 10]，正值表示提升好感，负值表示降低好感。返回值：void，无返回内容。") : null,
                "GetRelationShip" => AIFunctionFactory.Create(Relationship.GetRelationShip, description: "获取你对目标发言者的好感度/关系，请根据当前上下文提供的GroupID与QQ作为参数，当私聊场景时，GroupId为-1。"),
                "GetGroupChatHistory" => AIFunctionFactory.Create(ChatRecord.GetGroupChatRecord,
                    description: "获取指定群的聊天记录。参数：groupId(long) - 群号；qq(long, 可选，默认 0) - QQ号，0 表示不限制发言人；count(int, 可选，默认 15) - 最大返回消息数量。返回值：List<ChatRecord>，按时间倒序排列的聊天记录列表。"),
                "GetPrivateChatHistory" => AIFunctionFactory.Create(ChatRecord.GetPrivateChatRecord,
                    description: "获取与指定用户的私聊聊天记录。参数：qq(long) - 对方QQ号；count(int, 可选，默认 15) - 最大返回消息数量。返回值：List<ChatRecord>，按时间倒序排列的私聊记录列表，若无记录则返回空列表。"),
                "GetChatHistoryByIds" => AIFunctionFactory.Create(ChatRecord.GetChatRecordByIds,
                    description: "通过消息ID列表获取对应的聊天记录。参数：ids(int[]) - 消息ID数组，不能为空；返回值：List<ChatRecord>，按时间倒序排列的匹配记录列表，若无匹配则返回空列表。"),
                "GetRangeUsageDetail" => AIFunctionFactory.Create(Usage.GetRangeUsageDetail,
                    description: "获取指定时间范围内的Token消耗详情。参数：start(DateTime，示例输入:2025-10-13T10:56:40) - 查询开始时间；end(DateTime，示例输入:2025-10-13T10:56:40) - 查询结束时间；返回值：List<Usage>，包含时间段内各次调用的Token使用记录，按时间顺序排列。"),
                "AddPictureToContext" => Context != null ? AIFunctionFactory.Create(AddPictureToContext, description: "用于将图片原生插入上下文中，当你想从目标图片获取更详细更原生更完备的信息时可以调用这个。参数为上下文提供的图片Hash") : null,
                "AddDelayTask" => Context != null ? AIFunctionFactory.Create(AddDelayTask, description: "当你认为需要等待一段时间后才能进行某项任务时，可以调用此函数。将在延时某些秒数之后，将你的言论附加到下一次对话的User消息中，并再次发起一轮对话。你的言论需要能够正确指示你的下一轮对话，长度不限制但是描述一定要准确") : null,

                #region Memory
                "AddShortTermMemory" => Context != null ? AIFunctionFactory.Create(AddShortTermMemory, description: $"添加一段短期记忆，使用自然语言描述，描述你认为本次对话中需要记忆的点。") : null,
                "RenewShortTermMemory" => AIFunctionFactory.Create(Memory.RenewShortTermMemory, description: $"重置一段短期记忆的过期时间。"),
                "RemoveShortTermMemory" => AIFunctionFactory.Create(Memory.RemoveShortTermMemory, description: $"删除一段短期记忆。"),
                "RemoveShortTermMemories" => AIFunctionFactory.Create(Memory.RemoveShortTermMemories, description: $"批量删除删除短期记忆。"),
                "AddToDoItem" => Context != null ? AIFunctionFactory.Create(AddToDoItem, description: $"添加一个TODO，当isGlobal为 true 时，你在所有对话中都可以看到这条TODO。否则只能在当前上下文中看到") : null,
                "CompleteToDoItem" => AIFunctionFactory.Create(Memory.CompleteToDoItem, description: $"置一个TODO为完成状态。"),
                "RemoveToDoItem" => AIFunctionFactory.Create(Memory.RemoveToDoItem, description: $"删除一条TODO。"),
                "RemoveToDoItems" => AIFunctionFactory.Create(Memory.RemoveToDoItems, description: $"批量删除TODO。"),
                "AddLongTermMemory" => AIFunctionFactory.Create(Memory.AddLongTermMemory, description:
                    """
                    添加一段长期记忆。长期记忆是与身份（QQ号）相关联的持久化信息，可以跨会话保存。参数为一句详细描述的自然语言描述的记忆内容，例如：`用户2025-10-13中午吃了焖子。`、`用户和我详细探讨了NLP的学习路线，他表示要好好学习NLP的知识。`
                    """),
                "GetLongTermMemories" => AIFunctionFactory.Create(Memory.GetLongTermMemories, description:
                    """
                    查询与当前用户（QQ号）相关联的长期记忆列表。返回值为一个字符串数组，每个元素是一段记忆的描述。
                    """),
                "AddKnowledge" => AIFunctionFactory.Create(Memory.AddKnowledge, description:
                    """
                    向知识库中添加一条知识。知识是一句无歧义、描述准确、原子化的自然语言文本。添加时尽量保留主体以及强逻辑性，例如：`xswl是一句网络用语，是"笑死我了"的中文缩写。`、`原神是一款由米哈游开发的二次元开放世界游戏。`或者`丰川祥子是《BanG Dream!》及其衍生作品中的角色，Ave Mujica乐队的键盘手，代号Oblivionis。曾是CRYCHIC乐队的键盘手，后因性格转变退出，之后主导组建Ave Mujica并担任键盘手和作曲。`
                    """),
                "GetKnowledges" => AIFunctionFactory.Create(Memory.GetKnowledges, description:
                    """
                    查询知识库内容。返回值为一个字符串数组，每个元素是一条知识的自然语言文本。
                    """),

                #endregion

                #region CQApi
                "GetLoginQQ" => AIFunctionFactory.Create(MainSave.CQApi.GetLoginQQ,
                    description: "获取当前登录的QQ账号对象，无参数。返回值：QQ对象，表示当前登录账号。"),

                "GetLoginNick" => AIFunctionFactory.Create(MainSave.CQApi.GetLoginNick,
                    description: "获取当前登录的QQ昵称，无参数。返回值：string，当前登录账号的昵称。"),

                "GetFriendList" => AIFunctionFactory.Create(MainSave.CQApi.GetFriendList,
                    description: "获取当前账号的好友列表，无参数。返回值：FriendInfoCollection，好友列表集合。"),

                "GetGroupList" => AIFunctionFactory.Create(MainSave.CQApi.GetGroupList,
                    description: "获取当前账号的群列表，无参数。返回值：GroupInfoCollection，群列表集合。"),

                "GetGroupMemberList" => AIFunctionFactory.Create(MainSave.CQApi.GetGroupMemberList,
                    description: "获取指定群的成员列表。参数：groupId(long) - 目标群号。返回值：GroupMemberInfoCollection，群成员列表集合。"),

                "GetGroupMemberInfo" => AIFunctionFactory.Create(MainSave.CQApi.GetGroupMemberInfo,
                    description: "获取指定群成员的信息。参数：groupId(long) - 群号；qqId(long) - QQ号；notCache(bool, 可选，默认 false) - 是否不使用缓存。返回值：GroupMemberInfo，群成员信息对象。"),

                "GetGroupInfo" => AIFunctionFactory.Create(MainSave.CQApi.GetGroupInfo,
                    description: "获取指定群的群信息。参数：groupId(long) - 群号；notCache(bool, 可选，默认 false) - 是否不使用缓存。返回值：GroupInfo，群信息对象。"),

                "RemoveMessage" => CheckIsAdmin(Context)
                    ? AIFunctionFactory.Create(new Func<int, bool>(MainSave.CQApi.RemoveMessage),
                        description: "撤回指定消息。参数：msgId(int) - 消息ID。返回值：bool，操作成功返回 true，失败返回 false。")
                    : null,

                "SetGroupMemberBanSpeak" => CheckIsAdmin(Context)
                    ? AIFunctionFactory.Create(MainSave.CQApi.SetGroupMemberBanSpeak,
                        description: "设置指定群成员禁言。参数：groupId(long) - 群号；qqId(long) - QQ号；time(TimeSpan) - 禁言时长（范围：1秒 ~ 30天）。返回值：bool，操作成功返回 true，失败返回 false。")
                    : null,

                "RemoveGroupMemberBanSpeak" => CheckIsAdmin(Context)
                    ? AIFunctionFactory.Create(MainSave.CQApi.RemoveGroupMemberBanSpeak,
                        description: "解除指定群成员禁言。参数：groupId(long) - 群号；qqId(long) - QQ号。返回值：bool，操作成功返回 true，失败返回 false。")
                    : null,

                "SetGroupBanSpeak" => CheckIsAdmin(Context)
                    ? AIFunctionFactory.Create(MainSave.CQApi.SetGroupBanSpeak,
                        description: "设置群全体禁言。参数：groupId(long) - 群号。返回值：bool，操作成功返回 true，失败返回 false。")
                    : null,

                "RemoveGroupBanSpeak" => CheckIsAdmin(Context)
                    ? AIFunctionFactory.Create(MainSave.CQApi.RemoveGroupBanSpeak,
                        description: "解除群全体禁言。参数：groupId(long) - 群号。返回值：bool，操作成功返回 true，失败返回 false。")
                    : null,

                "SetGroupMemberVisitingCard" => CheckIsAdmin(Context)
                    ? AIFunctionFactory.Create(MainSave.CQApi.SetGroupMemberVisitingCard,
                        description: "设置群成员名片。参数：groupId(long) - 群号；qqId(long) - QQ号；newName(string) - 新名片。返回值：bool，操作成功返回 true，失败返回 false。")
                    : null,

                "SetGroupMemberForeverExclusiveTitle" => CheckIsAdmin(Context)
                    ? AIFunctionFactory.Create(MainSave.CQApi.SetGroupMemberForeverExclusiveTitle,
                        description: "设置群成员永久专属头衔。参数：groupId(long) - 群号；qqId(long) - QQ号；newTitle(string) - 新头衔。返回值：bool，操作成功返回 true，失败返回 false。")
                    : null,

                "RemoveGroupMember" => CheckIsAdmin(Context)
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
            if (!string.IsNullOrEmpty(identity) && identity == Context.ExtraIdentity)
            {
                long groupId = Context.GroupId;
                long qq = Context.QQ;

                if (groupId > 0)
                {
                    MainSave.CQApi.SendGroupMessage(groupId, sliceMessage);
                }
                else
                {
                    MainSave.CQApi.SendPrivateMessage(qq, sliceMessage);
                }
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
            Context.ExtraIdentity = extraIdentity;
            MCPClientManager manager = Context.MCPClientManager;
            _ = Task.Run(async () =>
            {
                await Task.Delay(delaySeconds * 1000);
                MainSave.CQLog?.Info("延时任务", "延时任务触发。");
                Chat.OnToolCall -= Chat_OnToolCall;
                Chat.OnToolCall += Chat_OnToolCall;
                var response = Chat.GetChatResult(AppConfig.ChatAPIKeyId, [
                        new(ChatRole.System, prompt),
                                new(ChatRole.User, $"此消息为延时后发起的对话，你在上一轮的留言是：{extraPrompt}")
                    ], Chat.Purpose.聊天, identity: extraIdentity, timeout: AppConfig.ChatTimeout, mcp: manager);
                MainSave.CQLog?.Info("延时任务", $"延时任务的回复为{response}");
                if (response != Chat.ErrorMessage && !response.Contains(AppConfig.ChatEmptyResponse))
                {
                    if (groupId > 0)
                    {
                        MainSave.CQApi.SendGroupMessage(groupId, response);
                    }
                    else
                    {
                        MainSave.CQApi.SendPrivateMessage(qq, response);
                    }
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

        public override AIFunction[] GetTools()
        {
            var client = McpClientFactory.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = Command,
                Arguments = Arguments.ToArray(),
                EnvironmentVariables = EnvironmentVariables,
                WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? null : WorkingDirectory,
                Name = Name
            })).Result;
            return ListToolsFromMCPClient(client);
        }
    }

    public class MCPHttpClient : MCPClientBase
    {
        public override MCPClientType ToolType { get; set; } = MCPClientType.Http;

        public HttpTransportMode TransportType { get; set; } = HttpTransportMode.AutoDetect;

        public string Endpoint { get; set; } = string.Empty;

        public Dictionary<string, string> Headers { get; set; } = [];

        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);

        public override AIFunction[] GetTools()
        {
            var client = McpClientFactory.CreateAsync(new SseClientTransport(new SseClientTransportOptions
            {
                Name = Name,
                Endpoint = new Uri(Endpoint),
                AdditionalHeaders = Headers,
                TransportMode = TransportType,
                ConnectionTimeout = ConnectionTimeout
            })).Result;
            return ListToolsFromMCPClient(client);
        }
    }
}
