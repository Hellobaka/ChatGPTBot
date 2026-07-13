using ChatGPTv3.Core.Model.MCP;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;

namespace ChatGPTv3.UI.ViewModels;

public partial class MCPAddClientViewModel : ObservableObject
{
    public Action? RequestClose { get; set; }

    [ObservableProperty]
    private string _clientType = "Http";

    [ObservableProperty]
    private string _clientName = string.Empty;

    [ObservableProperty]
    private string _endpoint = string.Empty;

    [ObservableProperty]
    private string _command = string.Empty;

    [ObservableProperty]
    private string _arguments = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStdio))]
    [NotifyPropertyChangedFor(nameof(IsSse))]
    private bool _isHttp = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHttp))]
    [NotifyPropertyChangedFor(nameof(IsStdio))]
    private bool _isSse;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHttp))]
    [NotifyPropertyChangedFor(nameof(IsSse))]
    private bool _isStdio;

    [ObservableProperty]
    private string _sseEndpoint = string.Empty;

    partial void OnIsHttpChanged(bool value)
    {
        if (value)
        {
            ClientType = "Http";
            IsSse = false;
            IsStdio = false;
        }
    }

    partial void OnIsSseChanged(bool value)
    {
        if (value)
        {
            ClientType = "SSE";
            IsHttp = false;
            IsStdio = false;
        }
    }

    partial void OnIsStdioChanged(bool value)
    {
        if (value)
        {
            ClientType = "Stdio";
            IsHttp = false;
            IsSse = false;
        }
    }

    [RelayCommand]
    private void SetHttp()
    {
        IsHttp = true;
        IsStdio = false;
    }

    [RelayCommand]
    private void SetStdio()
    {
        IsHttp = false;
        IsStdio = true;
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(ClientName))
        {
            Growl.Warning("请输入客户端名称");
            return;
        }

        if (IsHttp || IsSse)
        {
            var endpoint = IsHttp ? Endpoint : SseEndpoint;
            var label = IsHttp ? "Endpoint URL" : "SSE Endpoint URL";
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                Growl.Warning($"请输入 {label}");
                return;
            }

            var client = new MCPHttpClient
            {
                Name = ClientName.Trim(),
                Endpoint = endpoint.Trim(),
                TransportMode = IsHttp ? "StreamableHttp" : "SSE",
                ToolType = IsHttp ? MCPClientType.Http : MCPClientType.SSE
            };
            MCPClientManager.AddClient(client);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(Command))
            {
                Growl.Warning("请输入 Command 路径");
                return;
            }

            var args = string.IsNullOrWhiteSpace(Arguments)
                ? []
                : Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var client = new MCPStdioClient { Name = ClientName.Trim(), Command = Command.Trim(), Arguments = args };
            MCPClientManager.AddClient(client);
        }

        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke();
    }
}
