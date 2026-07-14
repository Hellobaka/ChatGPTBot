using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;
using System.Text.Json;

namespace ChatGPTv3.Core.Model.MCP;

/// <summary>
/// Manages all MCP clients and provides per-conversation tool aggregation.
/// Loads/saves MCP configuration from MCP.json.
/// </summary>
public class MCPClientManager
{
    public static List<MCPClientBase> Clients { get; set; } = [];

    private static string _appDir = string.Empty;

    // ── Persistence (for future UI) ────────────────────────

    public static void Save()
    {
        if (string.IsNullOrEmpty(_appDir))
        {
            return;
        }

        Save(_appDir);
    }

    // ── CRUD (for future UI) ───────────────────────────────

    public static void AddClient(MCPClientBase client)
    {
        Clients.Add(client);
        Save();
        client.Start();
    }

    public static void RemoveClient(string name)
    {
        var client = Clients.FirstOrDefault(c => c.Name == name);
        if (client != null)
        {
            client.Stop();
            Clients.Remove(client);
            Save();
        }
    }

    public static void UpdateClient(string name, Action<MCPClientBase> update)
    {
        var client = Clients.FirstOrDefault(c => c.Name == name);
        if (client != null)
        {
            update(client);
            Save();
            client.Stop();
            client.Start(); // reconnect with new settings
        }
    }

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Load MCP client configurations from disk.
    /// </summary>
    public static void Load(string appDirectory)
    {
        _appDir = appDirectory;
        try
        {
            var path = Path.Combine(appDirectory, "MCP.json");
            if (!File.Exists(path))
            {
                return;
            }

            var json = File.ReadAllText(path);
            var wrapper = JsonSerializer.Deserialize<McpConfigWrapper>(json, JsonOptions);
            if (wrapper?.Clients != null)
            {
                Clients = wrapper.Clients;
                EnsureCustomClientsExist();
            }
        }
        catch (Exception ex)
        {
            CommonHelper.LogWarning?.Invoke("MCP", $"加载失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Save MCP client configurations to disk.
    /// </summary>
    public static void Save(string appDirectory)
    {
        try
        {
            var path = Path.Combine(appDirectory, "MCP.json");
            var json = JsonSerializer.Serialize(new McpConfigWrapper { Clients = Clients }, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            CommonHelper.LogWarning?.Invoke("MCP", $"保存失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Rebuilds all MCP clients (reconnect transports, refresh tool lists).
    /// </summary>
    public static void Rebuild()
    {
        if (!AppConfig.EnableMCP)
        {
            return;
        }

        EnsureCustomClientsExist();

        Parallel.ForEach(Clients, client =>
        {
            try
            {
                client.Stop();
                client.GetToolsAsync().Wait();
                client.Start();
            }
            catch (Exception ex)
            {
                CommonHelper.LogWarning?.Invoke("MCP", $"Rebuild {client.Name} 失败: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Ensures built-in custom tool types are registered as client entries.
    /// </summary>
    private static void EnsureCustomClientsExist()
    {
        var customToolNames = MCPSelfBuiltinTools.GetBuiltinToolNames();
        foreach (var name in customToolNames)
        {
            if (!Clients.Any(c => c.Name == name))
            {
                Clients.Add(new MCPCustomClient { Name = name, Enabled = false });
            }
        }
    }

    /// <summary>
    /// Gets AI functions (ToolDefinition[]) for a specific conversation context.
    /// Filters by permissions (group/QQ whitelist, master-only, disabled).
    /// </summary>
    public static ToolDefinition[] GetToolsForConversation(MCPToolContext ctx)
    {
        var tools = new List<ToolDefinition>();

        foreach (var client in Clients)
        {
            if (!client.Enabled)
            {
                continue;
            }

            try
            {
                var clientTools = client.GetToolsAsync().Result;
                foreach (var tool in clientTools)
                {
                    if (!CanUseClient(client, ctx, tool.Function.Name))
                    {
                        continue;
                    }

                    // Apply name converters
                    if (client.ToolNameConverters.TryGetValue(tool.Function.Name, out var newName))
                    {
                        tool.Function.Name = newName;
                    }

                    tools.Add(tool);
                }
            }
            catch (Exception ex)
            {
                CommonHelper.LogWarning?.Invoke("MCP", $"GetTools {client.Name}: {ex.Message}");
            }
        }

        return tools.ToArray();
    }

    /// <summary>
    /// Executes a tool call against the appropriate MCP client.
    /// </summary>
    public static async Task<object?> ExecuteToolAsync(ToolCallRequest toolCall, CancellationToken ct)
        => await ExecuteToolAsync(toolCall, ct, null);

    /// <summary>
    /// Executes a tool call with an explicit <see cref="MCPToolContext"/>.
    /// When provided, the context is passed directly to the tool handler,
    /// avoiding reliance on the mutable <see cref="MCPCustomClient.Context"/> property.
    /// This prevents race conditions when concurrent callers share the same client instance.
    /// </summary>
    public static async Task<object?> ExecuteToolAsync(ToolCallRequest toolCall, CancellationToken ct, MCPToolContext? context)
    {
        foreach (var client in Clients)
        {
            try
            {
                var tools = await client.GetToolsAsync();
                if (tools.Any(t => t.Function.Name == toolCall.Function.Name))
                {
                    // Re-apply permission check for execution
                    if (context != null && context.ChatIdentity != "管理面板工具测试" && !CanUseClient(client, context, toolCall.Function.Name))
                    {
                        return $"[{client.Name}] 工具 '{toolCall.Function.Name}' 无权限";
                    }

                    return await ExecuteOnClientAsync(client, toolCall, ct, context);
                }
            }
            catch { }
        }

        return $"Tool '{toolCall.Function.Name}' not found";
    }

    private static async Task<object?> ExecuteOnClientAsync(MCPClientBase client, ToolCallRequest toolCall, CancellationToken ct, MCPToolContext? context = null)
    {
        if (client is MCPCustomClient customClient)
        {
            return await customClient.ExecuteToolAsync(toolCall, ct, context);
        }

        if (client is MCPExternalClient extClient)
        {
            return await extClient.ExecuteToolAsync(toolCall, ct);
        }

        return $"Unknown client type: {client.GetType().Name}";
    }

    /// <summary>
    /// Gets tools for a named client, without conversation context filtering.
    /// For UI inspection only — does not check permissions or apply name converters.
    /// </summary>
    public static async Task<ToolDefinition[]> GetToolsForClientAsync(string name)
    {
        var client = Clients.FirstOrDefault(c => c.Name == name);
        if (client == null)
        {
            return [];
        }

        try
        {
            return await client.GetToolsAsync();
        }
        catch (Exception ex)
        {
            CommonHelper.LogWarning?.Invoke("MCP", $"GetToolsForClient {name}: {ex.Message}");
            return [];
        }
    }

    private static bool CanUseClient(MCPClientBase client, MCPToolContext ctx, string toolName = "")
    {
        if (!client.Enabled)
        {
            return false;
        }

        var perm = client.PerToolPermissions.TryGetValue(toolName, out var tp)
            ? tp
            : null;

        bool groupEnabled = perm?.GroupEnabled ?? client.GroupEnabled;
        bool personEnabled = perm?.PersonEnabled ?? client.PersonEnabled;
        bool canOnlyMasterCall = perm?.CanOnlyMasterCall ?? client.CanOnlyMasterCall;
        bool isGroupBlackList = perm?.IsGroupBlackList ?? client.IsGroupBlackList;
        long[] groups = perm?.Groups ?? client.Groups;
        bool isPersonBlackList = perm?.IsPersonBlackList ?? client.IsPersonBlackList;
        long[] persons = perm?.Persons ?? client.Persons;

        if (canOnlyMasterCall && !AppConfig.MasterQQ.Contains(ctx.QQ))
        {
            return false;
        }

        if (ctx.GroupId > 0 && groupEnabled)
        {
            if (groups.Length == 0)
            {
                return true;
            }

            if (isGroupBlackList)
            {
                return !groups.Contains(ctx.GroupId);
            }

            return groups.Contains(ctx.GroupId);
        }

        if (ctx.GroupId == 0 && ctx.QQ > 0 && personEnabled)
        {
            if (persons.Length == 0)
            {
                return true;
            }

            if (isPersonBlackList)
            {
                return !persons.Contains(ctx.QQ);
            }

            return persons.Contains(ctx.QQ);
        }

        return false;
    }

    private class McpConfigWrapper
    {
        public List<MCPClientBase> Clients { get; set; } = [];
    }
}