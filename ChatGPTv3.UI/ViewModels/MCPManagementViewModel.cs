using ChatGPTv3.Core.Model.MCP;
using ChatGPTv3.OpenAIClient;
using ChatGPTv3.UI.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;

namespace ChatGPTv3.UI.ViewModels;

public partial class MCPServerNode : ObservableObject
{
    public string Name { get; init; } = string.Empty;

    public string TypeBadge
    {
        get
        {
            if (IsBuiltInGroup)
            {
                return "内置";
            }

            return Client?.ToolType switch
            {
                MCPClientType.Http => "Http",
                MCPClientType.SSE => "SSE",
                MCPClientType.Stdio => "Stdio",
                _ => "?"
            };
        }
    }

    public bool IsBuiltInGroup { get; init; }

    public MCPClientBase? Client { get; init; }

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _enabled;

    [ObservableProperty]
    private string _status = string.Empty;

    public ObservableCollection<MCPToolLeaf> Tools { get; } = [];

    partial void OnEnabledChanged(bool value)
    {
        if (Client != null && !IsBuiltInGroup)
        {
            Client.Enabled = value;
        }
    }
}

public partial class MCPToolLeaf : ObservableObject
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string ServerName { get; init; } = string.Empty;

    public string ClientName { get; init; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _alias = string.Empty;

    public string DisplayName => string.IsNullOrEmpty(Alias) ? Name : Alias;
}

public partial class MCPManagementViewModel : ViewModelBase
{
    public ObservableCollection<MCPServerNode> ServerNodes { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClientSelected))]
    [NotifyPropertyChangedFor(nameof(SelectedClientName))]
    [NotifyPropertyChangedFor(nameof(SelectedClientType))]
    [NotifyPropertyChangedFor(nameof(HasExternalServerSelected))]
    [NotifyPropertyChangedFor(nameof(IsSelectedClientEnabled))]
    private MCPServerNode? _selectedServer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClientSelected))]
    [NotifyPropertyChangedFor(nameof(SelectedClientName))]
    [NotifyPropertyChangedFor(nameof(SelectedClientType))]
    [NotifyPropertyChangedFor(nameof(HasToolSelected))]
    [NotifyPropertyChangedFor(nameof(IsSelectedClientEnabled))]
    private MCPToolLeaf? _selectedTool;

    // ── Tool alias ──
    [ObservableProperty]
    private string _toolAlias = string.Empty;

    public bool HasClientSelected => SelectedServer?.Client != null || SelectedTool != null;

    public bool HasToolSelected => SelectedTool != null;

    public bool HasExternalServerSelected =>
        SelectedServer != null && !SelectedServer.IsBuiltInGroup;

    public bool IsSelectedClientEnabled => GetSelectedClient()?.Enabled ?? false;

    public string SelectedClientName =>
        SelectedTool?.Name ?? SelectedServer?.Name ?? string.Empty;

    public string SelectedClientType =>
        SelectedTool != null
            ? MCPClientManager.Clients.FirstOrDefault(c => c.Name == SelectedTool.ClientName)?.ToolType switch
            {
                MCPClientType.Http => "Http",
                MCPClientType.SSE => "SSE",
                MCPClientType.Stdio => "Stdio",
                _ => "内置"
            }
            : SelectedServer?.TypeBadge ?? string.Empty;

    partial void OnSelectedToolChanged(MCPToolLeaf? value)
    {
        TestJsonArgs = "{}";
        TestResult = string.Empty;

        if (value == null)
        {
            return;
        }

        var client = MCPClientManager.Clients.FirstOrDefault(c => c.Name == value.ClientName);
        LoadClientPermissions(client);
        ToolAlias = client?.ToolNameConverters.GetValueOrDefault(value.Name, string.Empty) ?? string.Empty;

        _ = LoadToolSchemaAsync(value);
    }

    private async Task LoadToolSchemaAsync(MCPToolLeaf value)
    {
        try
        {
            var tools = await MCPClientManager.GetToolsForClientAsync(value.ClientName);
            var tool = tools.FirstOrDefault(t => t.Function.Name == value.Name);
            if (tool != null)
            {
                TestJsonArgs = MCPToolTestViewModel.GenerateDefaultArgs(tool.Function.Parameters);
            }
        }
        catch
        {
            // Ignore — use empty JSON
        }
    }

    [ObservableProperty]
    private bool _groupEnabled;

    [ObservableProperty]
    private bool _personEnabled;

    [ObservableProperty]
    private bool _canOnlyMasterCall;

    [ObservableProperty]
    private bool _isGroupBlackList;

    [ObservableProperty]
    private string _groupsText = string.Empty;

    [ObservableProperty]
    private bool _isPersonBlackList;

    [ObservableProperty]
    private string _personsText = string.Empty;

    [ObservableProperty]
    private int _selectedTabIndex;

    // ── Tool test context ──
    [ObservableProperty]
    private long _testGroupId;

    [ObservableProperty]
    private long _testQQ = 10000;

    [ObservableProperty]
    private bool _testIsGroupChat;
    [ObservableProperty]
    private string _testJsonArgs = "{}";

    [ObservableProperty]
    private string _testResult = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExecuteTest))]
    private bool _isTestBusy;

    public bool CanExecuteTest => !IsTestBusy;

    public MCPManagementViewModel()
    {
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var builtIn = new MCPServerNode
        {
            Name = "内置工具",
            IsBuiltInGroup = true,
            Status = "加载中..."
        };
        ServerNodes.Add(builtIn);

        // Add server nodes synchronously first
        foreach (var client in MCPClientManager.Clients)
        {
            if (client is MCPCustomClient)
            {
                continue;
            }

            var node = new MCPServerNode
            {
                Name = client.Name,
                IsBuiltInGroup = false,
                Client = client,
                Enabled = client.Enabled
            };
            ServerNodes.Add(node);
        }

        // Load tools asynchronously
        var builtInTools = new List<MCPToolLeaf>();
        foreach (var client in MCPClientManager.Clients)
        {
            if (client is MCPCustomClient)
            {
                var tools = await MCPClientManager.GetToolsForClientAsync(client.Name);
                foreach (var t in tools)
                {
                    builtInTools.Add(new MCPToolLeaf
                    {
                        Name = t.Function.Name,
                        Description = t.Function.Description,
                        ServerName = "内置工具",
                        ClientName = client.Name,
                        Alias = client.ToolNameConverters.GetValueOrDefault(t.Function.Name, string.Empty)
                    });
                }
            }
            else
            {
                var serverNode = ServerNodes.FirstOrDefault(n => n.Client == client);
                if (serverNode != null)
                {
                    LoadExternalTools(serverNode);
                }
            }
        }

        foreach (var t in builtInTools.OrderBy(x => x.Name))
        {
            builtIn.Tools.Add(t);
        }

        builtIn.Status = $"{builtIn.Tools.Count} 个工具";
    }

    private async void LoadExternalTools(MCPServerNode node)
    {
        if (node.Client == null)
        {
            return;
        }

        node.Status = "加载中...";
        try
        {
            var tools = await MCPClientManager.GetToolsForClientAsync(node.Client.Name);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                node.Tools.Clear();
                node.Status = tools.Length > 0
                    ? $"{tools.Length} 个工具"
                    : "无工具";
                if (node.Client is MCPExternalClient ext && !ext.IsConnected)
                {
                    node.Status = "未连接";
                }

                foreach (var t in tools.OrderBy(x => x.Function.Name))
                {
                    node.Tools.Add(new MCPToolLeaf
                    {
                        Name = t.Function.Name,
                        Description = t.Function.Description,
                        ServerName = node.Name,
                        ClientName = node.Client.Name,
                        Alias = node.Client.ToolNameConverters.GetValueOrDefault(t.Function.Name, string.Empty)
                    });
                }
            });
        }
        catch
        {
            node.Status = "加载失败";
        }
    }

    partial void OnSelectedServerChanged(MCPServerNode? value)
    {
        LoadClientPermissions(value?.Client);
        ToolAlias = string.Empty;
    }

    private void LoadClientPermissions(MCPClientBase? client)
    {
        if (client == null)
        {
            return;
        }

        var toolName = SelectedTool?.Name ?? string.Empty;
        var perm = !string.IsNullOrEmpty(toolName)
            ? client.GetToolPermission(toolName)
            : null;

        _groupEnabled = perm?.GroupEnabled ?? client.GroupEnabled;
        _personEnabled = perm?.PersonEnabled ?? client.PersonEnabled;
        _canOnlyMasterCall = perm?.CanOnlyMasterCall ?? client.CanOnlyMasterCall;
        _isGroupBlackList = perm?.IsGroupBlackList ?? client.IsGroupBlackList;
        _groupsText = perm != null ? string.Join(", ", perm.Groups) : string.Join(", ", client.Groups);
        _isPersonBlackList = perm?.IsPersonBlackList ?? client.IsPersonBlackList;
        _personsText = perm != null ? string.Join(", ", perm.Persons) : string.Join(", ", client.Persons);
        OnPropertyChanged(nameof(GroupEnabled));
        OnPropertyChanged(nameof(PersonEnabled));
        OnPropertyChanged(nameof(CanOnlyMasterCall));
        OnPropertyChanged(nameof(IsGroupBlackList));
        OnPropertyChanged(nameof(GroupsText));
        OnPropertyChanged(nameof(IsPersonBlackList));
        OnPropertyChanged(nameof(PersonsText));
    }

    private MCPClientBase? GetSelectedClient()
    {
        if (SelectedServer?.Client != null)
        {
            return SelectedServer.Client;
        }

        if (SelectedTool != null)
        {
            return MCPClientManager.Clients.FirstOrDefault(c => c.Name == SelectedTool.ClientName);
        }

        return null;
    }

    [RelayCommand]
    private void SavePermissions()
    {
        var client = GetSelectedClient();
        if (client == null)
        {
            return;
        }

        var groups = ParseLongList(GroupsText);
        var persons = ParseLongList(PersonsText);
        var alias = ToolAlias?.Trim();
        var toolName = SelectedTool?.Name ?? string.Empty;

        MCPClientManager.UpdateClient(client.Name, c =>
        {
            c.Enabled = client.Enabled;

            if (!string.IsNullOrEmpty(toolName))
            {
                // Save per-tool permissions
                c.PerToolPermissions[toolName] = new MCPToolPermission
                {
                    GroupEnabled = GroupEnabled,
                    PersonEnabled = PersonEnabled,
                    CanOnlyMasterCall = CanOnlyMasterCall,
                    IsGroupBlackList = IsGroupBlackList,
                    Groups = groups,
                    IsPersonBlackList = IsPersonBlackList,
                    Persons = persons
                };

                if (!string.IsNullOrEmpty(alias))
                {
                    c.ToolNameConverters[toolName] = alias;
                }
                else
                {
                    c.ToolNameConverters.Remove(toolName);
                }
            }
            else
            {
                // Save server-level permissions
                c.GroupEnabled = GroupEnabled;
                c.PersonEnabled = PersonEnabled;
                c.CanOnlyMasterCall = CanOnlyMasterCall;
                c.IsGroupBlackList = IsGroupBlackList;
                c.Groups = groups;
                c.IsPersonBlackList = IsPersonBlackList;
                c.Persons = persons;
            }
        });
        Growl.Success($"已保存 {client.Name} 的权限配置");
    }

    [RelayCommand]
    private void RebuildAll()
    {
        MCPClientManager.Rebuild();
        Growl.Success("已重建所有 MCP 客户端连接");
    }

    [RelayCommand]
    private void DeleteServer()
    {
        if (SelectedServer?.Client == null || SelectedServer.IsBuiltInGroup)
        {
            return;
        }

        var msgBox = HandyControl.Controls.MessageBox.Show(
            $"确定要删除 MCP 服务端 \"{SelectedServer.Name}\" 吗？",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (msgBox != MessageBoxResult.Yes)
        {
            return;
        }

        var serverName = SelectedServer.Name;
        MCPClientManager.RemoveClient(serverName);
        ServerNodes.Remove(SelectedServer);
        SelectedServer = null;
        Growl.Success($"已删除 {serverName}");
    }

    [RelayCommand]
    private void ToggleEnabled()
    {
        var client = GetSelectedClient();
        if (client == null)
        {
            return;
        }

        client.Enabled = !client.Enabled;
        MCPClientManager.Save();
        if (SelectedServer != null)
        {
            SelectedServer.Enabled = client.Enabled;
        }
        OnPropertyChanged(nameof(IsSelectedClientEnabled));
        Growl.Success($"{(client.Enabled ? "已启用" : "已禁用")} {client.Name}");
    }

    [RelayCommand]
    private void OpenAddClientDialog()
    {
        var dialog = new MCPAddClientDialog { Owner = GetActiveWindow() };
        dialog.ShowDialog();
        RefreshAll();
    }

    [RelayCommand]
    private async Task RefreshExternalTools()
    {
        if (SelectedServer == null || SelectedServer.IsBuiltInGroup)
        {
            return;
        }

        LoadExternalTools(SelectedServer);
    }

    [RelayCommand]
    private void OpenToolTestDialog(MCPToolLeaf? tool)
    {
        if (tool == null)
        {
            return;
        }

        var dialog = new MCPToolTestDialog(tool.ClientName, tool.Name)
        {
            Owner = GetActiveWindow()
        };
        dialog.ShowDialog();
    }

    partial void OnIsTestBusyChanged(bool value)
    {
        if (value)
        {
            _ = ExecuteTestAsync();
        }
    }

    [RelayCommand]
    private async Task ExecuteToolTest()
    {
        // Triggered by ExecuteToolTestCommand binding; kept for backward compat.
        // Actual execution now goes through OnIsTestBusyChanged → ExecuteTestAsync.
        IsTestBusy = true;
    }

    private async Task ExecuteTestAsync()
    {
        TestResult = string.Empty;

        try
        {
            // Validate JSON args
            try
            {
                JsonDocument.Parse(TestJsonArgs);
            }
            catch (JsonException ex)
            {
                Growl.Error($"JSON 解析失败: {ex.Message}");
                return;
            }

            var toolCall = new ToolCallRequest
            {
                Id = "ui-test",
                Function = new FunctionCall
                {
                    Name = SelectedTool.Name,
                    Arguments = TestJsonArgs
                }
            };

            var context = new MCPToolContext
            {
                GroupId = TestIsGroupChat ? TestGroupId : 0,
                QQ = TestQQ,
                ChatIdentity = "管理面板工具测试"
            };

            var result = await MCPClientManager.ExecuteToolAsync(toolCall, CancellationToken.None, context);
            TestResult = result?.ToString() ?? "(null)";
        }
        catch (Exception ex)
        {
            TestResult = $"错误: {ex.Message}";
            Growl.Error($"工具执行失败: {ex.Message}");
        }
        finally
        {
            IsTestBusy = false;
        }
    }

    private void RefreshAll()
    {
        ServerNodes.Clear();
        _ = InitializeAsync();
    }

    private static long[] ParseLongList(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, out var n) ? n : (long?)null)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .Distinct()
            .ToArray();
    }

    private static System.Windows.Window? GetActiveWindow()
    {
        return Application.Current.Windows
            .OfType<System.Windows.Window>()
            .FirstOrDefault(w => w.IsActive);
    }
}
