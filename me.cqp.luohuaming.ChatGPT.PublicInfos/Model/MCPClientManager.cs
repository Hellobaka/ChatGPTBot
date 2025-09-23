using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model.CustomTools;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.Model
{
    public class MCPClientManager(long groupId, long qqId)
    {
        public static List<MCPClientBase> Clients { get; set; } = [];

        public static Dictionary<MCPClientBase, AIFunction[]> MCPTools { get; set; } = [];

        private Relationship? RelationshipContext { get; set; }

        public static JsonSerializerSettings JsonSerializerSettings { get; set; } = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Formatting.Indented
        };

        public static void Load()
        {
            try
            {
                string path = Path.Combine(MainSave.AppDirectory, "MCP.json");
                if (File.Exists(path))
                {
                    var mcp = JObject.Parse(File.ReadAllText(path));
                    if (mcp != null)
                    {
                        Clients = mcp[nameof(Clients)].ToObject<List<MCPClientBase>>(new JsonSerializer() { TypeNameHandling = TypeNameHandling.Auto});
                    }
                }
            }
            catch (Exception e)
            {
                MainSave.CQLog?.Error("加载MCP客户端", $"加载MCP客户端失败，错误信息：{e.Message}");
            }
        }

        public static void Save()
        {
            string path = Path.Combine(MainSave.AppDirectory, "MCP.json");
            try
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(new
                {
                    Clients,
                }, JsonSerializerSettings));
            }
            catch (Exception e)
            {
                MainSave.CQLog?.Error("保存MCP客户端", $"保存MCP客户端失败，错误信息：{e.Message}");
            }
        }

        public static void Rebuild()
        {
            foreach (var item in MCPTools)
            {
                foreach (McpClientTool tool in item.Value.Where(x => x is McpClientTool))
                {
                    tool.BeforeToolCalled -= Tool_BeforeToolCalled;
                }
            }
            MCPTools = [];
            Parallel.ForEach(Clients, client =>
            {
                try
                {
                    MCPTools.Add(client, []);
                    var mcpClient = client.Create();
                    var functions = mcpClient.ListToolsAsync().Result;
                    foreach (var item in functions)
                    {
                        if (client.ToolNameConverters.TryGetValue(item.Name, out var newName))
                        {
                            var renamedTool = item.WithName(newName);
                            renamedTool.BeforeToolCalled += Tool_BeforeToolCalled;
                            MCPTools[client] = [.. MCPTools[client], renamedTool];
                        }
                        else
                        {
                            item.BeforeToolCalled += Tool_BeforeToolCalled;
                            MCPTools[client] = [.. MCPTools[client], item];
                        }
                    }
                }
                catch (Exception e)
                {
                    MainSave.CQLog?.Error("加载MCP客户端", $"加载MCP客户端 {client.Name} 失败，错误信息：{e.Message}");
                }
            });
        }

        public AIFunction[] GetAIFunctions()
        {
            AIFunction[] functions = [.. CreateCustomTools()];
            foreach (var item in MCPTools)
            {
                if (!CheckClientCanBuild(item.Key, groupId, qqId))
                {
                    continue;
                }

                functions = [.. functions, .. item.Value];
            }
            return functions;
        }

        public void UpdateRelationshipContext(Relationship relationship)
        {
            RelationshipContext = relationship;
        }

        public AIFunction[] CreateCustomTools()
        {
            List<AIFunction> custom = [];
            custom.Add(AIFunctionFactory.Create(MojiCityIdConverter.GetCityIdByName, description: "用于墨迹天气接口中，城市名称转换为 cityId。"));
            custom.Add(AIFunctionFactory.Create(MoodManager.Instance.UpdateMood, description: "更新你发言后的心情状态，有以下枚举可选：happy，angry，sad，surprised，disgusted，fearful，neutral"));
            if (RelationshipContext != null)
            {
                custom.Add(AIFunctionFactory.Create(RelationshipContext.GetType().GetMethod("UpdateFavorability"), RelationshipContext, description: "更新你对目标发言者的好感度，可用范围 [-10, 10]"));
            }
            // TODO: 重构记忆模块
            // custom.Add(AIFunctionFactory.Create(InsertMemory, description: "用自然语言描述你想记住的内容，注意区分对话主体"));
            return custom.ToArray();
        }

        private bool CheckClientCanBuild(MCPClientBase client, long groupId, long personId)
        {
            if (groupId > 0 && client.GroupEnabled)
            {
                if (client.IsGroupBlackList && client.Groups.Contains(groupId))
                {
                    return false;
                }
                if (!client.IsGroupBlackList && !client.Groups.Contains(groupId))
                {
                    return false;
                }
                return true;
            }
            if (groupId == 0 && personId > 0 && client.PersonEnabled)
            {
                if (client.IsPersonBlackList && client.Persons.Contains(personId))
                {
                    return false;
                }
                if (!client.IsPersonBlackList && !client.Persons.Contains(personId))
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        private static void Tool_BeforeToolCalled(McpClientTool tool, IReadOnlyDictionary<string, object?>? arg)
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };
            MainSave.CQLog?.Info($"调用工具：{tool.Name}，参数：{System.Text.Json.JsonSerializer.Serialize(arg, options)}");
        }
    }
}
