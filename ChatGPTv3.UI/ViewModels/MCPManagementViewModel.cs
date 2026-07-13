using ChatGPTv3.Core.Model.MCP;
using ChatGPTv3.UI.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using System.Collections.ObjectModel;
using System.Windows;

namespace ChatGPTv3.UI.ViewModels;

public partial class MCPClientItem : ObservableObject
{
    public MCPClientBase Client { get; }

    public string Name => Client.Name;

    public string TypeBadge => Client.ToolType switch
    {
        MCPClientType.Custom => "内置",
        MCPClientType.Http => "Http",
        MCPClientType.SSE => "SSE",
        MCPClientType.Stdio => "Stdio",
        _ => "?"
    };

    public bool IsBuiltIn => Client.ToolType == MCPClientType.Custom;

    [ObservableProperty]
    private bool _enabled;

    partial void OnEnabledChanged(bool value)
    {
        Client.Enabled = value;
    }

    public MCPClientItem(MCPClientBase client)
    {
        Client = client;
        _enabled = client.Enabled;
    }
}

public partial class MCPManagementViewModel : ViewModelBase
{
    public ObservableCollection<MCPClientItem> Clients { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(SelectedClientName))]
    [NotifyPropertyChangedFor(nameof(IsBuiltInSelected))]
    [NotifyPropertyChangedFor(nameof(IsExternalSelected))]
    private MCPClientItem? _selectedClient;

    public bool HasSelection => SelectedClient != null;

    public string SelectedClientName => SelectedClient?.Name ?? string.Empty;

    public bool IsBuiltInSelected => SelectedClient?.IsBuiltIn ?? false;

    public bool IsExternalSelected => SelectedClient != null && !SelectedClient.IsBuiltIn;

    // ── Permission editing proxies ──

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private bool _groupEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private bool _personEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private bool _canOnlyMasterCall;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private bool _isGroupBlackList;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private string _groupsText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private bool _isPersonBlackList;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private string _personsText = string.Empty;

    public MCPManagementViewModel()
    {
        foreach (var client in MCPClientManager.Clients.OrderBy(c => c.ToolType != MCPClientType.Custom).ThenBy(c => c.Name))
        {
            Clients.Add(new MCPClientItem(client));
        }
    }

    partial void OnSelectedClientChanged(MCPClientItem? value)
    {
        if (value == null)
        {
            return;
        }

        var c = value.Client;
        _groupEnabled = c.GroupEnabled;
        _personEnabled = c.PersonEnabled;
        _canOnlyMasterCall = c.CanOnlyMasterCall;
        _isGroupBlackList = c.IsGroupBlackList;
        _groupsText = string.Join(", ", c.Groups);
        _isPersonBlackList = c.IsPersonBlackList;
        _personsText = string.Join(", ", c.Persons);
        OnPropertyChanged(nameof(GroupEnabled));
        OnPropertyChanged(nameof(PersonEnabled));
        OnPropertyChanged(nameof(CanOnlyMasterCall));
        OnPropertyChanged(nameof(IsGroupBlackList));
        OnPropertyChanged(nameof(GroupsText));
        OnPropertyChanged(nameof(IsPersonBlackList));
        OnPropertyChanged(nameof(PersonsText));
    }

    [RelayCommand]
    private void SavePermissions()
    {
        if (SelectedClient == null)
        {
            return;
        }

        var groups = ParseLongList(GroupsText);
        var persons = ParseLongList(PersonsText);

        MCPClientManager.UpdateClient(SelectedClient.Name, client =>
        {
            client.Enabled = SelectedClient.Enabled;
            client.GroupEnabled = GroupEnabled;
            client.PersonEnabled = PersonEnabled;
            client.CanOnlyMasterCall = CanOnlyMasterCall;
            client.IsGroupBlackList = IsGroupBlackList;
            client.Groups = groups;
            client.IsPersonBlackList = IsPersonBlackList;
            client.Persons = persons;
        });

        Growl.Success($"已保存 {SelectedClient.Name} 的权限配置");
    }

    [RelayCommand]
    private void RebuildAll()
    {
        MCPClientManager.Rebuild();
        Growl.Success("已重建所有 MCP 客户端连接");
    }

    [RelayCommand]
    private void DeleteClient()
    {
        if (SelectedClient == null || SelectedClient.IsBuiltIn)
        {
            return;
        }

        var name = SelectedClient.Name;
        MCPClientManager.RemoveClient(name);
        Clients.Remove(SelectedClient);
        SelectedClient = null;
        Growl.Success($"已删除 {name}");
    }

    [RelayCommand]
    private void ToggleEnabled()
    {
        if (SelectedClient == null)
        {
            return;
        }

        SelectedClient.Enabled = !SelectedClient.Enabled;
        SavePermissions();
    }

    [RelayCommand]
    private void OpenAddClientDialog()
    {
        var dialog = new MCPAddClientDialog() { Owner = GetActiveWindow() };
        dialog.ShowDialog();
        RefreshClientList();
    }

    [RelayCommand]
    private void OpenToolTestDialog()
    {
        if (SelectedClient == null)
        {
            return;
        }

        // For external clients, we'd need to get tool names differently
        var dialog = new MCPToolTestDialog(SelectedClient.Name, SelectedClient.Name);
        dialog.ShowDialog();
    }

    private void RefreshClientList()
    {
        var selectedName = SelectedClient?.Name;
        Clients.Clear();
        foreach (var client in MCPClientManager.Clients.OrderBy(c => c.ToolType != MCPClientType.Custom).ThenBy(c => c.Name))
        {
            Clients.Add(new MCPClientItem(client));
        }

        if (selectedName != null)
        {
            SelectedClient = Clients.FirstOrDefault(c => c.Name == selectedName);
        }
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

    private static System.Windows.Window? GetActiveWindow() =>
        System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive);
}
