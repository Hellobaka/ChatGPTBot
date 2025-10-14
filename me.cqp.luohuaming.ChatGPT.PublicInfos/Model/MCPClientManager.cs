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
    public class MCPClientManager(long groupId, long qqId, string chatIdentity, string prompt)
    {
        public static List<MCPClientBase> Clients { get; set; } = [];

        public static Dictionary<MCPClientBase, AIFunction[]> MCPTools { get; set; } = [];

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
                        Clients = mcp[nameof(Clients)].ToObject<List<MCPClientBase>>(new JsonSerializer() { TypeNameHandling = TypeNameHandling.Auto });
                        CreateCustomClientsIfNeeded();
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
            if (!AppConfig.EnableMCP)
            {
                return;
            }
            MCPTools = [];
            CreateCustomClientsIfNeeded();
            Parallel.ForEach(Clients, client =>
            {
                try
                {
                    MCPTools.Add(client, client.GetTools());
                }
                catch (Exception e)
                {
                    MainSave.CQLog?.Error("加载MCP客户端", $"加载MCP客户端 {client.Name} 失败，错误信息：{e}");
                }
            });
        }

        public AIFunction[] GetAIFunctions()
        {
            AIFunction[] functions = [];
            foreach (var item in MCPTools)
            {
                if (!CheckClientCanBuild(item.Key, groupId, qqId))
                {
                    continue;
                }
                if (item.Key is MCPCustomClient customClient)
                {
                    customClient.Context = new CustomToolContext(groupId, qqId, chatIdentity, prompt, this);
                    functions = [.. functions, .. customClient.GetTools()];
                }
                else
                {
                    functions = [.. functions, .. item.Value];
                }
            }
            CommonHelper.DebugLog("构建工具列表", $"共添加了 {functions.Length} 个工具");
            return functions;
        }

        private static void CreateCustomClientsIfNeeded()
        {
            foreach (var tool in MCPCustomClient.CustomToolNames.Where(x => !Clients.Any(o => o.Name == x)))
            {
                Clients.Add(new MCPCustomClient
                {
                    Name = tool,
                    Enabled = false
                });
            }
        }

        private bool CheckClientCanBuild(MCPClientBase client, long groupId, long personId)
        {
            if (!client.Enabled)
            {
                return false;
            }
            if (client.CanOnlyMasterCall && !AppConfig.MasterQQ.Any(x => x == personId))
            {
                return false;
            }
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
            MainSave.CQLog?.Info("调用 MCP 工具", $"调用工具：{tool.Name}，参数：{System.Text.Json.JsonSerializer.Serialize(arg, options)}");
        }
    }
}
