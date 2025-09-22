using ModelContextProtocol.Client;
using System;
using System.Collections.Generic;

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

        public virtual IMcpClient Create()
        {
            throw new NotImplementedException();
        }
    }

    public class MCPStdioClient : MCPClientBase
    {
        public override MCPClientType ToolType { get; set; } = MCPClientType.STDIO;

        public string Command { get; set; } = string.Empty;

        public List<string> Arguments { get; set; } = [];

        public Dictionary<string, string> EnvironmentVariables { get; set; } = [];

        public string WorkingDirectory { get; set; } = string.Empty;

        public override IMcpClient Create()
        {
            return McpClientFactory.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
            {
                Command = Command,
                Arguments = Arguments.ToArray(),
                EnvironmentVariables = EnvironmentVariables,
                WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? null : WorkingDirectory,
                Name = Name
            })).Result;
        }
    }

    public class MCPHttpClient : MCPClientBase
    {
        public override MCPClientType ToolType { get; set; } = MCPClientType.Http;

        public HttpTransportMode TransportType { get; set; } = HttpTransportMode.AutoDetect;

        public string Endpoint { get; set; } = string.Empty;

        public Dictionary<string, string> Headers { get; set; } = [];

        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);

        public override IMcpClient Create()
        {
            return McpClientFactory.CreateAsync(new SseClientTransport(new SseClientTransportOptions
            {
                Name = Name,
                Endpoint = new Uri(Endpoint),
                AdditionalHeaders = Headers,
                TransportMode = TransportType,
                ConnectionTimeout = ConnectionTimeout
            })).Result;
        }
    }
}
