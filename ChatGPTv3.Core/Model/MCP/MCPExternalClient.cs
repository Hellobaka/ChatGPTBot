using System.Text.Json;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace ChatGPTv3.Core.Model.MCP;

/// <summary>
/// Base for external MCP clients (Http/Stdio). Handles connection lifecycle,
/// background reconnection (3 retries × 3s, then auto-disable), and fail-fast tool calls.
/// </summary>
public abstract class MCPExternalClient : MCPClientBase
{
    private const int MaxReconnectAttempts = 3;
    private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(2);

    private McpClient? _client;
    private Timer? _reconnectTimer;
    private Timer? _heartbeatTimer;
    private int _reconnectAttempts;
    private volatile bool _connected;
    public bool IsConnected => _connected;

    protected ToolDefinition[] _cachedTools = [];

    protected abstract Task<McpClient> CreateClientAsync();

    // ── Connection ────────────────────────────────────────

    private async Task<bool> ConnectAsync()
    {
        try
        {
            if (_client != null)
            {
                try { await _client.DisposeAsync(); } catch { }
            }
            _client = await CreateClientAsync();
            _connected = true;
            StartHeartbeat();
            CommonHelper.LogInfo?.Invoke("MCP", $"[{Name}] 已连接");
            return true;
        }
        catch (Exception ex)
        {
            CommonHelper.LogWarning?.Invoke("MCP", $"[{Name}] 连接失败: {ex.Message}");
            return false;
        }
    }

    // ── Lifecycle ─────────────────────────────────────────

    public override async void Start()
    {
        await StopAsync();
        if (await ConnectAsync())
            _reconnectAttempts = 0;
    }

    public override async void Stop()
    {
        await StopAsync();
    }

    private async Task StopAsync()
    {
        _connected = false;
        _reconnectTimer?.Dispose();
        _reconnectTimer = null;
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
        _reconnectAttempts = 0;
        if (_client != null)
        {
            try { await _client.DisposeAsync(); } catch { }
            _client = null;
        }
    }

    // ── Heartbeat ─────────────────────────────────────────

    private void StartHeartbeat()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = new Timer(async _ =>
        {
            if (!_connected || _client == null) return;
            try
            {
                await _client.PingAsync();
            }
            catch
            {
                CommonHelper.LogWarning?.Invoke("MCP", $"[{Name}] 心跳失败，触发重连");
                _connected = false;
                ScheduleReconnect();
            }
        }, null, HeartbeatInterval, HeartbeatInterval);
    }

    // ── Background reconnection ───────────────────────────

    private void ScheduleReconnect()
    {
        if (_reconnectTimer != null) return;
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;

        _reconnectTimer = new Timer(async _ =>
        {
            if (_connected) { _reconnectTimer?.Dispose(); _reconnectTimer = null; StartHeartbeat(); return; }

            _reconnectAttempts++;
            if (_reconnectAttempts > MaxReconnectAttempts)
            {
                CommonHelper.LogWarning?.Invoke("MCP",
                    $"[{Name}] {MaxReconnectAttempts} 次重连失败，已禁用");
                Enabled = false;
                _reconnectTimer?.Dispose();
                _reconnectTimer = null;
                return;
            }

            CommonHelper.LogInfo?.Invoke("MCP",
                $"[{Name}] 重连尝试 {_reconnectAttempts}/{MaxReconnectAttempts}");

            if (await ConnectAsync())
            {
                _reconnectAttempts = 0;
                _reconnectTimer?.Dispose();
                _reconnectTimer = null;
            }
        }, null, ReconnectInterval, ReconnectInterval);
    }

    // ── Tool listing ──────────────────────────────────────

    public override async Task<ToolDefinition[]> GetToolsAsync()
    {
        if (_client == null || !_connected)
            return _cachedTools;

        try
        {
            // Manual pagination — handles non-compliant empty-string cursors
            var tools = new List<ToolDefinition>();
            string? cursor = null;
            do
            {
                var result = await _client.ListToolsAsync(
                    new ListToolsRequestParams { Cursor = cursor });

                if (result.Tools != null)
                {
                    foreach (var t in result.Tools)
                    {
                        tools.Add(new ToolDefinition
                        {
                            Type = "function",
                            Function = new FunctionDefinition
                            {
                                Name = t.Name,
                                Description = t.Description ?? "",
                                Parameters = JsonSerializer.SerializeToElement(new
                                {
                                    type = "object",
                                    properties = new { }
                                })
                            }
                        });
                    }
                }

                cursor = result.NextCursor;
            }
            while (!string.IsNullOrEmpty(cursor));

            _cachedTools = tools.ToArray();
            return _cachedTools;
        }
        catch (Exception ex)
        {
            CommonHelper.LogWarning?.Invoke("MCP", $"[{Name}] 获取工具列表失败: {ex.Message}");
            _connected = false;
            ScheduleReconnect();
            return _cachedTools;
        }
    }

    // ── Tool execution (fail-fast) ────────────────────────

    public async Task<object?> ExecuteToolAsync(ToolCallRequest toolCall, CancellationToken ct)
    {
        if (_client == null || !_connected)
            return $"[{Name}] 未连接，工具不可用";

        try
        {
            var jsonArgs = toolCall.Function.ParseArguments();
            var args = new Dictionary<string, object?>();
            if (jsonArgs != null)
            {
                foreach (var prop in jsonArgs.RootElement.EnumerateObject())
                    args[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.Number => prop.Value.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => prop.Value.GetRawText()
                    };
            }
            var result = await _client.CallToolAsync(
                toolCall.Function.Name,
                args,
                cancellationToken: ct);

            if (result?.Content == null || result.Content.Count == 0)
                return "工具返回空结果";

            var texts = result.Content
                .Select(c => c?.ToString() ?? "")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            return texts.Count > 0 ? string.Join("\n", texts) : "(非文本内容)";
        }
        catch (Exception ex)
        {
            CommonHelper.LogWarning?.Invoke("MCP", $"[{Name}] 工具调用失败: {ex.Message}");
            _connected = false;
            ScheduleReconnect();
            return $"[{Name}] 调用失败: {ex.Message}";
        }
    }
}
