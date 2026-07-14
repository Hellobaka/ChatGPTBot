namespace ChatGPTv3.Core.Model.MCP;

/// <summary>
/// External MCP client via SSE (Server-Sent Events) transport.
/// </summary>
public class MCPSSEClient : MCPHttpClient
{
    public override MCPClientType ToolType { get; set; } = MCPClientType.SSE;

    public MCPSSEClient()
    {
        TransportMode = "SSE";
    }
}
