using System.Text.Json.Serialization;
using ModelContextProtocol.Client;

namespace ChatGPTv3.Core.Model.MCP;

/// <summary>
/// External MCP client via HTTP transport (auto-detect Streamable HTTP or SSE).
/// </summary>
public class MCPHttpClient : MCPExternalClient
{
    public override MCPClientType ToolType { get; set; } = MCPClientType.Http;

    [JsonPropertyName("Endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [JsonPropertyName("TransportMode")]
    public string TransportMode { get; set; } = "AutoDetect";

    protected override Task<McpClient> CreateClientAsync()
    {
        var mode = Enum.TryParse<HttpTransportMode>(TransportMode, ignoreCase: true, out var m)
            ? m : HttpTransportMode.AutoDetect;

        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(Endpoint),
            TransportMode = mode
        });

        return McpClient.CreateAsync(transport, new McpClientOptions
        {
            ClientInfo = new() { Name = Name, Version = "1.0" }
        });
    }
}
