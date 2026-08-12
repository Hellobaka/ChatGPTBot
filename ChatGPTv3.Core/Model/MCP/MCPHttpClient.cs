using ModelContextProtocol.Client;
using System.Text.Json.Serialization;

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
    public string TransportMode { get; set; } = "StreamableHttp";

    /// <summary>Custom HTTP headers sent with every request to this endpoint.</summary>
    [JsonPropertyName("Headers")]
    public Dictionary<string, string> Headers { get; set; } = [];

    protected override Task<McpClient> CreateClientAsync()
    {
        // Explicit transport only — never auto-detect.
        var mode = Enum.TryParse<HttpTransportMode>(TransportMode, ignoreCase: true, out var m)
            && m != HttpTransportMode.AutoDetect
            ? m
            : HttpTransportMode.StreamableHttp;

        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(Endpoint),
            TransportMode = mode,
            AdditionalHeaders = Headers,
            MaxReconnectionAttempts = 5,
            DefaultReconnectionInterval = TimeSpan.FromSeconds(1)
        });

        return McpClient.CreateAsync(transport);
    }
}
