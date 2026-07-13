using ChatGPTv3.Core.Model.MCP;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace ChatGPTv3.UI.ViewModels;

public partial class MCPToolTestViewModel : ObservableObject
{
    public string ClientName { get; }
    public string ToolName { get; }

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
