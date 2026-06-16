using System.Text.Json;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Model.MCP;

/// <summary>
/// Manages all MCP clients and provides per-conversation tool aggregation.
/// Loads/saves MCP configuration from MCP.json.
/// </summary>
public class MCPClientManager
{
    public static List<MCPClientBase> Clients { get; set; } = [];

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
        try
        {
            var path = Path.Combine(appDirectory, "MCP.json");
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var wrapper = JsonSerializer.Deserialize<McpConfigWrapper>(json);
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
        if (!AppConfig.EnableMCP) return;

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
    public ToolDefinition[] GetToolsForConversation(long groupId, long qq)
    {
        var tools = new List<ToolDefinition>();

        foreach (var client in Clients)
        {
            if (!CanUseClient(client, groupId, qq)) continue;

            try
            {
                var clientTools = client.GetToolsAsync().Result;
                foreach (var tool in clientTools)
                {
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
    public async Task<object?> ExecuteToolAsync(ToolCallRequest toolCall, CancellationToken ct)
    {
        foreach (var client in Clients)
        {
            try
            {
                var tools = await client.GetToolsAsync();
                if (tools.Any(t => t.Function.Name == toolCall.Function.Name))
                {
                    // Execute the tool via the MCP client
                    // For custom clients, this is an in-process call
                    // For external clients, this goes through ModelContextProtocol
                    return await ExecuteOnClientAsync(client, toolCall, ct);
                }
            }
            catch { }
        }

        return $"Tool '{toolCall.Function.Name}' not found";
    }

    private async Task<object?> ExecuteOnClientAsync(MCPClientBase client, ToolCallRequest toolCall, CancellationToken ct)
    {
        if (client is MCPCustomClient customClient)
        {
            return await customClient.ExecuteToolAsync(toolCall, ct);
        }

        // For external MCP clients, use ModelContextProtocol to invoke
        // TODO: Phase 7 — implement external MCP tool invocation
        return null;
    }

    private static bool CanUseClient(MCPClientBase client, long groupId, long qq)
    {
        if (!client.Enabled) return false;
        if (client.CanOnlyMasterCall && !AppConfig.MasterQQ.Contains(qq)) return false;

        if (groupId > 0 && client.GroupEnabled)
        {
            if (client.IsGroupBlackList)
                return !client.Groups.Contains(groupId);
            return client.Groups.Contains(groupId);
        }

        if (groupId == 0 && qq > 0 && client.PersonEnabled)
        {
            if (client.IsPersonBlackList)
                return !client.Persons.Contains(qq);
            return client.Persons.Contains(qq);
        }

        return false;
    }

    private class McpConfigWrapper
    {
        public List<MCPClientBase> Clients { get; set; } = [];
    }
}
