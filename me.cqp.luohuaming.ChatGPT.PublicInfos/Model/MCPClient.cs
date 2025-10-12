using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model.CustomTools;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;

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

        public override AIFunction[] GetTools()
        {
            var function = Name switch
            {
                "GetCityIdByName" => AIFunctionFactory.Create(MojiCityIdConverter.GetCityIdByName, description: "用于墨迹天气接口中，城市名称转换为 cityId。"),
                "UpdateMood" => AIFunctionFactory.Create(MoodManager.Instance.UpdateMood, description: "更新你发言后的心情状态，有以下枚举可选：happy，angry，sad，surprised，disgusted，fearful，neutral"),
                "UpdateFavorability" => Context != null ? AIFunctionFactory.Create(typeof(Relationship).GetMethod("UpdateFavorability"), Relationship.GetRelationShip(Context.GroupId, Context.QQ), description: "更新你对目标发言者的好感度，可用范围 [-10, 10]。") : null,
                "GetRelationShip" => AIFunctionFactory.Create(Relationship.GetRelationShip, description: "获取你对目标发言者的好感度/关系，请根据当前上下文提供的GroupID与QQ作为参数，当私聊场景时，GroupId为-1。"),
                "GetGroupChatHistory" => AIFunctionFactory.Create(ChatRecord.GetGroupChatRecord, description: "获取群聊的聊天记录，请根据当前上下文提供的GroupID与QQ作为参数。当QQ为0时，表示不限制发言人"),
                "GetPrivateChatHistory" => AIFunctionFactory.Create(ChatRecord.GetPrivateChatRecord, description: "获取私聊的聊天记录，请根据当前上下文提供的QQ作为参数"),
                "GetChatHistoryByIds" => AIFunctionFactory.Create(ChatRecord.GetChatRecordByIds, description: "通过消息ID列表获取聊天记录"),
                "GetRangeUsageDetail" => AIFunctionFactory.Create(Usage.GetRangeUsageDetail, description: "获取Token消耗情况"),
                "AddPictureToContext" => Context != null ? AIFunctionFactory.Create((string hash) =>
                {
                    MainSave.CQLog?.Info("调用 AddPictureToContext", $"将图片 {hash} 添加到上下文 {Context.ChatIdentity} 中");
                    PictureContextManager.AddPicture(Context.ChatIdentity, hash);
                }, description: "用于将图片原生插入上下文中，当你想从目标图片获取更详细更原生更完备的信息时可以调用这个。参数为上下文提供的图片Hash") : null,
                "AddDelayTask" => Context != null ? AIFunctionFactory.Create((CustomToolContext context, int delaySeconds, string extraPrompt) =>
                {
                    MainSave.CQLog?.Info("调用 AddDelayTask", $"延时 {delaySeconds} 秒");
                }, description: "当你认为需要等待一段时间后才能进行某项任务时，可以调用此函数。将在延时某些秒数之后，将你的言论附加到当前Prompt中，并再次发起一轮对话。") : null,

                #region CQApi
                "GetLoginQQ" => AIFunctionFactory.Create(MainSave.CQApi.GetLoginQQ, description: "获取当前登录的QQ账号对象，无参数。返回值：QQ对象，表示当前登录账号。"),
                "GetLoginNick" => AIFunctionFactory.Create(MainSave.CQApi.GetLoginNick, description: "获取当前登录的QQ昵称，无参数。返回值：string，当前登录账号的昵称。"),
                "GetFriendList" => AIFunctionFactory.Create(MainSave.CQApi.GetFriendList, description: "获取当前账号的好友列表，无参数。返回值：FriendInfoCollection，好友列表集合。"),
                "GetGroupList" => AIFunctionFactory.Create(MainSave.CQApi.GetGroupList, description: "获取当前账号的群列表，无参数。返回值：GroupInfoCollection，群列表集合。"),
                "GetGroupMemberList" => AIFunctionFactory.Create(MainSave.CQApi.GetGroupMemberList, description: "获取指定群的成员列表。参数：groupId(long) - 目标群号。返回值：GroupMemberInfoCollection，群成员列表集合。"),
                "GetGroupMemberInfo" => AIFunctionFactory.Create(MainSave.CQApi.GetGroupMemberInfo, description: "获取指定群成员的信息。参数：groupId(long) - 群号；qqId(long) - QQ号；notCache(bool, 可选，默认 false) - 是否不使用缓存。返回值：GroupMemberInfo，群成员信息对象。"),
                "GetGroupInfo" => AIFunctionFactory.Create(MainSave.CQApi.GetGroupInfo, description: "获取指定群的群信息。参数：groupId(long) - 群号；notCache(bool, 可选，默认 false) - 是否不使用缓存。返回值：GroupInfo，群信息对象。"),
                "RemoveMessage" => CheckIsAdmin(Context) ? AIFunctionFactory.Create(new Func<int, bool>(MainSave.CQApi.RemoveMessage), description: "撤回指定消息。参数：msgId(int) - 消息ID。返回值：bool，操作成功返回 true，失败返回 false。"): null,
                "SetGroupMemberBanSpeak" => CheckIsAdmin(Context) ? AIFunctionFactory.Create(MainSave.CQApi.SetGroupMemberBanSpeak, description: "设置指定群成员禁言。参数：groupId(long) - 群号；qqId(long) - QQ号；time(TimeSpan) - 禁言时长（1秒~30天）。返回值：bool，操作成功返回 true，失败返回 false。") : null,
                "RemoveGroupMemberBanSpeak" => CheckIsAdmin(Context) ? AIFunctionFactory.Create(MainSave.CQApi.RemoveGroupMemberBanSpeak, description: "解除指定群成员禁言。参数：groupId(long) - 群号；qqId(long) - QQ号。返回值：bool，操作成功返回 true，失败返回 false。") : null,
                "SetGroupBanSpeak" => CheckIsAdmin(Context) ? AIFunctionFactory.Create(MainSave.CQApi.SetGroupBanSpeak, description: "设置群全体禁言。参数：groupId(long) - 群号。返回值：bool，操作成功返回 true，失败返回 false。") : null,
                "RemoveGroupBanSpeak" => CheckIsAdmin(Context) ? AIFunctionFactory.Create(MainSave.CQApi.RemoveGroupBanSpeak, description: "解除群全体禁言。参数：groupId(long) - 群号。返回值：bool，操作成功返回 true，失败返回 false。") : null,
                "SetGroupMemberVisitingCard" => CheckIsAdmin(Context) ? AIFunctionFactory.Create(MainSave.CQApi.SetGroupMemberVisitingCard, description: "设置群成员名片。参数：groupId(long) - 群号；qqId(long) - QQ号；newName(string) - 新名片。返回值：bool，操作成功返回 true，失败返回 false。") : null,
                "SetGroupMemberForeverExclusiveTitle" => CheckIsAdmin(Context) ? AIFunctionFactory.Create(MainSave.CQApi.SetGroupMemberForeverExclusiveTitle, description: "设置群成员永久专属头衔。参数：groupId(long) - 群号；qqId(long) - QQ号；newTitle(string) - 新头衔。返回值：bool，操作成功返回 true，失败返回 false。") : null,
                "RemoveGroupMember" => CheckIsAdmin(Context) ? AIFunctionFactory.Create(MainSave.CQApi.RemoveGroupMember, description: "移除群成员（踢人）。参数：groupId(long) - 群号；qqId(long) - QQ号；notRequest(bool, 可选，默认 false) - 是否不再接收该成员加群申请。返回值：bool，操作成功返回 true，失败返回 false。") : null,
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
