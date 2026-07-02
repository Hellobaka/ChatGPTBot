using ModelContextProtocol.Client;
using System.Text.Json.Serialization;

namespace ChatGPTv3.Core.Model.MCP;

/// <summary>
/// External MCP client via stdio subprocess transport.
/// </summary>
public class MCPStdioClient : MCPExternalClient
{
    public override MCPClientType ToolType { get; set; } = MCPClientType.Stdio;

    [JsonPropertyName("Command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("Arguments")]
    public string[] Arguments { get; set; } = [];

    protected override Task<McpClient> CreateClientAsync()
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = Name,
            Command = Command,
            Arguments = Arguments
        });

        return McpClient.CreateAsync(transport, new McpClientOptions
        {
            ClientInfo = new() { Name = Name, Version = "1.0" }
        });
    }
}