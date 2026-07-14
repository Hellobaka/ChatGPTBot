using ChatGPTv3.Core.Model.MCP;
using ChatGPTv3.OpenAIClient;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ChatGPTv3.UI.ViewModels;

public partial class MCPToolTestViewModel : ObservableObject
{
    public string ClientName { get; }
    public string ToolName { get; }
    public string ToolDescription { get; }

    [ObservableProperty]
    private string _jsonArgs = "{}";

    [ObservableProperty]
    private string _result = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExecute))]
    private bool _isBusy;

    public bool CanExecute => !IsBusy;

    public MCPToolTestViewModel(string clientName, string toolName)
    {
        ClientName = clientName;
        ToolName = toolName;
        ToolDescription = string.Empty;

        try
        {
            var tools = MCPSelfBuiltinTools.GetToolDefinitionsForClient(clientName, null);
            var tool = tools.FirstOrDefault(t => t.Function.Name == toolName);
            if (tool != null)
            {
                ToolDescription = tool.Function.Description;
                JsonArgs = GenerateDefaultArgs(tool.Function.Parameters);
            }
        }
        catch
        {
            // If tool lookup fails, keep defaults
        }
    }

    /// <summary>
    /// Generates a default JSON args object from the tool's JSON Schema parameters.
    /// Maps types: string→"", integer/number→0, boolean→false, array→[], object→{}, other→null.
    /// </summary>
    public static string GenerateDefaultArgs(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object
            || !schema.TryGetProperty("properties", out var props)
            || props.ValueKind != JsonValueKind.Object)
        {
            return "{}";
        }

        var args = new Dictionary<string, object?>();
        foreach (var prop in props.EnumerateObject())
        {
            var type = "string";
            if (prop.Value.TryGetProperty("type", out var typeElem))
            {
                if (typeElem.ValueKind == JsonValueKind.Array)
                {
                    type = typeElem.EnumerateArray()
                        .Select(e => e.GetString())
                        .FirstOrDefault(s => s != "null") ?? "string";
                }
                else
                {
                    type = typeElem.GetString() ?? "string";
                }
            }

            args[prop.Name] = type switch
            {
                "string" => string.Empty,
                "integer" or "number" => 0,
                "boolean" => false,
                "array" => System.Array.Empty<object>(),
                "object" => new Dictionary<string, object>(),
                _ => null
            };
        }

        return JsonSerializer.Serialize(args, new JsonSerializerOptions { WriteIndented = true });
    }

    [RelayCommand]
    private async Task ExecuteAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Result = string.Empty;

        try
        {
            JsonDocument? args;
            try
            {
                args = JsonDocument.Parse(JsonArgs);
            }
            catch (JsonException ex)
            {
                Growl.Error($"JSON 解析失败: {ex.Message}");
                return;
            }

            var context = new MCPToolContext
            {
                GroupId = 0,
                QQ = 0,
                ChatIdentity = "管理面板工具测试"
            };

            var result = await MCPSelfBuiltinTools.ExecuteToolAsync(ToolName, args, context);
            Result = result?.ToString() ?? "(null)";
        }
        catch (Exception ex)
        {
            Result = $"错误: {ex.Message}";
            Growl.Error($"工具执行失败: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
