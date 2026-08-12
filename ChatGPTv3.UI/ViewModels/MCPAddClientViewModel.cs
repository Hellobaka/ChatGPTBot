using ChatGPTv3.Core.Model.MCP;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HandyControl.Controls;
using System.Collections.ObjectModel;

namespace ChatGPTv3.UI.ViewModels;

public partial class EnvVarItem : ObservableObject
{
    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;
}

public partial class HeaderItem : ObservableObject
{
    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string _value = string.Empty;
}

public partial class MCPAddClientViewModel : ObservableObject
{
    public Action? RequestClose { get; set; }

    public bool IsEditMode { get; }

    public bool CanChangeType => !IsEditMode;

    public string WindowTitle => IsEditMode ? "编辑 MCP 客户端" : "添加 MCP 客户端";

    public string ConfirmButtonText => IsEditMode ? "保存" : "添加";

    /// <summary>Explicit HTTP transport options; SSE is just another HTTP mode.</summary>
    public IReadOnlyList<string> TransportModes { get; } = ["StreamableHttp", "SSE"];

    private readonly MCPClientBase? _editingClient;

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
    private bool _isHttp = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHttp))]
    private bool _isStdio;

    [ObservableProperty]
    private string _transportMode = "StreamableHttp";

    public ObservableCollection<EnvVarItem> EnvironmentVariables { get; } = [];

    public ObservableCollection<HeaderItem> Headers { get; } = [];

    public MCPAddClientViewModel()
    {
    }

    /// <summary>Creates the dialog in edit mode for an existing client.</summary>
    public MCPAddClientViewModel(MCPClientBase client)
    {
        IsEditMode = true;
        _editingClient = client;
        ClientName = client.Name;

        switch (client)
        {
            case MCPStdioClient stdio:
                IsStdio = true;
                Command = stdio.Command;
                Arguments = string.Join(' ', stdio.Arguments);
                foreach (var (key, value) in stdio.EnvironmentVariables)
                {
                    EnvironmentVariables.Add(new EnvVarItem { Key = key, Value = value });
                }
                break;

            case MCPHttpClient http:
                IsHttp = true;
                Endpoint = http.Endpoint;
                TransportMode = http.TransportMode == "AutoDetect"
                    ? "StreamableHttp"
                    : http.TransportMode;
                foreach (var (key, value) in http.Headers)
                {
                    Headers.Add(new HeaderItem { Key = key, Value = value });
                }
                break;
        }
    }

    [RelayCommand]
    private void AddEnvVar()
    {
        EnvironmentVariables.Add(new EnvVarItem());
    }

    [RelayCommand]
    private void RemoveEnvVar(EnvVarItem? item)
    {
        if (item != null)
        {
            EnvironmentVariables.Remove(item);
        }
    }

    [RelayCommand]
    private void AddHeader()
    {
        Headers.Add(new HeaderItem());
    }

    [RelayCommand]
    private void RemoveHeader(HeaderItem? item)
    {
        if (item != null)
        {
            Headers.Remove(item);
        }
    }

    partial void OnIsHttpChanged(bool value)
    {
        if (value)
        {
            ClientType = "Http";
            IsStdio = false;
        }
    }

    partial void OnIsStdioChanged(bool value)
    {
        if (value)
        {
            ClientType = "Stdio";
            IsHttp = false;
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
    private async Task Add()
    {
        if (IsEditMode && _editingClient != null)
        {
            SaveExisting();
            RequestClose?.Invoke();
            return;
        }

        if (string.IsNullOrWhiteSpace(ClientName))
        {
            Growl.Warning("请输入客户端名称");
            return;
        }

        if (IsHttp)
        {
            if (string.IsNullOrWhiteSpace(Endpoint))
            {
                Growl.Warning("请输入 Endpoint URL");
                return;
            }

            var client = new MCPHttpClient
            {
                Name = ClientName.Trim(),
                Endpoint = Endpoint.Trim(),
                TransportMode = TransportMode,
                Headers = CollectHeaders()
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

            var envVars = EnvironmentVariables
                .Where(e => !string.IsNullOrWhiteSpace(e.Key))
                .ToDictionary(e => e.Key.Trim(), e => e.Value?.Trim() ?? string.Empty);

            var client = new MCPStdioClient
            {
                Name = ClientName.Trim(),
                Command = Command.Trim(),
                Arguments = args,
                EnvironmentVariables = envVars
            };
            MCPClientManager.AddClient(client);
        }

        RequestClose?.Invoke();
    }

    private void SaveExisting()
    {
        if (_editingClient == null)
        {
            return;
        }

        var originalName = _editingClient.Name;
        MCPClientManager.UpdateClient(originalName, c =>
        {
            c.Name = ClientName.Trim();

            if (c is MCPStdioClient stdio)
            {
                stdio.Command = Command.Trim();
                stdio.Arguments = string.IsNullOrWhiteSpace(Arguments)
                    ? []
                    : Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                stdio.EnvironmentVariables = EnvironmentVariables
                    .Where(e => !string.IsNullOrWhiteSpace(e.Key))
                    .ToDictionary(e => e.Key.Trim(), e => e.Value?.Trim() ?? string.Empty);
            }
            else if (c is MCPHttpClient http)
            {
                http.Endpoint = Endpoint.Trim();
                http.TransportMode = TransportMode;
                http.Headers = CollectHeaders();
            }
        });
    }

    private Dictionary<string, string> CollectHeaders() =>
        Headers
            .Where(h => !string.IsNullOrWhiteSpace(h.Key))
            .ToDictionary(h => h.Key.Trim(), h => h.Value?.Trim() ?? string.Empty);

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke();
    }
}
